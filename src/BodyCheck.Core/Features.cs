using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace BodyCheck.Core;

/// <summary>What momentum a phase gives the player and the entity phased through.</summary>
[JsonConverter(typeof(StrictEnumConverter<PhaseThrow>))]
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

/// <summary>How the player's block moves when it is spawned under the player.</summary>
[JsonConverter(typeof(StrictEnumConverter<SpawnMotion>))]
public enum SpawnMotion
{
    /// <summary>It is still, so a player in the air lands on it, and pushes it down.</summary>
    AtRest,

    /// <summary>It moves as the player does, so a player in the air stands on it at once, and can jump off it.</summary>
    WithPlayer,
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
    /// deriving from them or caching them, so that there is nothing to bring up to date. What must copy them into the
    /// engine may listen, as the world does (029 §2.5), but a node should listen only while it is in the tree, so that
    /// the event doesn't keep it alive.
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

    private static readonly JsonDocumentOptions ReadOptions =
        new() { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true };

    /// <summary>
    /// Reads a configuration: a JSON object that names features by their properties' names and gives their values. A
    /// feature it doesn't name keeps its default (decision record 017). Comments and trailing commas are allowed, so
    /// that a file is easy to edit by hand.
    /// </summary>
    /// <param name="path">The configuration's path, for errors.</param>
    /// <exception cref="InvalidDataException">
    /// When the text isn't a configuration, such as when it names something that isn't a feature or gives a value of
    /// the wrong type, naming the path and what is wrong.
    /// </exception>
    public static Features Read(string path, string json)
    {
        try
        {
            // Reading checks the text, but the source generator's code gives a feature the text doesn't name 0, not its
            // default. So the features it names are laid over the defaults, and the whole is read again.
            _ = JsonSerializer.Deserialize(json, FeaturesJsonContext.Default.Features)
                ?? throw new InvalidDataException($"{path}: The file holds null.");
            JsonObject features = JsonSerializer.SerializeToNode(new Features(), FeaturesJsonContext.Default.Features)!
                .AsObject();
            foreach ((string name, JsonNode? value) in JsonNode.Parse(json, documentOptions: ReadOptions)!.AsObject())
            {
                features[name] = value?.DeepClone();
            }
            return features.Deserialize(FeaturesJsonContext.Default.Features)!;
        }
        catch (JsonException e)
        {
            throw new InvalidDataException($"{path}: {e.Message}", e);
        }
    }

    #region Tuning
    // Durations on game time are given in game seconds, since a tick's length in game time changes with the speed.
    // Durations on real time are given in ticks.

    /// <summary>The tick rate that features given in ticks count at: the real time of a physics tick.</summary>
    public const int TuningTicksPerSecond = 60;

    /// <summary>
    /// A length of real time given in ticks at <see cref="TuningTicksPerSecond"/>, less half a tick: a timer set to it
    /// and counted down by ticks' real lengths stays above zero for exactly that many ticks, with half a tick to spare
    /// either way however the lengths round.
    /// </summary>
    public static float TicksToTime(int ticks) => (ticks - 0.5f) / TuningTicksPerSecond;
    #endregion

    #region World
    /// <summary>
    /// How fast the game plays: the engine's time scale while playing, outside the slowdown to a phase's pause. The
    /// other features are tuned at speed 1, and every one on game time scales with it, while those on real time, such
    /// as the leniencies for the player's fingers, don't (see docs/designs/architecture.md). From 0.5 to 1.
    /// </summary>
    public float GameplaySpeed { get; init; } = 1;

    /// <summary>
    /// The default friction for static tiles. Walkers have no engine friction, and the smaller of two coefficients
    /// wins, so this only affects bodies that have some.
    /// </summary>
    public float TileDefaultFriction { get; init; } = 0.5f;
    #endregion

    #region Player
    /// <summary>The player's mass.</summary>
    public float PlayerMass { get; init; } = 1;

    /// <summary>Speed at which the player's run drive force falls to zero.</summary>
    public float PlayerTopSpeed { get; init; } = 12;

    /// <summary>Force applied when on the ground and input direction is the same as the direction of motion.</summary>
    public float PlayerGroundDriveForce { get; init; } = 67.5f;
    /// <summary>Force applied when on the ground and input direction is against the direction of motion.</summary>
    public float PlayerGroundBrakeForce { get; init; } = 135;
    /// <summary>Force applied when in the air and input direction is the same as the direction of motion.</summary>
    public float PlayerAirDriveForce { get; init; } = 22.5f;
    /// <summary>Force applied when in the air and input direction is against the direction of motion.</summary>
    public float PlayerAirBrakeForce { get; init; } = 45;

