using System.Numerics;

namespace BodyCheck.Core;

/// <param name="Move">Horizontal input, from -1 (left) to 1 (right).</param>
public readonly record struct PlayerInput(float Move, bool JumpHeld);

public sealed record PlayerTuning
{
    public Vector2 HalfSize { get; init; } = new(0.4f, 0.8f);
    public float Mass { get; init; } = 1.0f;
    public float Drag { get; init; } = 0.5f;

    /// <summary>Speed at which the run drive force falls to zero.</summary>
    public float TopSpeed { get; init; } = 16;

    /// <summary>Force applied when on the ground and moving in the direction of input.</summary>
    public float GroundForwardDriveForce { get; init; } = 120;
    /// <summary>Force applied when on the ground and moving against the direction of input.</summary>
    public float GroundReverseDriveForce { get; init; } = 240;
    /// <summary>Force applied when in the air and moving in the direction of input.</summary>
    public float AirForwardDriveForce { get; init; } = 40;
    /// <summary>Force applied when in the air and moving against the direction of input.</summary>
    public float AirReverseDriveForce { get; init; } = 80;

    /// <summary>Coefficient of ground friction, applied when not driving in the direction of travel.</summary>
    public float Friction { get; init; } = 4;

    /// <summary>Upward speed given by the jump impulse.</summary>
    public float JumpSpeed { get; init; } = 16;

    /// <summary>
    /// Number of frames the jump thrust lasts while the button is held. Includes the frame with the initial jump impulse.
    /// </summary>
    public int JumpThrustTicks { get; init; } = 12;

    /// <summary>Jump thrust as a fraction of the player's weight. At 1 it cancels gravity.</summary>
    public float JumpThrustFrac { get; init; } = 0.9f;

    /// <summary>
    /// Number of frames the player gets to jump, or turn around with ground thrust, *after* running off an edge.
    /// </summary>
    public int CoyoteTicks { get; init; } = 4;
}

/// <summary>
/// Turns player input into forces and impulses on the player's body.
/// Call <see cref="Apply"/> once per tick, before <see cref="World.Step"/>.
/// </summary>
public sealed class PlayerController
{
    public PlayerTuning Tuning { get; }
    public Body Body { get; }

    private bool _jumpWasHeld;
    private int _groundTicksLeft;
    private int _thrustTicksLeft;

    public PlayerController(Vector2 position, PlayerTuning tuning)
    {
        Tuning = tuning;
        Body = new Body(position, Tuning.HalfSize, Tuning.Mass) { Drag = Tuning.Drag };
    }

    public void Teleport(Vector2 position, Vector2 velocity)
    {
        Body.Teleport(position, velocity);
        _groundTicksLeft = 0;
        _thrustTicksLeft = 0;
    }

    public void Apply(PlayerInput input, World world, float dt)
    {
        if (Body.OnGround)
        {
            _groundTicksLeft = Tuning.CoyoteTicks + 1; // +1 to include this frame in remaining ground ticks
        }

        float weight = Body.Mass * -world.Gravity.Y;
        ApplyRun(Math.Clamp(input.Move, -1, 1), weight, dt);
        ApplyJump(input.JumpHeld, weight);

        _groundTicksLeft = Math.Max(0, _groundTicksLeft - 1);
        _thrustTicksLeft = Math.Max(0, _thrustTicksLeft - 1);
        _jumpWasHeld = input.JumpHeld;
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
            float force =
                _groundTicksLeft > 0
                    ? (reverse ? Tuning.GroundReverseDriveForce : Tuning.GroundForwardDriveForce)
                    : (reverse ? Tuning.AirReverseDriveForce : Tuning.AirForwardDriveForce);
            Body.ApplyForce(new Vector2(move * force * headroom, 0));
        }

        if (Body.OnGround && reverse)
        {
            // Friction can stop the body but can't reverse it.
            float impulse = MathF.Min(Tuning.Friction * weight * dt, Body.Mass * MathF.Abs(speed));
            Body.ApplyImpulse(new Vector2(-MathF.Sign(speed) * impulse, 0));
        }
    }

    private void ApplyJump(bool jumpHeld, float weight)
    {
        if (!jumpHeld)
        {
            return;
        }

        if (!_jumpWasHeld && _groundTicksLeft > 0)
        {
            Body.ApplyImpulse(new Vector2(0, Body.Mass * (Tuning.JumpSpeed - Body.Velocity.Y)));
            _groundTicksLeft = 0;
            _thrustTicksLeft = Tuning.JumpThrustTicks;
        }

        // If stopped rising (e.g. hit a ceiling), cancel thrust.
        if (Body.Velocity.Y <= 0)
        {
            _thrustTicksLeft = 0;
        }

        if (_thrustTicksLeft > 0)
        {
            Body.ApplyForce(new Vector2(0, Tuning.JumpThrustFrac * weight));
        }
    }
}
