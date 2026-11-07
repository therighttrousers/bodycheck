namespace BodyCheck.Core.Tasks;

/// <summary>
/// A tick, as a task sees it: what to await to let ticks pass. Whatever starts a task gives it the tick it is to run
/// on, so that what it waits on resumes when that tick's scheduler runs.
/// </summary>
public interface ITick
{
    /// <summary>
    /// A task that finishes when the scheduler next runs: in this tick's run if made before it, and in the next tick's
    /// if made while the scheduler runs, such as by a task it resumed.
    /// </summary>
    TickTask NextTick();

    /// <summary>
    /// A task that finishes once <paramref name="seconds"/> have passed on the scheduler's clock, which counts the time
    /// of the ticks it runs, from the end of the last one: in the run whose tick ends nearest, so that rounding never
    /// adds a tick, but never in the run it is made in. So waiting for no time finishes in the scheduler's next run,
    /// which for a task it resumed is the next tick's, and a loop that waits can't spin within a tick.
    /// </summary>
    TickTask Wait(float seconds);

    /// <summary>
    /// A task that finishes in the first of the scheduler's runs in which <paramref name="condition"/> holds, checking
    /// it in each, or at once if it holds now: the way to wait for what changes off the tick, such as with the player's
    /// input to a menu.
    /// </summary>
    TickTask Until(Func<bool> condition);

    /// <summary>
    /// A task that finishes in the first of the scheduler's runs after <paramref name="task"/> has completed, or at once
    /// if it has: the way to wait for a .NET <c>Task</c>, which can't be awaited on the tick. It fails with the task's
    /// exception, if it has one, or is cancelled, if it was. A result, if the task has one, is then read from the task.
    /// </summary>
    TickTask Until(Task task);

    /// <summary>
    /// The length of the tick being run, or of the last one run, on the clock that waits count.
    /// </summary>
    float Dt { get; }
}