    /// <summary>
    /// <para>
    /// The player's coefficient of friction, rough, while it stands on something and doesn't slip: with the ground,
    /// whether tiles or another body, and with what it carries. Applied when not driving in the direction of travel.
    /// </para>
    /// <para>
    /// <b>This should be less than 3.</b> If it's too high, the sudden change from 0 friction while driving
    /// to this friction while braking can cause the player to stop dead in its tracks and stay still for ~1 second.
    /// It's probably due to a limitation or a flaw in Jolt 3D.
    /// </para>
    /// </summary>
    public float PlayerGroundFriction { get; init; } = 2.5f;

    /// <summary>
    /// The most torque with which the player holds the rotation of what it carries to its own. Robots don't hold what
    /// they carry. A block of 1 tile tips off a body that decelerates at more than gravity unless something holds it.
    /// </summary>
    public float PlayerCarryTorque { get; init; } = 100;

    /// <summary>
    /// The most force with which the player steadies what it carries: it holds the carried body's speed at its center
    /// to its own, like a hand held up behind its head, so that the body neither slides nor tips. Robots
    /// don't steady what they carry. This should be smaller than <see cref="PlayerGroundDriveForce"/> so a 2-tile-high
    /// tunnel can knock the block off the player's head (and not stop it from going through).
    /// </summary>
    public float PlayerCarrySteadying { get; init; } = 60;

    /// <summary>Upward speed given by the jump impulse.</summary>
    public float PlayerJumpSpeed { get; init; } = 12.7f;

    /// <summary>
    /// How long the jump thrust lasts while the button is held, in game seconds, from the tick after the one with the
    /// initial jump impulse. A tick that it covers only part of gets that part of the thrust, so the thrust adds the
    /// same speed at any <see cref="GameplaySpeed"/>.
    /// </summary>
    public float PlayerJumpThrustTime { get; init; } = 0.23f;

    /// <summary>Jump thrust as a fraction of the player's weight. At 1 it cancels gravity.</summary>
    public float PlayerJumpThrustFrac { get; init; } = 0.885f;

    /// <summary>
    /// How far, in tiles, the player may be nudged sideways while rising, so that its head clears the corner of a tile
    /// it would otherwise bump. At 0, it bumps every corner it meets.
    /// </summary>
    public float PlayerUpwardCornerCorrection { get; init; } = 0.25f;

    /// <summary>
    /// How much, in degrees, a corner-correcting horizontal nudge is allowed to work against velocity. At 0, it may
    /// not, as in Celeste. At 90, a nudge may be opposite velocity (though that wouldn't help much).
    /// </summary>
    public float PlayerUpwardCornerCorrectionAngle { get; init; } = 30;

    /// <summary>
    /// How far, in tiles, the player may be nudged up or down while moving sideways, so that it clears the corner of a
    /// tile it would otherwise run into, such as the top of a step or the far side of a gap. At 0, it bumps every corner
    /// it meets.
    /// </summary>
    public float PlayerForwardCornerCorrection { get; init; } = 0.25f;

    /// <summary>
    /// How much, in degrees, a corner-correcting vertical nudge is allowed to work against velocity. At 0, it may
    /// not, as in Celeste. At 90, a nudge may be opposite velocity (though that wouldn't help much). This should be
    /// small, and should allow the player to consistently run over 1-tile gaps while carrying a block.
    /// </summary>
    public float PlayerForwardCornerCorrectionAngle { get; init; } = 10;

    /// <summary>
    /// How long the player gets to jump, or turn around with ground thrust, *after* running off an edge, in ticks at
    /// <see cref="TuningTicksPerSecond"/> of real time, so that the time to react is the same at any
    /// <see cref="GameplaySpeed"/>.
    /// </summary>
    public int PlayerCoyoteTicks { get; init; } = 4;

    /// <summary>
    /// How long a press of jump in the air still counts, in ticks at <see cref="TuningTicksPerSecond"/> of real time,
    /// counting the tick it is pressed on. Landing within it jumps. At 1, only a press on the ground jumps.
    /// </summary>
    public int PlayerJumpBufferTicks { get; init; } = 4;

    /// <summary>
    /// How far the box of the entity phased through may be beyond a sphere around the player's center that just
    /// contains the player's box, whose radius is half the player's diagonal. At least 0, so that everything touching
    /// the player is in reach.
    /// </summary>
    public float PlayerPhaseReach { get; init; } = 0.165f;

