using System.Runtime.ExceptionServices;

namespace BodyCheck.Core.Tasks;

/// <summary>
/// What runs <see cref="TickTask"/>s: the queue of what waits for tasks that have finished, and the clock that its
/// waits count, which its owner runs once a tick, and stops when it is done with it, which unwinds whatever waits on
/// it.
/// </summary>
public sealed class TickScheduler : ITick
{
    private readonly Queue<Action> _queue = new();

    private readonly List<(TickTask Task, double Due)> _waits = [];

    // The time that has passed in the ticks this scheduler has run.
    private double _time;

    // The dt passed to the last call to Run.
    private float _dt;

    private bool _running;

    private bool _stopped;

    /// <summary>Adds what waits for a task that has finished, to run when the scheduler next runs.</summary>
    internal void Add(Action continuation) => _queue.Enqueue(continuation);

    /// <inheritdoc/>
    public TickTask NextTick() => Wait(0);

    /// <inheritdoc/>
    public TickTask Wait(float seconds)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(seconds);
        var task = new TickTask(this);
        if (_stopped)
        {
            // Such as by a finally block that waits, as a stop unwinds it: it would never finish.
            task.Cancel(Cancellation());
        }
        else
        {
            _waits.Add((task, _time + seconds));
        }
        return task;
    }

    /// <inheritdoc/>
    public async TickTask While(Func<bool> condition)
    {
        while (condition())
        {
            await NextTick();
        }
    }

    /// <inheritdoc/>
    public async TickTask While(Func<bool> condition, Action body)
    {
        while (condition())
        {
            body();
            await NextTick();
        }
    }

    /// <inheritdoc/>
    public async TickTask While(Func<bool> condition, Func<TickTask> body)
    {
        while (condition())
        {
            double time = _time;
            await body();
            if (_time == time)
            {
                await NextTick();
            }
        }
    }

    /// <inheritdoc/>
    public async TickTask UntilCompleted(Task task)
    {
        await While(() => !task.IsCompleted);
        // Not await: the method would resume off the tick. The task has completed, so this only throws its exception,
        // if it has one, unwrapped, as await would.
        task.GetAwaiter().GetResult();
    }

    /// <inheritdoc/>
    public async TickTask All(IEnumerable<TickTask> tasks)
    {
        // Enumerated first, so that every task has started before any is awaited.
        List<TickTask> taskList = [.. tasks];
        foreach (var task in taskList)
        {
            await task;
        }
    }

    /// <inheritdoc/>
    public float Dt => _dt;

    /// <inheritdoc/>
    public double Time => _time;

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
            foreach ((TickTask task, double _) in _waits)
            {
                task.Cancel(Cancellation());
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

    private static OperationCanceledException Cancellation() => new("The TickScheduler was stopped.");

    /// <summary>
    /// Runs one tick: finishes the waits that are due, then runs what waits for every task that has finished, until
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
