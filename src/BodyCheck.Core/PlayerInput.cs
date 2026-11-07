namespace BodyCheck.Core;

/// <summary>What the player is pressing, as the game reads it each tick.</summary>
/// <param name="XAxis">Horizontal input, from -1 (left) to 1 (right).</param>
/// <param name="YAxis">Vertical input, from -1 (down) to 1 (up).</param>
public readonly record struct PlayerInput(
    float XAxis = 0,
    float YAxis = 0,
    bool JumpHeld = false,
    bool PhaseHeld = false,
    bool SpawnHeld = false)
{
    /// <summary>Whether interact is held: up, which only looks once it has been held a while.</summary>
    public bool InteractHeld => YAxis > 0;

    public bool CanLook => YAxis != 0 && XAxis == 0 && !JumpHeld && !PhaseHeld && !SpawnHeld;
}
