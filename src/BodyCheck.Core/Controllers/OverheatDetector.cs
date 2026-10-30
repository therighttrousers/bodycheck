namespace BodyCheck.Core.Controllers;

/// <summary>
/// How an <see cref="OverheatDetector"/> is tuned. Each value is a function that reads it from the features in effect
/// when it is used, so that a change to them takes effect at once.
/// </summary>
public sealed record OverheatTuning
{
    /// <summary>How much heat each event adds, such as each stall.</summary>
    public required Func<float> HeatPerEvent { get; init; }
    /// <summary>The heat at which the entity overheats.</summary>
    public required Func<float> MaxHeat { get; init; }
    /// <summary>How quickly heat is shed, in heat per game-time second.</summary>
    public required Func<float> CooldownRate { get; init; }
}

/// <summary>
/// Tells when something an entity does has happened too often: each event adds heat, which time sheds at a steady rate,
/// and the entity overheats when its heat reaches a maximum. It stays overheated until its heat is back to 0, not only
/// below the maximum, so that it rests for a while instead of starting again at once.
/// </summary>
/// <param name="tuning">How the detector is tuned.</param>
internal sealed class OverheatDetector(OverheatTuning tuning)
{
    private float _heat;

    /// <summary>Whether the heat has reached the maximum, and not yet fallen back to 0.</summary>
    public bool Overheated { get; private set; }

    /// <summary>Adds an event's heat, up to the maximum, and overheats on reaching it. While overheated, it adds nothing.</summary>
    public void HeatUp()
    {
        if (Overheated) return;
        float maxHeat = tuning.MaxHeat();
        _heat = Math.Min(maxHeat, _heat + tuning.HeatPerEvent());
        Overheated = _heat == maxHeat;
    }

    /// <summary>Sheds heat for <paramref name="gameDt"/> of game time, down to 0, where it stops being overheated.</summary>
    public void CoolDown(float gameDt)
    {
        _heat = Math.Max(0, _heat - tuning.CooldownRate() * gameDt);
        if (_heat == 0)
        {
            Overheated = false;
        }
    }
}
