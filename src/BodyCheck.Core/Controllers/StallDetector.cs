namespace BodyCheck.Core.Controllers;

/// <summary>
/// Tells when an entity driving toward a goal has stalled: when its speed toward the goal has stayed too low for too
/// long, such as when something is in its way.
/// </summary>
internal sealed class StallDetector
{
    private float _slowTime;

    /// <summary>Starts over, as when the goal changes.</summary>
    public void Reset() => _slowTime = 0;

    /// <summary>
    /// Takes the entity's speed toward its goal, after <paramref name="dt"/> more game time, and says whether it has
    /// been below <paramref name="minSpeed"/> for at least <paramref name="maxTime"/>.
    /// </summary>
    public bool Update(float speed, float dt, float minSpeed, float maxTime)
    {
        _slowTime = speed < minSpeed ? _slowTime + dt : 0;
        return _slowTime >= maxTime;
    }
}
