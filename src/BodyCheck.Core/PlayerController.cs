using System.Numerics;

namespace BodyCheck.Core;

/// <param name="Move">Horizontal input, from -1 (left) to 1 (right).</param>
public readonly record struct PlayerInput(float Move, bool JumpHeld);

/// <summary>
/// Tuning parameters for the player controller. Goals for setting these constants:
/// <list type="bullet">
///     <item>Minimum jump height is just over 2 tiles.</item>
///     <item>Maximum jump height is just over 4 tiles.</item>
///     <item>Jumps at full horizontal speed are approximately 45 degrees.</item>
///     <item>Air control is weaker than ground control.</item>
///     <item>Accelerating in reverse is faster than accelerating forward (mostly for precision landings).</item>
/// </list>
/// </summary>
/// 
/// TODO: Move this into a design doc
public sealed record PlayerTuning
{
    /// <summary>Speed at which the run drive force falls to zero.</summary>
    public float TopSpeed { get; init; } = 16;

    /// <summary>Force applied when on the ground and moving in the direction of input.</summary>
    public float GroundForwardDriveForce { get; init; } = 80;
    /// <summary>Force applied when on the ground and moving against the direction of input.</summary>
    public float GroundReverseDriveForce { get; init; } = 160;
    /// <summary>Force applied when in the air and moving in the direction of input.</summary>
    public float AirForwardDriveForce { get; init; } = 40;
    /// <summary>Force applied when in the air and moving against the direction of input.</summary>
    public float AirReverseDriveForce { get; init; } = 80;

    /// <summary>Coefficient of ground friction, applied when not driving in the direction of travel.</summary>
    public float Friction { get; init; } = 2;

    /// <summary>Upward speed given by the jump impulse.</summary>
    public float JumpSpeed { get; init; } = 16;

    /// <summary>Longest time the jump thrust lasts while the button is held.</summary>
    public float JumpHoldTime { get; init; } = 0.2f;

    /// <summary>Jump thrust as a fraction of the player's weight. At 1 it cancels gravity.</summary>
    public float JumpHoldThrust { get; init; } = 0.9f;

    /// <summary>Number of grace frames the player gets to jump after running off an edge.</summary>
    public ulong CoyoteFrames { get; init; } = 4;
}

/// <summary>
/// Turns player input into forces and impulses on the player's body.
/// Call <see cref="Apply"/> once per tick, before <see cref="World.Step"/>.
/// </summary>
public sealed class PlayerController
{
    private readonly Body _body;
    private bool _jumpWasHeld;
    private ulong _lastOnGroundFrame = 0;
    private int _thrustTicksLeft;

    public PlayerController(Body body, PlayerTuning tuning)
    {
        _body = body;
        Tuning = tuning;
    }

    public PlayerTuning Tuning { get; set; }

    public void Apply(PlayerInput input, World world, float dt, ulong frame)
    {
        if (_body.OnGround)
        {
            _lastOnGroundFrame = frame;
        }

        float weight = _body.Mass * -world.Gravity.Y;
        ApplyRun(Math.Clamp(input.Move, -1, 1), weight, dt);
        ApplyJump(input.JumpHeld, weight, dt, frame);

        _jumpWasHeld = input.JumpHeld;
    }

    private void ApplyRun(float move, float weight, float dt)
    {
        float speed = _body.Velocity.X;
        bool reverse = speed != 0 && move * speed <= 0;

        if (move != 0)
        {
            // The drive fades out as the body nears top speed in the input direction.
            // It never slows a body that is already faster than that.
            float headroom = Math.Clamp(1 - speed * MathF.Sign(move) / Tuning.TopSpeed, 0, 1);
            float force =
                _body.OnGround
                    ? (reverse ? Tuning.GroundReverseDriveForce : Tuning.GroundForwardDriveForce)
                    : (reverse ? Tuning.AirReverseDriveForce : Tuning.AirForwardDriveForce);
            _body.ApplyForce(new Vector2(move * force * headroom, 0));
        }

        if (_body.OnGround && reverse)
        {
            // Friction can stop the body but can't reverse it.
            float impulse = MathF.Min(Tuning.Friction * weight * dt, _body.Mass * MathF.Abs(speed));
            _body.ApplyImpulse(new Vector2(-MathF.Sign(speed) * impulse, 0));
        }
    }

    private void ApplyJump(bool jumpHeld, float weight, float dt, ulong frame)
    {
        if (jumpHeld && !_jumpWasHeld && (frame <= _lastOnGroundFrame + Tuning.CoyoteFrames))
        {
            _body.ApplyImpulse(new Vector2(0, _body.Mass * Tuning.JumpSpeed));
            _thrustTicksLeft = (int)MathF.Round(Tuning.JumpHoldTime / dt);
        }
        else if (!jumpHeld || _body.Velocity.Y <= 0)
        {
            // Released, or stopped rising (e.g. hit a ceiling): the thrust can't resume.
            _thrustTicksLeft = 0;
        }

        if (_thrustTicksLeft > 0)
        {
            _body.ApplyForce(new Vector2(0, Tuning.JumpHoldThrust * weight));
            _thrustTicksLeft--;
        }
    }
}
