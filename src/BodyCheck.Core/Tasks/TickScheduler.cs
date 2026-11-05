namespace BodyCheck.Core.Tasks;

/// <summary>
/// What runs <see cref="TickTask"/>s: the queue of what waits for tasks that have finished, and the clock that its
/// waits count, which its owner runs once a tick. Dropping it drops whatever waits on it.
/// </summary>
public sealed class TickScheduler : ITick
{
    private readonly Queue<Action> _queue = new();

    private readonly List<(TickTask Task, double Due)> _waits = [];

    // The time that has passed in the ticks this scheduler has run.
    private double _time;

    private bool _running;

    /// <summary>Adds what waits for a task that has finished, to run when the scheduler next runs.</summary>
    internal void Add(Action continuation) => _queue.Enqueue(continuation);

    /// <inheritdoc/>
    public TickTask NextTick() => Wait(0);

    /// <inheritdoc/>
    public TickTask Wait(float seconds)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(seconds);
        var task = new TickTask(this);
        _waits.Add((task, _time + seconds));
        return task;
    }

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
        _running = true;
        try
        {
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
