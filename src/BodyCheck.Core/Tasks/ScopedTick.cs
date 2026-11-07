namespace BodyCheck.Core.Tasks;

/// <summary>
/// A <see cref="TickScheduler"/>'s tick, as its tasks see it, whose waits are cancelled once its scope ends: once
/// <paramref name="condition"/> no longer holds, or never, if there is none.
/// </summary>
internal sealed class ScopedTick(TickScheduler scheduler, Func<bool>? condition) : ITick
{
    /// <inheritdoc/>
    public TickTask NextTick() => Wait(0);

    /// <inheritdoc/>
    public TickTask Wait(float seconds) => scheduler.Wait(seconds, condition);

    /// <inheritdoc/>
    public ITick Scoped(Func<bool> inner) =>
        // The outer condition first, so that it can guard the inner one, such as that an entity is still there.
        new ScopedTick(scheduler, condition is null ? inner : () => condition() && inner());

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
            double time = Time;
            await body();
            if (Time == time)
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
    public float Dt => scheduler.Dt;

    /// <inheritdoc/>
    public double Time => scheduler.Time;
}
