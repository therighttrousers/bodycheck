namespace BodyCheck.Core.Tasks;

/// <summary>What a request to run work does while a run of it is in progress, or after one has finished.</summary>
public enum Reentry
{
    /// <summary>Every request starts a run of its own, alongside any in progress.</summary>
    Alongside,

    /// <summary>
    /// A request while a run is in progress starts nothing, and gets that run, which it finishes with. One made when
    /// none is in progress starts a run.
    /// </summary>
    Join,

    /// <summary>
    /// Only the first request starts a run, and every request gets that run: one made while it runs finishes when it
    /// does, and one made after finishes at once.
    /// </summary>
    Once,
}

/// <summary>
/// Work that may be asked to run again while it runs, which starts runs of it as its <see cref="Reentry"/> says, so
/// that what responds to requests says in one line what a second request does.
/// </summary>
/// <param name="reentry">What a request does while a run is in progress, or after one.</param>
/// <param name="start">Starts a run of the work on a tick.</param>
public sealed class ReentrantTask(Reentry reentry, Func<ITick, TickTask> start)
{
    // The last run started, for Join and Once.
    private TickTask? _run;

    /// <summary>A run of the work, started or not as the reentry policy says.</summary>
    public TickTask Run(ITick tick) => reentry switch
    {
        Reentry.Alongside => start(tick),
        Reentry.Join => _run is { IsFinished: false } ? _run : _run = start(tick),
        Reentry.Once => _run ??= start(tick),
        _ => throw new ArgumentOutOfRangeException(nameof(reentry), reentry, null),
    };
}
