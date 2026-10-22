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

    /// <summary>
    /// The player's coefficient of friction with what it carries, such as a block on its head. It wins over the carried
    /// body's own. Holding a carried block through a reversal takes about 5.6.
    /// </summary>
    public float PlayerCarryFriction { get; init; } = 6;

    /// <summary>
    /// The most torque with which the player holds the rotation of what it carries to its own. Robots don't hold what
    /// they carry. A block of 1 tile tips off a body that decelerates at more than gravity unless something holds it.
    /// </summary>
    public float PlayerCarryTorque { get; init; } = 25;

    /// <summary>
    /// The most force with which the player steadies what it carries: it holds the carried body's speed at its center
    /// to its own, like a hand held up behind its head, so that the body neither slides nor tips. Robots
    /// don't steady what they carry. Steadying a block of mass 2 through a reversal takes about 315.
    /// </summary>
    public float PlayerCarrySteadying { get; init; } = 35;

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
    #endregion

    #region Robots
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

    /// <summary>A robot's coefficient of friction with what it carries. It wins over the carried body's own.</summary>
    public float RobotCarryFriction { get; init; } = 6;
    #endregion

    #region Platforms
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
