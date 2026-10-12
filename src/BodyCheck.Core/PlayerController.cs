using Godot;

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

    /// <summary>
    /// Coefficient of friction with the ground, whether tiles or another body. Applied when not driving in
    /// the direction of travel.
    /// </summary>
    public float GroundFriction { get; init; } = 4;

    /// <summary>Upward speed given by the jump impulse.</summary>
    public float JumpSpeed { get; init; } = 16;

    /// <summary>
    /// Number of frames the jump thrust lasts while the button is held. Includes the frame with the initial jump impulse.
    /// </summary>
    public int JumpThrustTicks { get; init; } = 13;

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
    private Vector2 _lastGroundVelocity = Vector2.Zero;
    private int _thrustTicksLeft;

    public PlayerController(PlayerTuning tuning)
    {
        Tuning = tuning;
        Body = new Body(Tuning.HalfSize, Tuning.Mass) { Drag = Tuning.Drag };
    }

    public void Apply(PlayerInput input, World world, float dt)
    {
        if (Body.OnGround)
        {
            _groundTicksLeft = Tuning.CoyoteTicks + 1; // +1 to include this frame in remaining ground ticks
            _lastGroundVelocity = Body.Velocity;
        }

        float weight = Body.Mass * world.Gravity;
        ApplyRun(Math.Clamp(input.Move, -1, 1), weight);
        ApplyJump(input.JumpHeld, weight);

        _groundTicksLeft = Math.Max(0, _groundTicksLeft - 1);
        _thrustTicksLeft = Math.Max(0, _thrustTicksLeft - 1);
        _jumpWasHeld = input.JumpHeld;
    }

    private void ApplyRun(float move, float weight)
    {
        // Speed is relative to whatever the body stands on, so running on a robot works like running on tiles.
        float speed = Body.Velocity.X - Body.GroundVelocity.X;
        bool reverse = speed != 0 && move * speed <= 0;

        float drive = 0;
        if (move != 0)
        {
            // The drive fades out as the body nears top speed in the input direction.
            // It never slows a body that is already faster than that.
            float headroom = Math.Clamp(1 - speed * MathF.Sign(move) / Tuning.TopSpeed, 0, 1);
            float driveForce =
                _groundTicksLeft > 0
                    ? (reverse ? Tuning.GroundReverseDriveForce : Tuning.GroundForwardDriveForce)
                    : (reverse ? Tuning.AirReverseDriveForce : Tuning.AirForwardDriveForce);
            drive = MathF.Abs(move) * driveForce * headroom;
        }

        if (Body.OnGround)
        {
            // On the ground the drive is traction: the world applies it, and pushes back on the support.
            // Friction adds to it unless the body is driving the way it is already going, so it holds an
            // idle body still on its support, and can stop a moving one but can't reverse it.
            float friction = move == 0 || reverse ? Tuning.GroundFriction * weight : 0;
            Body.Traction = new Traction(MathF.Sign(move) * Tuning.TopSpeed, drive + friction);
        }
        else
        {
            Body.Traction = default;
            Body.AddForce(new Vector2(MathF.Sign(move) * drive, 0));
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
            // From jump speed, subtract the delta-Y accumulated during coyote frames (which should be negative), to
            // allow coyote jumps to go just as high as jumps from the ground.
            var coyoteVelocityY = Body.Velocity.Y - _lastGroundVelocity.Y;
            var impulse = new Vector2(0, Body.Mass * (Tuning.JumpSpeed - coyoteVelocityY));
            Body.AddImpulse(impulse);
            Body.Support?.AddImpulse(-impulse);
            _groundTicksLeft = 0;
            _thrustTicksLeft = Tuning.JumpThrustTicks + 1;

            // The impulse doesn't show in the body's velocity until the engine's step, so thrust waits a tick.
            return;
        }

        // If stopped rising (e.g. hit a ceiling), cancel thrust.
        if (Body.Velocity.Y <= 0)
        {
            _thrustTicksLeft = 0;
        }

        if (_thrustTicksLeft > 0)
        {
            var force = new Vector2(0, Tuning.JumpThrustFrac * weight);
            Body.AddForce(force);
            Body.Support?.AddForce(-force);
        }
    }
}
