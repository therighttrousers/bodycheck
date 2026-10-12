using Godot;

namespace BodyCheck.Core;

public sealed record PlatformTuning
{
    public Vector2 HalfSize { get; init; } = new(1.4f, 0.4f);
    public float Mass { get; init; } = 5.0f;
    public float Drag { get; init; } = 0.5f;

    /// <summary>The engine's coefficient of friction, which carries the things that rest on the platform.</summary>
    public float EngineFriction { get; init; } = 1;

    /// <summary>Speed at which the horizontal drive force falls to zero.</summary>
    public float TopSpeed { get; init; } = 8;

    /// <summary>Force applied horizontally to move back and forth.</summary>
    public float HorizontalDriveForce { get; init; } = 120;

    /// <summary>How much the platform thrusts vertically to get back to its original altitude. Higher -> less sag.</summary>
    public float VerticalStiffness { get; init; } = 200;
    /// <summary>Maximum vertical thrust, as a fraction of weight. Should be > 1.</summary>
    public float MaxVerticalThrustFrac { get; init; } = 1.5f;
}

public sealed class PlatformController : ICharacterController
{
    public PlatformTuning Tuning { get; }
    public Body Body { get; }

    private float _move = 1;
    private readonly float _startHeight;

    /// <param name="startHeight">The height of the platform's center that it tries to hold.</param>
    public PlatformController(float startHeight, PlatformTuning tuning)
    {
        _startHeight = startHeight;
        Tuning = tuning;
        Body = new Body(Tuning.HalfSize, Tuning.Mass) { Drag = Tuning.Drag, EngineFriction = Tuning.EngineFriction };
    }

    public void Apply(World world, float dt)
    {
        // Patrol. Bounce off of anything solid (including the player).
        if (_move < 0 && Body.Contacts.HasFlag(Contacts.Left))
        {
            _move = 1;
        }
        else if (_move > 0 && Body.Contacts.HasFlag(Contacts.Right))
        {
            _move = -1;
        }

        float weight = Body.Mass * world.Gravity;
        ApplyHorizontalThrust(_move);
        ApplyVerticalThrust(weight);
    }

    private void ApplyHorizontalThrust(float move)
    {
        float speed = Body.Velocity.X;
        // The drive fades out as the body nears top speed in the input direction.
        // It never slows a body that is already faster than that.
        float headroom = Math.Clamp(1 - speed * MathF.Sign(move) / Tuning.TopSpeed, 0, 1);

        Body.Traction = default;
        Body.AddForce(new Vector2(move * Tuning.HorizontalDriveForce * headroom, 0));
    }

    private void ApplyVerticalThrust(float weight)
    {
        // PD controller: correction = K_p * (target - current) + K_d * -rate
        var damping = 2 * MathF.Sqrt(Tuning.VerticalStiffness * Tuning.Mass);
        var correction = Tuning.VerticalStiffness * (_startHeight - Body.Position.Y) + damping * -Body.Velocity.Y;

        var thrust = Math.Clamp(weight + correction, 0, weight * Tuning.MaxVerticalThrustFrac);
        Body.AddForce(new Vector2(0, thrust));
    }
}
