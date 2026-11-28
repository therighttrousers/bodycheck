namespace BodyCheck.Core.Scripts;

/// <summary>
/// What one hold of a cutscene asks of the game. The world joins the requests of all current holds with
/// <see cref="Join"/>, and the game presents the cutscene as the joined request says. The default asks for nothing
/// beyond the cutscene itself, as joining no requests gives.
/// </summary>
/// <param name="BlackBars">
/// Whether to show black bars at the top and bottom of the screen, to tell the player that their control has been taken
/// away.
/// </param>
public sealed record CutsceneRequest(bool BlackBars = false)
{
    /// <summary>
    /// Combines this request with <paramref name="other"/>, each field by its own rule. A flag that any hold may turn
    /// on is combined with "or", as <see cref="BlackBars"/> is. A new field needs its rule here.
    /// </summary>
    /// <param name="other">The request to combine with this one.</param>
    public CutsceneRequest Join(CutsceneRequest other) => new(BlackBars: BlackBars || other.BlackBars);

    /// <summary>
    /// Combines the requests into one, as <see cref="Join"/> does. Returns a request with no effects when there are
    /// none.
    /// </summary>
    public static CutsceneRequest JoinAll(IEnumerable<CutsceneRequest> requests) =>
        requests.Aggregate(new CutsceneRequest(), (joined, request) => joined.Join(request));
}
