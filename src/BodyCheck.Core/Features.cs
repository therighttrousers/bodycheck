namespace BodyCheck.Core;

/// <summary>What momentum a phase gives the player and the entity phased through.</summary>
public enum PhaseThrow
{
    /// <summary>
    /// Replaces the momentum of each with equal and opposite momentum, along the phase's direction. Every phase leaves
    /// them moving the same, however they were moving before, which makes juggling easy and predictable. Momentum is
    /// not conserved.
    /// </summary>
    Fixed,

    /// <summary>
    /// Adds equal and opposite momentum to each, along the phase's direction, so momentum is conserved. Where they end
    /// up moving depends on how they were moving: phases in a row can build speed, and to juggle a robot high, the
    /// player has to meet it just before the top of its rise.
    /// </summary>
    Conserved,
}

/// <summary>
/// The values that shape gameplay, in one place: the only source of each (decision record 016). Read them from
/// <see cref="Current"/> where they are used. A feature must be a simple value that nothing derives from or caches.
/// Changes are eventually consistent: what reads a feature sees the change at once, except an action in progress, such
/// as a jump, which goes on with the values it started with.
/// </summary>
public sealed record Features
{
    /// <summary>The features in effect. Their defaults until <see cref="Set"/> or <see cref="Reset"/> is called.</summary>
    public static Features Current { get; private set; } = new();

    /// <summary>
    /// Raised after <see cref="Current"/> changes. Try not to need it: read features where they are used, instead of
    /// deriving from them or caching them, so that there is nothing to bring up to date.
    /// </summary>
    public static event Action? Changed;

    /// <summary>Puts the given features in effect.</summary>
    public static void Set(Features features)
    {
        Current = features;
        Changed?.Invoke();
    }

    /// <summary>Puts every feature back to its default.</summary>
    public static void Reset() => Set(new());

    // The player.

    /// <summary>Speed at which the player's run drive force falls to zero.</summary>
    public float PlayerTopSpeed { get; init; } = 12;

    /// <summary>Force applied when on the ground and moving in the direction of input.</summary>
    public float PlayerGroundForwardDriveForce { get; init; } = 67.5f;
    /// <summary>Force applied when on the ground and moving against the direction of input.</summary>
    public float PlayerGroundReverseDriveForce { get; init; } = 135;
    /// <summary>Force applied when in the air and moving in the direction of input.</summary>
    public float PlayerAirForwardDriveForce { get; init; } = 22.5f;
    /// <summary>Force applied when in the air and moving against the direction of input.</summary>
    public float PlayerAirReverseDriveForce { get; init; } = 45;

    /// <summary>
    /// The player's coefficient of friction with the ground, whether tiles or another body. Applied when not driving in
    /// the direction of travel.
    /// </summary>
    public float PlayerGroundFriction { get; init; } = 4;

    /// <summary>Upward speed given by the jump impulse.</summary>
    public float PlayerJumpSpeed { get; init; } = 12.7f;

    /// <summary>
    /// Number of frames the jump thrust lasts while the button is held. Includes the frame with the initial jump impulse.
    /// </summary>
    public int PlayerJumpThrustTicks { get; init; } = 14;

    /// <summary>Jump thrust as a fraction of the player's weight. At 1 it cancels gravity.</summary>
    public float PlayerJumpThrustFrac { get; init; } = 0.9f;

    /// <summary>
    /// Number of frames the player gets to jump, or turn around with ground thrust, *after* running off an edge.
    /// </summary>
    public int PlayerCoyoteTicks { get; init; } = 4;

    /// <summary>
    /// How far the box of the entity phased through may be beyond a sphere around the player's center that just
    /// contains the player's box, whose radius is half the player's diagonal. At least 0, so that everything touching
    /// the player is in reach.
    /// </summary>
    public float PlayerPhaseReach { get; init; } = 0.165f;

    /// <summary>
    /// How far from the player's center, in the direction held, to measure from when choosing among the entities in
    /// reach: the target is the one whose box is nearest that point. Holding toward an entity favors it.
    /// </summary>
    public float PlayerPhaseInputOffset { get; init; } = 0.5f;

    /// <summary>
    /// How far apart the player's box and the box of the entity phased through end up, along the phase's direction.
    /// </summary>
    public float PlayerPhaseGap { get; init; } = 0.5f;

    /// <summary>
    /// The speed of the momentum a phase gives the player: the speed it leaves with, under <see cref="PhaseThrow.Fixed"/>,
    /// or what is added to how it moves, under <see cref="PhaseThrow.Conserved"/>.
    /// </summary>
    public float PlayerPhaseSpeed { get; init; } = 12.7f;

    /// <summary>The minimum time between phase uses, according to the game clock.</summary>
    public float PlayerPhaseCooldownTime { get; init; } = 0.5f;

    /// <summary>What momentum a phase gives the player and the target. To be chosen by playing.</summary>
    public PhaseThrow PlayerPhaseThrow { get; init; } = PhaseThrow.Fixed;

    // Robots.

    /// <summary>Speed at which a robot's run drive force falls to zero.</summary>
    public float RobotTopSpeed { get; init; } = 3;

    /// <summary>Force applied to a robot when on the ground and moving in the direction it patrols.</summary>
    public float RobotForwardDriveForce { get; init; } = 11.25f;
    /// <summary>Force applied to a robot when on the ground and moving against the direction it patrols.</summary>
    public float RobotReverseDriveForce { get; init; } = 22.5f;

    /// <summary>
    /// A robot's coefficient of friction with the ground, whether tiles or another body. Applied when not driving in
    /// the direction of travel.
    /// </summary>
    public float RobotGroundFriction { get; init; } = 4;

    // Platforms.

    /// <summary>Speed at which a platform's horizontal drive force falls to zero.</summary>
    public float PlatformTopSpeed { get; init; } = 6;

    /// <summary>Force applied to a platform horizontally to move back and forth.</summary>
    public float PlatformHorizontalDriveForce { get; init; } = 67.5f;

    /// <summary>
    /// How much a platform thrusts vertically to get back to its original altitude. Higher -> less sag.
    /// </summary>
    public float PlatformVerticalStiffness { get; init; } = 112.5f;
    /// <summary>A platform's maximum vertical thrust, as a fraction of its weight. Should be > 1.</summary>
    public float PlatformMaxVerticalThrustFrac { get; init; } = 1.5f;
}
