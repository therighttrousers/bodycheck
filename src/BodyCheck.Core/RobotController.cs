using System.Numerics;

namespace BodyCheck.Core;

public sealed record RobotTuning
{
    public Vector2 HalfSize { get; init; } = new(0.4f, 0.8f);
    public float Mass { get; init; } = 1.0f;
    public float Drag { get; init; } = 0.5f;

    /// <summary>Speed at which the run drive force falls to zero.</summary>
    public float TopSpeed { get; init; } = 4;

    /// <summary>Force applied when on the ground and moving in the direction of input.</summary>
    public float ForwardDriveForce { get; init; } = 20;
    /// <summary>Force applied when on the ground and moving against the direction of input.</summary>
    public float ReverseDriveForce { get; init; } = 40;

    /// <summary>Coefficient of ground friction, applied when not driving in the direction of travel.</summary>
    public float GroundFriction { get; init; } = 4;
    public float SupportFriction { get; init; } = 2;
}

public sealed class RobotController : ICharacterController
{
    public RobotTuning Tuning { get; }
    public Body Body { get; }

    private float _facing = 1;
    private float _move = 0;

    public RobotController(Vector2 position, RobotTuning tuning)
    {
        Tuning = tuning;
        Body = new Body(position, Tuning.HalfSize, Tuning.Mass) { Drag = Tuning.Drag };
    }

    public void Apply(World world, float dt)
    {
        // Patrol if on tiles. Bounce off of anything solid (including the player).
        if (Body.OnTiles)
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

        float weight = Body.Mass * -world.Gravity.Y;
        ApplyRun(_move, weight, dt);
        Body.ApplySupportFriction(Tuning.SupportFriction, weight, dt);
    }

    private void ApplyRun(float move, float weight, float dt)
    {
        float speed = Body.Velocity.X;
        bool reverse = speed != 0 && move * speed <= 0;

        if (move != 0)
        {
            // The drive fades out as the body nears top speed in the input direction.
            // It never slows a body that is already faster than that.
            float headroom = Math.Clamp(1 - speed * MathF.Sign(move) / Tuning.TopSpeed, 0, 1);
            float drive = reverse ? Tuning.ReverseDriveForce : Tuning.ForwardDriveForce;
            var force = new Vector2(move * drive * headroom, 0);
            Body.ApplyForce(force);
            Body.Support?.ApplyForce(-force);
        }

        if (Body.OnTiles && reverse)
        {
            // Friction can stop the body but can't reverse it.
            float impulse = MathF.Min(Tuning.GroundFriction * weight * dt, Body.Mass * MathF.Abs(speed));
            Body.ApplyImpulse(new Vector2(-MathF.Sign(speed) * impulse, 0));
        }
    }
}
