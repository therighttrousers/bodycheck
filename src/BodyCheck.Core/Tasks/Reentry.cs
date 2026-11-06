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
/// <para>
/// A request made while a run is being started, such as by the work asking for more of itself, throws. No policy could
/// serve it: with <see cref="Reentry.Queue"/> the new run would wait for the one that waits for it, and the others would
/// lose track of the run in progress. Throwing every time, rather than only when another run is in progress, makes
/// the mistake show the first time it is made. Work that shares one <c>ReentrantTask</c> calls the work itself, not
/// the requests that go through it: a toggle starts the work of opening, not a request to open.
/// </para>
/// </summary>
/// <param name="reentry">What a request does while a run is in progress, or after one.</param>
public sealed class ReentrantTask(Reentry reentry)
{
    // The last run asked for.
    private TickTask? _run;

    // Whether a run is being started: whether `start` is running, up to its first wait.
    private bool _starting;

    /// <summary>A run of the work, started or not as the reentry policy says.</summary>
    /// <param name="start">Starts a run of the work, if the policy says to.</param>
    /// <exception cref="InvalidOperationException">If a run is being started.</exception>
    public TickTask Run(Func<TickTask> start)
    {
        if (_starting)
        {
            throw new InvalidOperationException(
                "A ReentrantTask was asked for a run while starting one, which no policy can serve. Call the work itself.");
        }
        return reentry switch
        {
            Reentry.Join => _run is { IsFinished: false } ? _run : _run = Start(start),
            Reentry.Once => _run ??= Start(start),
            Reentry.Queue => _run = _run is { IsFinished: false } last ? After(last, start) : Start(start),
        };
    }

    /// <summary>Starts a run, marking it as being started until <paramref name="start"/> returns.</summary>
    private TickTask Start(Func<TickTask> start)
    {
        _starting = true;
        try
        {
            return start();
        }
        finally
        {
            _starting = false;
        }
    }

    /// <summary>A run that starts once <paramref name="last"/> has finished.</summary>
    private async TickTask After(TickTask last, Func<TickTask> start)
    {
        await last;
        await Start(start);
    }
}