    /// <summary>
    /// How near the player's box must come to a respawn's, in tiles, for the respawn to become the one the player
    /// starts from when the world restarts.
    /// </summary>
    public float PlayerRespawnReach { get; init; } = 2;

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

    /// <summary>The minimum delay after phasing successfully, according to the game clock.</summary>
    public float PlayerPhaseThrowCooldownTime { get; init; } = 0.125f;

    /// <summary>The minimum delay after canceling phasing, according to the game clock.</summary>
    public float PlayerPhaseCancelCooldownTime { get; init; } = 0.25f;

    /// <summary>
    /// How long the last direction held while aiming a phase still counts once it is let go of, in ticks at
    /// <see cref="TuningTicksPerSecond"/> of real time, counting the tick it is let go on. Releasing phase within it
    /// phases in that direction, instead of cancelling. At 1, releasing both on the same tick phases; at 0, the
    /// direction must still be held.
    /// </summary>
    public int PlayerPhaseReleaseTicks { get; init; } = 4;

    /// <summary>What momentum a phase gives the player and the target. To be chosen by playing.</summary>
    public PhaseThrow PlayerPhaseThrow { get; init; } = PhaseThrow.Fixed;

    /// <summary>
    /// The minimum time between spawning the player's block and spawning it again, in game time: long enough that
    /// spawning it under the player again and again can't be flight.
    /// </summary>
    public float PlayerSpawnCooldownTime { get; init; } = 1.5f;

    /// <summary>The minimum delay after a spawn is cancelled for want of room, in game time.</summary>
    public float PlayerSpawnCancelCooldownTime { get; init; } = 0.25f;

    /// <summary>How the player's block moves when it is spawned. To be chosen by playing.</summary>
    public SpawnMotion PlayerSpawnMotion { get; init; } = SpawnMotion.AtRest;
    #endregion

    #region Robots
    /// <summary>A robot's mass.</summary>
    public float RobotMass { get; init; } = 1;

    /// <summary>Speed at which a robot's run drive force falls to zero.</summary>
    public float RobotTopSpeed { get; init; } = 3.75f;

    /// <summary>
    /// Force applied to a robot when on the ground and facing the direction of motion. A robot doesn't slip, so it
    /// drives against its friction with the floor, which grows with what it carries.
    /// </summary>
    public float RobotDriveForce { get; init; } = 14.0625f;
    /// <summary>Force applied to a robot when on the ground and facing opposite the direction of motion.</summary>
    public float RobotBrakeForce { get; init; } = 22.5f;

    /// <summary>
    /// A robot's coefficient of friction, against everything it touches. It is the same while the robot drives, so it
    /// must be low enough for the drive to beat it. It is also how hard the robot's head holds what it carries: a block
    /// slides on it when the robot speeds up or slows down by more than this times gravity.
    /// </summary>
    public float RobotEngineFriction { get; init; } = 0.1f;

    /// <summary>
    /// A robot whose speed toward the end it is heading for stays below this for <see cref="RobotStallTime"/> has
    /// stalled, and turns around.
    /// </summary>
    public float RobotStallSpeed { get; init; } = 0.1f;
    /// <summary>
    /// How long, in game time, a robot's speed must stay below <see cref="RobotStallSpeed"/> to stall.
    /// </summary>
    public float RobotStallTime { get; init; } = 0.25f;
    /// <summary>
    /// The amount of heat added when a robot stalls.
    /// </summary>
    public float RobotStallHeat { get; init; } = 1;
    /// <summary>
    /// The maximum amount of stall heat a robot can accumulate before overheating and pausing until its heat is back to 0.
    /// </summary>
    public float RobotStallMaxHeat { get; init; } = 5;
    /// <summary>
    /// How quickly a robot sheds stall heat, in heat per game-time second.
    /// </summary>
    public float RobotStallCooldownRate { get; init; } = 1;

    /// <summary>How far, in tiles, a robot's laser reaches, and so how far away a robot can target the player.</summary>
    public float RobotLaserRange { get; init; } = 15;
    /// <summary>
    /// How far above or below level, in degrees, a robot can target the player. The design's 90-degree arcs above and
    /// below that it can't target leave 45 degrees.
    /// </summary>
    public float RobotLaserMaxElevation { get; init; } = 45;
    /// <summary>How long, in game time, a robot's thin beam follows the player before it locks.</summary>
    public float RobotLaserAimTime { get; init; } = 1;
    /// <summary>How long, in game time, a robot's beam stays locked before it fires.</summary>
    public float RobotLaserLockTime { get; init; } = 0.75f;
    /// <summary>How long, in game time, a robot's laser fires.</summary>
    public float RobotLaserFireTime { get; init; } = 0.75f;
    /// <summary>How long, in game time, a robot waits after its laser stops before it can aim again.</summary>
    public float RobotLaserCooldownTime { get; init; } = 3;
    /// <summary>
    /// How long, in game time, a robot that is aiming may lose sight of the player before it gives up and cools down.
    /// </summary>
    public float RobotLaserLoseTime { get; init; } = 0.1f;
    /// <summary>
    /// How long, in game time, a robot whose laser is on may be off the ground before it gives up and cools down, such as
    /// when it is phased through, thrown or falls. A robot doesn't start aiming in the air.
    /// </summary>
    public float RobotLaserAirTime { get; init; } = 0.1f;
    #endregion

