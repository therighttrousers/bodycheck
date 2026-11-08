namespace BodyCheck.Core.Tasks;

/// <summary>
/// What runs <see cref="TickTask"/>s: the queue of what waits for tasks that have finished, and the clock that its
/// waits count, which its owner runs once a tick, and stops when it is done with it, which unwinds whatever waits on
/// it. What fails with nothing to handle it goes to the owner's sink, and everything else carries on.
/// </summary>
/// <param name="unhandled">
/// Where unhandled failures go: the exception of each failed task that nothing has awaited by the end of the run in
/// which it failed, and of each scope's condition that throws. If it throws, so does the run.
/// </param>
/// <param name="cutShort">
/// Where warnings of what a scope cut short go: the cancellation of each task that a scope's ending cancelled, and that
/// nothing has awaited by the end of the run, such as the task of a trigger's route whose script's scope ended. If it
/// throws, so does the run. With none, they aren't reported.
/// </param>
public sealed class TickScheduler(Action<Exception> unhandled, Action<OperationCanceledException>? cutShort = null)
{
    private readonly Queue<Action> _queue = new();

    // Each with the condition of its tick's scope, if it has one.
    private readonly List<(TickTask Task, double Due, Func<bool>? Condition)> _waits = [];

    // What to report since the end of the last run, in order, at the end of this one: the tasks that have failed or a
    // scope has cancelled, if nothing has awaited them by then, and the exceptions of the scopes' conditions that have
    // thrown.
    private readonly List<(TickTask? Task, Exception? Exception)> _failures = [];

    // The scopes' conditions that have thrown since the end of the last run, so that each is reported once.
    private readonly HashSet<Func<bool>> _conditionsThrown = new(ReferenceEqualityComparer.Instance);

    // The time that has passed in the ticks this scheduler has run.
    private double _time;

    // The dt passed to the last call to Run.
    private float _dt;

    private bool _running;

    private bool _stopped;

    /// <summary>
    /// The scheduler's tick, as its tasks see it, with no scope: what it waits on ends only when the scheduler stops.
    /// </summary>
    public ITick Tick => field ??= new ScopedTick(this, null);

    /// <summary>Adds what waits for a task that has finished, to run when the scheduler next runs.</summary>
    internal void Add(Action continuation) => _queue.Enqueue(continuation);

    /// <summary>
    /// Notes a task that has failed or been cancelled, to report at the end of the run if nothing has awaited it and it
    /// failed, or a scope's ending cancelled it.
    /// </summary>
    internal void Settled(TickTask task) => _failures.Add((task, null));

