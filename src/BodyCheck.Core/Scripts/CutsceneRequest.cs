namespace BodyCheck.Core.Scripts;

/// <summary>
/// What a hold on a cutscene asks of it, beyond the cutscene itself: how the game is to present it. The world joins
/// the requests of every hold held into one, with <see cref="Join"/>, which the game presents. The default asks for
/// nothing more, and is the join of no requests.
/// </summary>
/// <param name="BlackBars">
/// Whether to show black bars across the top and bottom of the view, which say that the player's control is taken
/// away. Shown if any hold asks.
/// </param>
public sealed record CutsceneRequest(bool BlackBars = false)
{
    /// <summary>
    /// The request that asks for everything either asks for: each field joined by its own rule, such as "or" for a
    /// flag that any hold may turn on.
    /// </summary>
    public CutsceneRequest Join(CutsceneRequest other) => new(BlackBars: BlackBars || other.BlackBars);

    /// <summary>The join of <paramref name="requests"/>, which is the default for none.</summary>
    public static CutsceneRequest JoinAll(IEnumerable<CutsceneRequest> requests) =>
        requests.Aggregate(new CutsceneRequest(), (joined, request) => joined.Join(request));
}
