using System;

using BodyCheck.Core;

using Godot;

namespace BodyCheck.Spike;

public sealed record JoltRobotTuning
{
    public Vector3 HalfSize { get; init; } = new(0.4f, 0.8f, 0.4f);
    public float Mass { get; init; } = 1.0f;
    public float Drag { get; init; } = 0.5f;

    /// <summary>Speed at which the run drive force falls to zero.</summary>
    public float TopSpeed { get; init; } = 4;

    /// <summary>Force applied when on the ground and moving in the direction of input.</summary>
    public float ForwardDriveForce { get; init; } = 20;
    /// <summary>Force applied when on the ground and moving against the direction of input.</summary>
    public float ReverseDriveForce { get; init; } = 40;

    /// <summary>
    /// Coefficient of friction with the ground, whether tiles or another body. Applied when not driving in
    /// the direction of travel.
    /// </summary>
    public float GroundFriction { get; init; } = 4;
}

public sealed class JoltRobotController : IJoltCharacterController
{
    public JoltRobotTuning Tuning { get; }
    public JoltBody Body { get; }

    private readonly float _gravity;
    private float _facing = 1;
    private float _move = 0;

    public JoltRobotController(JoltBody body, JoltRobotTuning tuning, float gravity)
    {
        Tuning = tuning;
        Body = body;
        _gravity = gravity;
    }

    public void Apply(JoltWorld world, float dt)
    {
        // Patrol if on tiles. Bounce off of anything solid (including the player).
        if (Body.OnGround && Body.Support is null)
        {
            if (_move == 0)
            {
                _move = _facing;
            }
            else if (_move < 0 && Body.Contacts.HasFlag(Contacts.Left))
            {
                _facing = _move = 1;
            }
            else if (_move > 0 && Body.Contacts.HasFlag(Contacts.Right))
            {
                _facing = _move = -1;
            }
        }
        else
        {
            _move = 0;
        }

        float weight = Body.Mass * _gravity;
        ApplyRun(_move, weight);
    }

    private void ApplyRun(float move, float weight)
    {
        // Speed is relative to whatever the body stands on.
        float speed = Body.LinearVelocity.X - Body.GroundVelocity.X;
        bool reverse = speed != 0 && move * speed <= 0;

        float drive = 0;
        if (move != 0)
        {
            // The drive fades out as the body nears top speed in the input direction.
            // It never slows a body that is already faster than that.
            float headroom = Math.Clamp(1 - speed * MathF.Sign(move) / Tuning.TopSpeed, 0, 1);
            float force = reverse ? Tuning.ReverseDriveForce : Tuning.ForwardDriveForce;
            drive = MathF.Abs(move) * force * headroom;
        }

        // The drive is traction: the world applies it, and pushes back on the support. Friction adds to it
        // unless the body is driving the way it is already going, so it holds an idle body still on its
        // support, and can stop a moving one but can't reverse it. Neither does anything in the air.
        float friction = move == 0 || reverse ? Tuning.GroundFriction * weight : 0;
        Body.Traction = new Traction(MathF.Sign(move) * Tuning.TopSpeed, drive + friction);
    }
}
