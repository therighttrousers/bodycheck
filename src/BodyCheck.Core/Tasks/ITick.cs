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
    /// A task that loops while <paramref name="condition"/> holds, checking it once in each of the scheduler's runs, and
    /// finishes in the first in which it doesn't, or at once if it doesn't hold now: "do this while that". It is the way
    /// to wait for what changes in the world, or off the tick, such as the player's input to a menu.
    /// </summary>
    /// <param name="condition">What the loop goes on while.</param>
    TickTask While(Func<bool> condition);

    /// <inheritdoc cref="While(Func{bool})"/>
    /// <param name="body">What the loop does each time the condition holds, before it waits for the next run.</param>
    TickTask While(Func<bool> condition, Action body);

    /// <inheritdoc cref="While(Func{bool})"/>
    /// <param name="body">
    /// What the loop does each time the condition holds, before it waits for the next run. A body that returns a task
    /// is awaited; if it lets no time pass, the loop then waits for the next run, so that it can't spin within a run.
    /// </param>
    TickTask While(Func<bool> condition, Func<TickTask> body);

    /// <summary>
    /// A task that finishes in the first of the scheduler's runs after <paramref name="task"/> has completed, by
    /// finishing, failing or being cancelled, or at once if it has: the way to wait for a .NET <c>Task</c>, which can't
    /// be awaited on the tick. It fails with the task's exception, if it has one, or is cancelled, if the task was. A
    /// result, if the task has one, is then read from the task.
    /// </summary>
    TickTask UntilCompleted(Task task);

    /// <summary>
    /// A task that finishes when every one of <paramref name="tasks"/> has. It enumerates them at once, so that a
    /// sequence that starts them as it goes, such as a <c>Select</c> of calls, starts every one before any is awaited.
    /// If any of them fails or is cancelled, so does it, as the first in their order that did.
    /// </summary>
    TickTask All(IEnumerable<TickTask> tasks);

    /// <summary>
    /// The length of the tick being run, or of the last one run, on the clock that waits count.
    /// </summary>
    float Dt { get; }

    /// <summary>
    /// The time on the clock that waits count: the total length of the ticks the scheduler has run, counting the one
    /// being run, so that a script can set itself a deadline, such as <c>Time + 2</c>.
    /// </summary>
    double Time { get; }
}
