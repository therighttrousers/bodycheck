using Godot;

namespace BodyCheck.Core;

/// <summary>
/// What an entity's body is like, whatever controls it: its size, its mass, and how it meets the air and
/// other bodies.
/// </summary>
public sealed record BodyTuning
{
    public required Vector2 HalfSize { get; init; }
    public required float Mass { get; init; }

    /// <summary>Linear drag coefficient: drag force is -Drag × velocity.</summary>
    public float Drag { get; init; } = 0.5f;

    /// <summary>
    /// The engine's coefficient of friction for this body. Leave it at 0 for anything that walks: a walker
    /// grips through traction, and with engine friction it would grip walls too, and lose its speed on
    /// landing. Other bodies, which have no traction, need it to rest on things and be carried.
    /// <para>
    /// Where two bodies touch, the engine uses the smaller of their coefficients, so a walker slides
    /// freely against everything.
    /// </para>
    /// </summary>
    public float EngineFriction { get; init; }

    /// <summary>Whether this body grips through traction rather than through the engine's friction.</summary>
    public bool IsWalker => EngineFriction == 0;

    public static BodyTuning Player { get; } = new() { HalfSize = new(0.4f, 0.8f), Mass = 1 };

    public static BodyTuning Robot { get; } = new() { HalfSize = new(0.4f, 0.8f), Mass = 1 };

    /// <summary>A hovering platform. Its friction carries the things that rest on it.</summary>
    public static BodyTuning Platform { get; } = new() { HalfSize = new(1.4f, 0.4f), Mass = 5, EngineFriction = 1 };

    /// <summary>A block, which has no controller: it is only pushed, carried and stood on.</summary>
    public static BodyTuning Block { get; } = new() { HalfSize = new(0.5f, 0.5f), Mass = 2, EngineFriction = 1 };
}
