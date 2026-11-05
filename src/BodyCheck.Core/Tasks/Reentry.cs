namespace BodyCheck.Core.Tasks;

/// <summary>
/// What a request to run work does while a run of it is in progress, or after one has finished. Work that runs every
/// request alongside any in progress needs no policy: it just starts a run.
/// </summary>
public enum Reentry
{
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

    /// <summary>
    /// A request while a run is in progress starts a run once the last run asked for has finished, so that runs follow
    /// one another in the order they were asked for, and gets that run. One made when none is in progress starts a run.
    /// </summary>
    Queue,
}

/// <summary>
/// Runs of work that may be asked for while one is in progress, started as its <see cref="Reentry"/> says, so that
/// what responds to requests says in one line what a second request does.
/// <para>
/// One can be shared by several kinds of request, such as two actions, which then count as runs of the same work: with
/// <see cref="Reentry.Join"/>, a request for one while the other runs gets the other's run, and starts nothing.
/// </para>
/// </summary>
/// <param name="reentry">What a request does while a run is in progress, or after one.</param>
public sealed class ReentrantTask(Reentry reentry)
{
    // The last run asked for.
    private TickTask? _run;

    /// <summary>A run of the work, started or not as the reentry policy says.</summary>
    /// <param name="start">Starts a run of the work, if the policy says to.</param>
    public TickTask Run(Func<TickTask> start) => reentry switch
    {
        Reentry.Join => _run is { IsFinished: false } ? _run : _run = start(),
        Reentry.Once => _run ??= start(),
        Reentry.Queue => _run = _run is { IsFinished: false } last ? After(last, start) : start(),
    };

    /// <summary>A run that starts once <paramref name="last"/> has finished.</summary>
    private static async TickTask After(TickTask last, Func<TickTask> start)
    {
        await last;
        await start();
    }
}
