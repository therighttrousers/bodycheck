namespace BodyCheck.Core;

/// <summary>
/// What waits for <see cref="ActionTask"/>s that have finished, which the world runs at a fixed point in each played
/// tick, and the world's clock for waiting on game time. A world has one, and rebuilding the world drops it, with
/// whatever waits on it.
/// </summary>
public sealed class ActionTaskQueue : ITick
{
    private readonly Queue<Action> _queue = new();

    private readonly List<(ActionTask Task, double Due)> _waits = [];

    // The game time that has passed in the ticks this queue has run.
    private double _gameTime;

    private bool _running;

    /// <summary>Adds what waits for a task that has finished, to run when the queue next runs.</summary>
    internal void Add(Action continuation) => _queue.Enqueue(continuation);

    /// <inheritdoc/>
    public ActionTask NextTick() => Wait(0);

    /// <inheritdoc/>
    /// <remarks>The played ticks are those the queue runs.</remarks>
    public ActionTask Wait(float gameSeconds)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(gameSeconds);
        var task = new ActionTask(this);
        _waits.Add((task, _gameTime + gameSeconds));
        return task;
    }

    /// <summary>
    /// Runs one played tick: finishes the waits that are due, then runs what waits for every task that has finished,
    /// until nothing is left, so that a chain of actions that finish at once runs to its end in one tick.
    /// <para>
    /// If anything it runs throws, or a task has failed, it forgets everything queued and throws, failing the tick.
    /// </para>
    /// </summary>
    /// <param name="gameDt">The tick's length in game time.</param>
    public void Run(float gameDt)
    {
        if (_running)
        {
            throw new InvalidOperationException("The ActionTaskQueue is already running.");
        }
        _running = true;
        try
        {
            _gameTime += gameDt;
            FinishDueWaits(gameDt);
            while (_queue.TryDequeue(out Action? continuation))
            {
                continuation();
            }
        }
        catch
        {
            // We can't count on the world being in the expected state for what is queued, so don't run it.
            _queue.Clear();
            throw;
        }
        finally
        {
            _running = false;
        }
    }

    /// <summary>Finishes the waits due by the end of a tick of <paramref name="gameDt"/>, in the order they were made.</summary>
    private void FinishDueWaits(float gameDt)
    {
        // A wait is due in the tick that ends nearest it: the first that ends no more than half a tick before it.
        double dueBy = _gameTime + gameDt / 2;
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
