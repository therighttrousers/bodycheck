using Godot;

namespace BodyCheck.Core;

/// <summary>
/// A block: a body with no controller, which is only pushed, carried and stood on. It grips through the
/// engine's friction, not through traction.
/// </summary>
public sealed record BlockTuning
{
    public Vector2 HalfSize { get; init; } = new(0.5f, 0.5f);
    public float Mass { get; init; } = 2.0f;
    public float Drag { get; init; } = 0.5f;

    /// <summary>The engine's coefficient of friction.</summary>
    public float EngineFriction { get; init; } = 1;

    public Body CreateBody() => new(HalfSize, Mass) { Drag = Drag, EngineFriction = EngineFriction };
}
