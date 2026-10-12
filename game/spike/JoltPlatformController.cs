using System;

using BodyCheck.Core;

using Godot;

namespace BodyCheck.Spike;

public sealed record JoltPlatformTuning
{
    public Vector3 HalfSize { get; init; } = new(1.4f, 0.4f, 0.4f);
    public float Mass { get; init; } = 5.0f;
    public float Drag { get; init; } = 0.5f;

    /// <summary>Speed at which the horizontal drive force falls to zero.</summary>
    public float TopSpeed { get; init; } = 8;

    /// <summary>Force applied horizontally to move back and forth.</summary>
    public float HorizontalDriveForce { get; init; } = 120;

    /// <summary>How much the platform thrusts vertically to get back to its original altitude. Higher -> less sag.</summary>
    public float VerticalStiffness { get; init; } = 200;
    /// <summary>Maximum vertical thrust, as a fraction of weight. Should be > 1.</summary>
    public float MaxVerticalThrustFrac { get; init; } = 1.5f;
}

public sealed class JoltPlatformController : IJoltCharacterController
{
    public JoltPlatformTuning Tuning { get; }
    public JoltBody Body { get; }

    private float _move = 1;
    private readonly Vector3 _start;
    private readonly float _gravity;

    public JoltPlatformController(JoltBody body, JoltPlatformTuning tuning, float gravity)
    {
        Body = body;
        Tuning = tuning;
        _start = body.Position;
        _gravity = gravity;
    }

    public void Apply(JoltWorld world, float dt)
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

        float weight = Body.Mass * _gravity;
        ApplyHorizontalThrust(_move);
        ApplyVerticalThrust(weight);
    }

    private void ApplyHorizontalThrust(float move)
    {
        float speed = Body.LinearVelocity.X;
        // The drive fades out as the body nears top speed in the input direction.
        // It never slows a body that is already faster than that.
        float headroom = Math.Clamp(1 - speed * MathF.Sign(move) / Tuning.TopSpeed, 0, 1);

        Body.Traction = default;
        Body.AddForce(new Vector3(move * Tuning.HorizontalDriveForce * headroom, 0, 0));
    }

    private void ApplyVerticalThrust(float weight)
    {
        // PD controller: correction = K_p * (target - current) + K_d * -rate
        var damping = 2 * MathF.Sqrt(Tuning.VerticalStiffness * Tuning.Mass);
        var correction = Tuning.VerticalStiffness * (_start.Y - Body.Position.Y) + damping * -Body.LinearVelocity.Y;

        var thrust = Math.Clamp(weight + correction, 0, weight * Tuning.MaxVerticalThrustFrac);
        Body.AddForce(new Vector3(0, thrust, 0));
    }
}
