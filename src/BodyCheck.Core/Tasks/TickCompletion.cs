namespace BodyCheck.Core.Tasks;

/// <summary>
/// A <see cref="TickTask"/> that something off the tick finishes, such as a callback from the engine: the way to wait
/// for what doesn't run on a tick. Made by <see cref="ITick.NewCompletion"/>, so that its scheduler still makes the
/// task. Finishing or failing it only queues what waits for its task, which resumes when the scheduler next runs.
/// </summary>
public sealed class TickCompletion
{
    internal TickCompletion(TickScheduler scheduler) => Task = new TickTask(scheduler);

    /// <summary>The task that finishes when this is finished.</summary>
    public TickTask Task { get; }

    /// <summary>Finishes <see cref="Task"/>. It finishes only once.</summary>
    public void Finish() => Task.Finish();

    /// <summary>Fails <see cref="Task"/>, which fails the scheduler's next run. It finishes only once.</summary>
    public void Fail(Exception exception) => Task.Fail(exception);
}
