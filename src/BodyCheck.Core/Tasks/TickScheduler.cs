using System.Runtime.ExceptionServices;

namespace BodyCheck.Core.Tasks;

/// <summary>
/// What runs <see cref="TickTask"/>s: the queue of what waits for tasks that have finished, and the clock that its
/// waits count, which its owner runs once a tick, and stops when it is done with it, which unwinds whatever waits on
/// it.
/// </summary>
public sealed class TickScheduler
{
    private readonly Queue<Action> _queue = new();

    // Each with the condition of its tick's scope, if it has one.
    private readonly List<(TickTask Task, double Due, Func<bool>? Condition)> _waits = [];

    // The time that has passed in the ticks this scheduler has run.
    private double _time;

    // The dt passed to the last call to Run.
    private float _dt;

    private bool _running;

    private bool _stopped;

    public TickScheduler() => Tick = new ScopedTick(this, null);

    /// <summary>
    /// The scheduler's tick, as its tasks see it, with no scope: what it waits on ends only when the scheduler stops.
    /// </summary>
    public ITick Tick { get; }

    /// <summary>Adds what waits for a task that has finished, to run when the scheduler next runs.</summary>
    internal void Add(Action continuation) => _queue.Enqueue(continuation);

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
        else if (condition?.Invoke() == false)
        {
            task.Cancel(ScopeEnded());
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
    /// If cleanup fails, such as a <c>finally</c> block that throws, it unwinds everything else first, then throws
    /// the first failure, so that one failure doesn't leave the rest of the cleanup undone.
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

            ExceptionDispatchInfo? firstFailure = null;
            while (_queue.TryDequeue(out Action? continuation))
            {
                // Only what fails a task throws: kept, to throw once everything has unwound.
                try
                {
                    continuation();
                }
                catch (Exception e)
                {
                    firstFailure ??= ExceptionDispatchInfo.Capture(e);
                }
            }
            firstFailure?.Throw();
        }
        finally
        {
            _running = false;
        }
    }

    private static OperationCanceledException Stopped() => new("The TickScheduler was stopped.");

    private static OperationCanceledException ScopeEnded() => new("The scope of the tick waited on has ended.");

    /// <summary>
    /// Runs one tick: cancels the waits whose scopes have ended, finishes those that are due, then runs what waits for every task that has finished, until
    /// nothing is left, so that a chain of tasks that finish at once runs to its end in one tick.
    /// <para>
    /// If anything it runs throws, or a task has failed, it forgets everything queued and throws, failing the tick.
    /// </para>
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
            CancelWaitsOfEndedScopes();
            FinishDueWaits(dt);
            while (_queue.TryDequeue(out Action? continuation))
            {
                continuation();
            }
        }
        catch
        {
            // We can't count on the owner's state being as expected for what is queued, so don't run it.
            _queue.Clear();
            throw;
        }
        finally
        {
            _running = false;
        }
    }

    /// <summary>Cancels the waits whose scopes' conditions no longer hold, in the order they were made.</summary>
    private void CancelWaitsOfEndedScopes()
    {
        int kept = 0;
        int i = 0;
        try
        {
            for (; i < _waits.Count; i++)
            {
                if (_waits[i].Condition?.Invoke() == false)
                {
                    _waits[i].Task.Cancel(ScopeEnded());
                }
                else
                {
                    _waits[kept++] = _waits[i];
                }
            }
        }
        finally
        {
            // Drops the cancelled waits' entries. If a condition threw, which fails the tick, this also keeps its wait
            // and those not yet checked.
            _waits.RemoveRange(kept, i - kept);
        }
    }

    /// <summary>Finishes the waits due by the end of a tick of <paramref name="dt"/>, in the order they were made.</summary>
    private void FinishDueWaits(float dt)
    {
        // A wait is due in the tick that ends nearest it: the first that ends no more than half a tick before it.
        double dueBy = _time + dt / 2;
        int kept = 0;
        for (int i = 0; i < _waits.Count; i++)
        {
            if (_waits[i].Due <= dueBy)
            {
                _waits[i].Task.Finish();
            }
            else
            {
                _waits[kept++] = _waits[i];
            }
        }
        _waits.RemoveRange(kept, _waits.Count - kept);
    }
}
