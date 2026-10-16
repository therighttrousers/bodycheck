using Godot;

namespace BodyCheck.Core;

/// <param name="Move">Horizontal input, from -1 (left) to 1 (right).</param>
public readonly record struct PlayerInput(float Move, bool JumpHeld);

public sealed record PlayerTuning
{
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
/// Turns player input into forces, impulses and traction on the entity it controls.
/// </summary>
public sealed class PlayerController : ICharacterController<WalkerEntity>
{
    private readonly Func<PlayerInput> _input;

    private bool _jumpWasHeld;
    private int _groundTicksLeft;
    private Vector3 _lastGroundVelocity = Vector3.Zero;
    private int _thrustTicksLeft;

    /// <param name="input">Supplies the player's input, once per tick.</param>
    public PlayerController(PlayerTuning tuning, Func<PlayerInput> input)
    {
        Tuning = tuning;
        _input = input;
    }

    public PlayerTuning Tuning { get; }

    public void Apply(WalkerEntity entity, float dt)
    {
        PlayerInput input = _input();
        float weight = entity.Weight;
        float move = Math.Clamp(input.Move, -1, 1);

        if (entity.OnGround)
        {
            _groundTicksLeft = Tuning.CoyoteTicks + 1; // +1 to include this frame in remaining ground ticks
            _lastGroundVelocity = entity.LinearVelocity;
        }

        ApplyRun(entity, move, weight);
        ApplyJump(entity, input.JumpHeld, weight);

        // Set animation variables
        entity.IsGrounded = _groundTicksLeft > 0;
        entity.IsMoving = move != 0;
        entity.IsJumping = _thrustTicksLeft > 0;
        // Only change facing direction when on the ground or just after jumping (and allow setting to 0)
        if (_groundTicksLeft > 0 || _thrustTicksLeft > 0)
        {
            entity.Facing = Math.Sign(move);
        }

        _groundTicksLeft = Math.Max(0, _groundTicksLeft - 1);
        _thrustTicksLeft = Math.Max(0, _thrustTicksLeft - 1);
        _jumpWasHeld = input.JumpHeld;
    }

    private void ApplyRun(WalkerEntity entity, float move, float weight)
    {
        // Speed is relative to whatever the entity stands on, so running on a robot works like running on tiles.
        float speed = entity.GroundSpeed;
        bool reverse = speed != 0 && move * speed <= 0;

        float drive = 0;
        if (move != 0)
        {
            // The drive fades out as the entity nears top speed in the input direction.
            // It never slows an entity that is already faster than that.
            float headroom = Math.Clamp(1 - speed * MathF.Sign(move) / Tuning.TopSpeed, 0, 1);
            float driveForce =
                _groundTicksLeft > 0
                    ? (reverse ? Tuning.GroundReverseDriveForce : Tuning.GroundForwardDriveForce)
                    : (reverse ? Tuning.AirReverseDriveForce : Tuning.AirForwardDriveForce);
            drive = MathF.Abs(move) * driveForce * headroom;
        }

        if (entity.OnGround)
        {
            // On the ground the drive is traction: the engine applies it, and pushes back on the support.
            // Friction adds to it unless the entity is driving the way it is already going, so it holds an
            // idle entity still on its support, and can stop a moving one but can't reverse it.
            float friction = move == 0 || reverse ? Tuning.GroundFriction * weight : 0;
            entity.ApplyTraction(MathF.Sign(move) * Tuning.TopSpeed, drive + friction);
        }
        else
        {
            entity.ApplyCentralForce(new Vector3(MathF.Sign(move) * drive, 0, 0));
        }
    }

    private void ApplyJump(WalkerEntity entity, bool jumpHeld, float weight)
    {
        if (!jumpHeld)
        {
            return;
        }

        if (!_jumpWasHeld && _groundTicksLeft > 0)
        {
            // From jump speed, subtract the delta-Y accumulated during coyote frames (which should be negative), to
            // allow coyote jumps to go just as high as jumps from the ground.
            var coyoteVelocityY = entity.LinearVelocity.Y - _lastGroundVelocity.Y;
            var impulse = new Vector3(0, entity.Mass * (Tuning.JumpSpeed - coyoteVelocityY), 0);
            entity.ApplyCentralImpulse(impulse);
            entity.Support?.ApplyCentralImpulse(-impulse);
            _groundTicksLeft = 0;
            _thrustTicksLeft = Tuning.JumpThrustTicks + 1;

            // Thrust starts on the next tick.
            return;
        }

        // If stopped rising (e.g. hit a ceiling), cancel thrust.
        if (entity.LinearVelocity.Y <= 0)
        {
            _thrustTicksLeft = 0;
        }

        if (_thrustTicksLeft > 0)
        {
            var force = new Vector3(0, Tuning.JumpThrustFrac * weight, 0);
            entity.ApplyCentralForce(force);
            entity.Support?.ApplyCentralForce(-force);
        }
    }
}
