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
    /// <summary>The tick rate that features given in ticks count at.</summary>
    public const int TuningTicksPerSecond = 60;

    /// <summary>
    /// A length of time given in ticks at <see cref="TuningTicksPerSecond"/>, less half a tick: a timer set to it and
    /// counted down by ticks' lengths at full speed stays above zero for exactly that many ticks, with half a tick to
    /// spare either way however the lengths round.
    /// </summary>
    public static float TicksToTime(int ticks) => (ticks - 0.5f) / TuningTicksPerSecond;
    #endregion

    #region World
    /// <summary>
    /// The default friction for static tiles. Walkers have no engine friction, and the smaller of two coefficients
    /// wins, so this only affects bodies that have some.
    /// </summary>
    public float TileDefaultFriction { get; init; } = 1;
    #endregion

    #region Player
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
    /// The player's coefficient of friction with the ground, whether tiles or another body. Applied when not driving in
    /// the direction of travel.
    /// </summary>
    public float PlayerGroundFriction { get; init; } = 4;

    /// <summary>
    /// The player's coefficient of friction with what it carries, such as a block on its head. It wins over the carried
    /// body's own. Holding a carried block through a reversal takes about 5.6.
    /// </summary>
    public float PlayerCarryFriction { get; init; } = 6;

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
    public float PlayerCarrySteadying { get; init; } = 0;

    /// <summary>Upward speed given by the jump impulse.</summary>
    public float PlayerJumpSpeed { get; init; } = 12.7f;

    /// <summary>
    /// How long the jump thrust lasts while the button is held, in ticks at <see cref="TuningTicksPerSecond"/> of game
    /// time. Includes the tick with the initial jump impulse.
    /// </summary>
    public int PlayerJumpThrustTicks { get; init; } = 14;

    /// <summary>Jump thrust as a fraction of the player's weight. At 1 it cancels gravity.</summary>
    public float PlayerJumpThrustFrac { get; init; } = 0.9f;

    /// <summary>
    /// How long the player gets to jump, or turn around with ground thrust, *after* running off an edge, in ticks at
    /// <see cref="TuningTicksPerSecond"/> of game time.
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
    /// <summary>Speed at which a robot's run drive force falls to zero.</summary>
    public float RobotTopSpeed { get; init; } = 3;

    /// <summary>Force applied to a robot when on the ground and facing the direction of motion.</summary>
    public float RobotDriveForce { get; init; } = 11.25f;
    /// <summary>Force applied to a robot when on the ground and facing opposite the direction of motion.</summary>
    public float RobotBrakeForce { get; init; } = 22.5f;

    /// <summary>
    /// A robot's coefficient of friction with the ground, whether tiles or another body. Applied when not driving in
    /// the direction of travel.
    /// </summary>
    public float RobotGroundFriction { get; init; } = 4;

    /// <summary>A robot's coefficient of friction with what it carries. It wins over the carried body's own.</summary>
    public float RobotCarryFriction { get; init; } = 6;
    #endregion

    #region Platforms
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
}

/// <summary>The code that reads configurations, which System.Text.Json's source generator writes at compile time.</summary>
[JsonSourceGenerationOptions(
    RespectNullableAnnotations = true,
    UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    ReadCommentHandling = JsonCommentHandling.Skip,
    AllowTrailingCommas = true)]
[JsonSerializable(typeof(Features))]
internal sealed partial class FeaturesJsonContext : JsonSerializerContext;
