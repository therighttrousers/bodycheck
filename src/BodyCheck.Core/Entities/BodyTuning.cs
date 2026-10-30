using Godot;

namespace BodyCheck.Core.Entities;

/// <summary>
/// What an entity's body is like, whatever controls it: its size, its mass, and how it meets the air and
/// other bodies.
/// <para>
/// Its values that are features, which playtesting tunes, are functions that read them from the features in effect when
/// they are used, so that a change to them takes effect at once.
/// </para>
/// </summary>
public sealed record BodyTuning
{
    public required Vector2 HalfSize { get; init; }

    /// <summary>
    /// The body's mass. The engine holds it, and the world puts it there at the start of each tick, before any entity
    /// steps, so that a change to the features takes effect at the next tick (029 §2.1). Read the entity's own.
    /// </summary>
    public required Func<float> Mass { get; init; }

    /// <summary>Linear drag coefficient: drag force is -Drag × velocity.</summary>
    public float Drag { get; init; } = 0.375f;

    /// <summary>The layer this entity is on. Should be a bit in PhysicsLayer.EntityCollisionMask.</summary>
    public required PhysicsLayer CollisionLayer { get; init; }

    /// <summary>
    /// The engine's coefficient of friction for this body, against everything it touches. The player's changes with
    /// what it touches and does, with <see cref="GroundFriction"/>; see <see cref="PlayerEntity"/>.
    /// <para>
    /// Where two bodies touch, the engine uses the smaller of their coefficients, unless one is rough.
    /// </para>
    /// </summary>
    public Func<float> EngineFriction { get; init; } = () => 0;

    /// <summary>
    /// The player's coefficient of friction, rough, while it stands on something and doesn't slip, in place of
    /// <see cref="EngineFriction"/>. Only a <see cref="PlayerEntity"/> reads it.
    /// </summary>
    public Func<float>? GroundFriction { get; init; }

    /// <summary>
    /// The most force with which this body steadies what stands on it: it holds that body's speed at its center to its
    /// own, like a hand held up behind the head, so that friction at that body's base needn't, and doesn't tip it
    /// (019 §5.1). Null for none.
    /// </summary>
    public Func<float>? CarrySteadying { get; init; }

    /// <summary>
    /// The most torque with which this body, while it steadies what stands on it, turns that body towards level, as a
    /// hand would (019 §5.1, §5.2). Null for none.
    /// </summary>
    public Func<float>? CarryTorque { get; init; }

    /// <summary>
    /// Whether an entity this body rests on may steady it, with the values above. Either way, friction holds it to
    /// what it rests on as it holds any two bodies.
    /// </summary>
    public bool CanBeCarried { get; init; } = true;

    public static BodyTuning Player { get; } = new()
    {
        HalfSize = new(0.4f, 0.9f),
        Mass = () => Features.Current.PlayerMass,
        CollisionLayer = PhysicsLayer.Character,
        CanBeCarried = false,
        GroundFriction = () => Features.Current.PlayerGroundFriction,
        CarrySteadying = () => Features.Current.PlayerCarrySteadying,
        CarryTorque = () => Features.Current.PlayerCarryTorque,
    };

    /// <summary>A robot, which doesn't steady what it carries.</summary>
    public static BodyTuning Robot { get; } = new()
    {
        HalfSize = new(0.4f, 0.9f),
        Mass = () => Features.Current.RobotMass,
        EngineFriction = () => Features.Current.RobotEngineFriction,
        CollisionLayer = PhysicsLayer.Character,
    };

    /// <summary>
    /// A hovering platform. Its friction carries the things that rest on it. Nothing carries it, so that the player
    /// can't wear one that comes down on its head.
    /// </summary>
    public static BodyTuning Platform { get; } = new()
    {
        HalfSize = new(1.4f, 0.4f),
        Mass = () => Features.Current.PlatformMass,
        EngineFriction = () => Features.Current.PlatformEngineFriction,
        CollisionLayer = PhysicsLayer.Character,
        CanBeCarried = false,
    };

    /// <summary>The block the player spawns: a block, but light enough to carry and jump with.</summary>
    public static BodyTuning PlayerBlock { get; } = new()
    {
        HalfSize = new(0.5f, 0.5f),
        Mass = () => Features.Current.PlayerBlockMass,
        EngineFriction = () => Features.Current.PlayerBlockEngineFriction,
        CollisionLayer = PhysicsLayer.Prop
    };

    /// <summary>A block, which has no controller: it is only pushed, carried and stood on.</summary>
    public static BodyTuning Block { get; } = new()
    {
        HalfSize = new(0.5f, 0.5f),
        Mass = () => Features.Current.BlockMass,
        EngineFriction = () => Features.Current.BlockEngineFriction,
        CollisionLayer = PhysicsLayer.Prop
    };
}