    /// <summary>
    /// A wait of <paramref name="seconds"/>, which is cancelled if <paramref name="condition"/> no longer holds when
    /// it is made, or in a run before it is due.
    /// </summary>
    internal TickTask Wait(float seconds, Func<bool>? condition)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(seconds);
        var task = new TickTask(this);
        if (_stopped)
        {
            // Such as by a finally block that waits, as a stop unwinds it: it would never finish.
            task.Cancel(Stopped());
        }
        else if (!Holds(condition))
        {
            task.Cancel(new ScopeEndedException());
        }
        else
        {
            _waits.Add((task, _time + seconds, condition));
        }
        return task;
    }

    /// <summary>The length of the tick being run, or of the last one run.</summary>
    internal float Dt => _dt;

    /// <summary>The total length of the ticks the scheduler has run, counting the one being run.</summary>
    internal double Time => _time;

    /// <summary>
    /// Stops the scheduler for good, unwinding everything that waits on it: cancels every wait that hasn't finished,
    /// then runs what waits for the tasks that have, and for those it cancelled, until nothing is left. Awaiting a
    /// cancelled task throws an <see cref="OperationCanceledException"/>, so every method waiting on the scheduler
    /// unwinds in this call, through its <c>using</c> and <c>finally</c> blocks, while what they clean up is still
    /// there. A wait made after it has stopped is cancelled at once. It runs no more ticks.
    /// <para>
    /// A cleanup that fails, such as a <c>finally</c> block that throws, fails its method's task, and goes to the sink
    /// once everything has unwound, if nothing awaits the task.
    /// </para>
    /// </summary>
    public void Stop()
    {
        if (_running)
        {
            throw new InvalidOperationException("A TickScheduler can't be stopped while it runs.");
        }
        if (_stopped)
        {
            return;
        }
        _stopped = true;
        _running = true;
        try
        {
            foreach ((TickTask task, double _, Func<bool>? _) in _waits)
            {
                task.Cancel(Stopped());
            }
            _waits.Clear();
            RunQueued();
            ReportUnhandled();
        }
        finally
        {
            _running = false;
        }
    }

    private static OperationCanceledException Stopped() => new("The TickScheduler was stopped.");


    /// <summary>
    /// Runs one tick: cancels the waits whose scopes have ended and finishes those that are due, then runs what waits
    /// for every task that has finished, until nothing is left, so that a chain of tasks that finish at once runs to its
    /// end in one tick. Last, it gives the sink the failures that nothing has handled.
    /// </summary>
    /// <param name="dt">The tick's length on the clock that waits count.</param>
    public void Run(float dt)
    {
        if (_running)
        {
            throw new InvalidOperationException("The TickScheduler is already running.");
        }
        if (_stopped)
        {
            throw new InvalidOperationException("The TickScheduler has stopped.");
        }
        _running = true;
        try
        {
            _dt = dt;
            _time += dt;
            // A wait is due in the tick that ends nearest it: the first that ends no more than half a tick before it.
            SettleWaits(dueBy: _time + dt / 2);
            RunQueued();
            ReportUnhandled();
        }
        finally
        {
            _running = false;
        }
    }

    /// <summary>
    /// Ends the scopes whose conditions no longer hold, between runs, without a tick passing: cancels their waits, then
    /// runs what waits for them, as <see cref="Run"/> does, until nothing is left, and gives the sinks what that leaves
    /// unhandled. No wait finishes. It is for an owner that has made a scope's condition false and must have what runs
    /// in it unwind now, while what it cleans up is still there, such as an entity about to be removed.
    /// </summary>
    public void EndScopes()
    {
        if (_running)
        {
            throw new InvalidOperationException("The TickScheduler is already running.");
        }
        if (_stopped)
        {
            return;
        }
        _running = true;
        try
        {
            SettleWaits(dueBy: null);
            RunQueued();
            ReportUnhandled();
        }
        finally
        {
            _running = false;
        }
    }

    /// <summary>Runs what is queued, and what that queues, until nothing is left.</summary>
    private void RunQueued()
    {
        try
        {
            while (_queue.TryDequeue(out Action? continuation))
            {
                // An async method's continuation doesn't throw: its builder fails its task with what the method throws.
                continuation();
            }
        }
        catch
        {
            // Only a bug in the tasks themselves gets here. We can't count on the state of what is queued, so we don't
            // run it.
            _queue.Clear();
            throw;
        }
    }

    /// <summary>
    /// Gives the sinks what has failed with nothing to handle it, and what a scope has cut short with nothing to hear of
    /// it, in the order they happened, and forgets them: the tasks that nothing has awaited, and the scopes' conditions
    /// that have thrown. A task awaited later fails or cancels what awaits it, which may be reported in turn.
    /// </summary>
    private void ReportUnhandled()
    {
        // Copied, so that if the sink throws, nothing is reported twice.
        List<(TickTask? Task, Exception? Exception)> failures = [.. _failures];
        _failures.Clear();
        _conditionsThrown.Clear();
        foreach ((TickTask? task, Exception? exception) in failures)
        {
            if ((task?.UnhandledFailure ?? exception) is Exception e)
            {
                unhandled(e);
            }
            else if (task?.UnheardCancellation is OperationCanceledException cancellation)
            {
                cutShort?.Invoke(cancellation);
            }
        }
    }

    /// <summary>
    /// Whether a scope's condition holds. One that throws counts as not holding, so that its scope ends: its waits are
    /// cancelled, which blames nothing. Its exception goes to the sink at the end of the run, once.
    /// </summary>
    private bool Holds(Func<bool>? condition)
    {
        if (condition is null)
        {
            return true;
        }
        try
        {
            return condition();
        }
        // Everything: the condition is the caller's code, and its failure goes to the sink, which is where it would
        // go if its caller didn't handle it.
        catch (Exception e)
        {
            if (_conditionsThrown.Add(condition))
            {
                _failures.Add((null, e));
            }
            return false;
        }
    }

    /// <summary>
    /// Settles the waits, in the order they were made: cancels those whose scopes have ended, and finishes those due by
    /// <paramref name="dueBy"/>, if it is given. A wait whose scope has ended is cancelled even if it is due.
    /// </summary>
    private void SettleWaits(double? dueBy)
    {
        int kept = 0;
        for (int i = 0; i < _waits.Count; i++)
        {
            (TickTask task, double due, Func<bool>? condition) = _waits[i];
            if (!Holds(condition))
            {
                task.Cancel(new ScopeEndedException());
            }
            else if (due <= dueBy)
            {
                task.Finish();
            }
            else
            {
                _waits[kept++] = _waits[i];
            }
        }
        _waits.RemoveRange(kept, _waits.Count - kept);
    }
}
