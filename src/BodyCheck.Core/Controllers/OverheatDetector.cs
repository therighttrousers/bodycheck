namespace BodyCheck.Core.Controllers;

/// <summary>
/// Tells when something an entity does has happened too often: each time adds heat, which time sheds at a steady rate,
/// and the entity overheats when its heat reaches a maximum. It stays overheated until its heat is back to 0, not only
/// below the maximum, so that it rests for a while instead of starting again at once.
/// </summary>
internal sealed class OverheatDetector
{
    private float _heat;

    /// <summary>Whether the heat has reached the maximum, and not yet fallen back to 0.</summary>
    public bool Overheated { get; private set; }

    /// <summary>
    /// Adds <paramref name="amount"/> of heat, up to <paramref name="maxHeat"/>, and overheats on reaching it. While
    /// overheated, it adds nothing.
    /// </summary>
    public void HeatUp(float amount, float maxHeat)
    {
        if (Overheated) return;
        _heat = Math.Min(maxHeat, _heat + amount);
        Overheated = _heat == maxHeat;
    }

    /// <summary>Sheds <paramref name="amount"/> of heat, down to 0, where it stops being overheated.</summary>
    public void CoolDown(float amount)
    {
        _heat = Math.Max(0, _heat - amount);
        if (_heat == 0)
        {
            Overheated = false;
        }
    }
}
