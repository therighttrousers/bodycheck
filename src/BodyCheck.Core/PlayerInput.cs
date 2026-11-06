namespace BodyCheck.Core;

/// <summary>What the player is pressing, as the game reads it each tick.</summary>
/// <param name="XAxis">Horizontal input, from -1 (left) to 1 (right).</param>
/// <param name="YAxis">Vertical input, from -1 (down) to 1 (up).</param>
public readonly record struct PlayerInput(
    float XAxis,
    float YAxis,
    bool JumpHeld,
    bool PhaseHeld = false,
    bool SpawnHeld = false)
{
    public bool CanLook => YAxis != 0 && XAxis == 0 && !JumpHeld && !PhaseHeld && !SpawnHeld;
}
