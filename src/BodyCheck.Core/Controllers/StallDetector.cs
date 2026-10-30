namespace BodyCheck.Core.Controllers;

/// <summary>
/// How a <see cref="StallDetector"/> is tuned. Each value is a function that reads it from the features in effect when it
/// is used, so that a change to them takes effect at once.
/// </summary>
public sealed record StallTuning
{
    /// <summary>The speed toward the goal below which the entity is slow.</summary>
    public required Func<float> MinSpeed { get; init; }
    /// <summary>How long, in game time, the entity must be slow to stall.</summary>
    public required Func<float> MaxTime { get; init; }
}

/// <summary>
/// Tells when an entity driving toward a goal has stalled: when its speed toward the goal has stayed too low for too
/// long, such as when something is in its way.
/// </summary>
/// <param name="tuning">How the detector is tuned.</param>
internal sealed class StallDetector(StallTuning tuning)
{
    private float _slowTime;

    /// <summary>Starts over, as when the goal changes.</summary>
    public void Reset() => _slowTime = 0;

    /// <summary>
    /// Takes the entity's speed toward its goal, after <paramref name="gameDt"/> more game time, and says whether it has
    /// been slow for long enough to stall.
    /// </summary>
    public bool Update(float speed, float gameDt)
    {
        _slowTime = speed < tuning.MinSpeed() ? _slowTime + gameDt : 0;
        return _slowTime >= tuning.MaxTime();
    }
}
