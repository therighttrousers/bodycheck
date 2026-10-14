using Godot;

namespace BodyCheck.Core;

public sealed record PlatformTuning
{
    /// <summary>Speed at which the horizontal drive force falls to zero.</summary>
    public float TopSpeed { get; init; } = 8;

    /// <summary>Force applied horizontally to move back and forth.</summary>
    public float HorizontalDriveForce { get; init; } = 120;

    /// <summary>How much the platform thrusts vertically to get back to its original altitude. Higher -> less sag.</summary>
    public float VerticalStiffness { get; init; } = 200;
    /// <summary>Maximum vertical thrust, as a fraction of weight. Should be > 1.</summary>
    public float MaxVerticalThrustFrac { get; init; } = 1.5f;
}

public sealed class PlatformController : ICharacterController<PlatformEntity>
{
    private readonly float _startHeight;
    private float _move = 1;

    /// <param name="startHeight">The height of the platform's center that it tries to hold.</param>
    public PlatformController(float startHeight, PlatformTuning tuning)
    {
        _startHeight = startHeight;
        Tuning = tuning;
    }

    public PlatformTuning Tuning { get; }

    public void Apply(PlatformEntity entity, float dt)
    {
        // Patrol. Bounce off of anything solid (including the player).
        if (_move < 0 && entity.Contacts.HasFlag(Contacts.Left))
        {
            _move = 1;
        }
        else if (_move > 0 && entity.Contacts.HasFlag(Contacts.Right))
        {
            _move = -1;
        }

        ApplyHorizontalThrust(entity, _move);
        ApplyVerticalThrust(entity);
    }

    private void ApplyHorizontalThrust(PlatformEntity entity, float move)
    {
        float speed = entity.LinearVelocity.X;
        // The drive fades out as the entity nears top speed in the input direction.
        // It never slows an entity that is already faster than that.
        float headroom = Math.Clamp(1 - speed * MathF.Sign(move) / Tuning.TopSpeed, 0, 1);

        entity.ApplyCentralForce(new Vector3(move * Tuning.HorizontalDriveForce * headroom, 0, 0));
    }

    private void ApplyVerticalThrust(PlatformEntity entity)
    {
        // PD controller: correction = K_p * (target - current) + K_d * -rate
        float weight = entity.Weight;
        var damping = 2 * MathF.Sqrt(Tuning.VerticalStiffness * entity.Mass);
        var correction = Tuning.VerticalStiffness * (_startHeight - entity.Position.Y) + damping * -entity.LinearVelocity.Y;

        var thrust = Math.Clamp(weight + correction, 0, weight * Tuning.MaxVerticalThrustFrac);
        entity.ApplyCentralForce(new Vector3(0, thrust, 0));
    }
}