    #region Platforms
    /// <summary>
    /// A platform's mass. Its drive and its hold on its line are fractions of its weight, so they scale with it.
    /// </summary>
    public float PlatformMass { get; init; } = 5;
    /// <summary>
    /// A platform's coefficient of friction, with which it carries what rests on it. The smaller of two bodies'
    /// coefficients wins, unless one is rough, as a walker's is.
    /// </summary>
    public float PlatformEngineFriction { get; init; } = 0.5f;

    /// <summary>A platform's speed along its line, away from its ends.</summary>
    public float PlatformTopSpeed { get; init; } = 6;
    /// <summary>
    /// How hard a platform drives toward its target speed along its line: its acceleration, per tile per second short of
    /// that speed, until its drive is at its maximum. Higher -> holds its speed better under load.
    /// </summary>
    public float PlatformDriveGain { get; init; } = 10;
    /// <summary>A platform's maximum drive along its line, as a fraction of its weight under unscaled gravity.</summary>
    public float PlatformMaxDriveFrac { get; init; } = 0.5f;
    /// <summary>
    /// How hard a platform plans to brake as it nears the end of its line, as a fraction of its maximum drive. The
    /// rest is spare, for braking a load. Higher -> stops later and harder.
    /// </summary>
    public float PlatformBrakeFrac { get; init; } = 0.5f;

    /// <summary>
    /// The natural frequency, in radians per second, of the critically damped controller that holds a platform on its
    /// line. Being a frequency, it holds platforms of any mass alike. Higher -> less sag.
    /// </summary>
    public float PlatformHoldFrequency { get; init; } = 4.75f;
    /// <summary>
    /// A platform's maximum thrust back toward its line, as a fraction of its weight under unscaled gravity. A load
    /// heavier than this fraction of its weight sinks a platform that moves sideways.
    /// </summary>
    public float PlatformMaxHoldFrac { get; init; } = 0.5f;

    /// <summary>How near to the end of its line a platform turns around, in tiles.</summary>
    public float PlatformTurnDistance { get; init; } = 0.1f;

    /// <summary>
    /// A platform whose speed toward the end it is heading for stays below this for <see cref="PlatformStallTime"/> has
    /// stalled, and turns around. Below the least speed of its braking curve, about 1.2, so that braking isn't a stall.
    /// </summary>
    public float PlatformStallSpeed { get; init; } = 0.5f;
    /// <summary>
    /// How long, in game time, a platform's speed must stay below <see cref="PlatformStallSpeed"/> to stall. Longer than
    /// it takes to speed up again after turning around.
    /// </summary>
    public float PlatformStallTime { get; init; } = 0.25f;
    #endregion

    #region Blocks
    /// <summary>The mass of the block the player spawns, light enough to carry and jump with.</summary>
    public float PlayerBlockMass { get; init; } = 1;
    /// <summary>The coefficient of friction of the block the player spawns. See <see cref="BlockEngineFriction"/>.</summary>
    public float PlayerBlockEngineFriction { get; init; } = 0.5f;

    /// <summary>A block's mass.</summary>
    public float BlockMass { get; init; } = 2;
    /// <summary>
    /// A block's coefficient of friction, against everything it touches but what carries it. The smaller of two bodies'
    /// coefficients wins, unless one is rough, as a walker's and a carrier's are.
    /// </summary>
    public float BlockEngineFriction { get; init; } = 0.5f;
    #endregion
}

/// <summary>The code that reads configurations, which System.Text.Json's source generator writes at compile time.</summary>
[JsonSourceGenerationOptions(
    RespectNullableAnnotations = true,
    UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    ReadCommentHandling = JsonCommentHandling.Skip,
    AllowTrailingCommas = true)]
[JsonSerializable(typeof(Features))]
internal sealed partial class FeaturesJsonContext : JsonSerializerContext;
