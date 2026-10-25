using Godot;

namespace BodyCheck.Core.Entities;

/// <summary>
/// What an entity's body is like, whatever controls it: its size, its mass, and how it meets the air and
/// other bodies.
/// </summary>
public sealed record BodyTuning
{
    public required Vector2 HalfSize { get; init; }
    public required float Mass { get; init; }

    /// <summary>Linear drag coefficient: drag force is -Drag × velocity.</summary>
    public float Drag { get; init; } = 0.375f;

    /// <summary>The layer this entity is on. Should be a bit in PhysicsLayer.EntityCollisionMask.</summary>
    public required PhysicsLayer CollisionLayer { get; init; }

    /// <summary>
    /// The engine's coefficient of friction for this body, against everything it touches, unless
    /// <see cref="GroundFriction"/> or a carrier's <see cref="CarryFriction"/> stands in for it.
    /// <para>
    /// Where two bodies touch, the engine uses the smaller of their coefficients, unless one is rough.
    /// </para>
    /// </summary>
    public float EngineFriction { get; init; }

    // The values below are features, which playtesting tunes, so each is a function that reads them from the features
    // in effect when it is used (016 §3.1).

    /// <summary>
    /// The engine's coefficient of friction for this body with what it stands on, in place of
    /// <see cref="EngineFriction"/>: off the ground it has none, so that it slides along walls, as a walker does (027).
    /// Null for <see cref="EngineFriction"/> everywhere.
    /// </summary>
    public Func<Features, float>? GroundFriction { get; init; }

    /// <summary>
    /// The coefficient of friction with which this body grips what stands on it, which wins over that body's own and
    /// this one's: a walker grabs what it carries (019 §1.4). What stands on it takes it, rough, as its own (027).
    /// Null leaves what stands on it to its own.
    /// </summary>
    public Func<Features, float>? CarryFriction { get; init; }

    /// <summary>
    /// The most force with which this body steadies what stands on it: it holds that body's speed at its center to its
    /// own, like a hand held up behind the head, so that friction at that body's base needn't, and doesn't tip it
    /// (019 §5.1). Null for none.
    /// </summary>
    public Func<Features, float>? CarrySteadying { get; init; }

    /// <summary>
    /// The most torque with which this body, while it steadies what stands on it, turns that body towards level, as a
    /// hand would (019 §5.1, §5.2). Null for none.
    /// </summary>
    public Func<Features, float>? CarryTorque { get; init; }

    /// <summary>
    /// Whether an entity this body rests on may carry it, with the values above. If not, only its own engine friction
    /// holds it to an entity, which on a walker is none while the walker drives, so it slides off a walker's head.
    /// </summary>
    public bool CanBeCarried { get; init; } = true;

    public static BodyTuning Player { get; } = new()
    {
        HalfSize = new(0.4f, 0.9f),
        Mass = 1,
        CollisionLayer = PhysicsLayer.Character,
        CanBeCarried = false,
        GroundFriction = features => features.PlayerGroundFriction,
        CarryFriction = features => features.PlayerCarryFriction,
        CarrySteadying = features => features.PlayerCarrySteadying,
        CarryTorque = features => features.PlayerCarryTorque,
    };

    /// <summary>A robot, which doesn't steady or hold what it carries.</summary>
    public static BodyTuning Robot { get; } = new()
    {
        HalfSize = new(0.4f, 0.9f),
        Mass = 1,
        CollisionLayer = PhysicsLayer.Character,
        GroundFriction = features => features.RobotGroundFriction,
        CarryFriction = features => features.RobotCarryFriction,
    };

    /// <summary>
    /// A hovering platform. Its friction carries the things that rest on it. Nothing carries it, so that the player
    /// can't wear one that comes down on its head.
    /// </summary>
    public static BodyTuning Platform { get; } = new()
    {
        HalfSize = new(1.4f, 0.4f),
        Mass = 5,
        EngineFriction = 1,
        CollisionLayer = PhysicsLayer.Character,
        CanBeCarried = false,
    };

    /// <summary>The block the player spawns: a block, but light enough to carry and jump with.</summary>
    public static BodyTuning PlayerBlock { get; } = new()
    {
        HalfSize = new(0.5f, 0.5f),
        Mass = 1,
        EngineFriction = 1,
        CollisionLayer = PhysicsLayer.Prop
    };

    /// <summary>A block, which has no controller: it is only pushed, carried and stood on.</summary>
    public static BodyTuning Block { get; } = new()
    {
        HalfSize = new(0.5f, 0.5f),
        Mass = 2,
        EngineFriction = 1,
        CollisionLayer = PhysicsLayer.Prop
    };
}
