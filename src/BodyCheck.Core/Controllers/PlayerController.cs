using BodyCheck.Core.Entities;

using Godot;

namespace BodyCheck.Core.Controllers;

/// <param name="XAxis">Horizontal input, from -1 (left) to 1 (right).</param>
/// <param name="YAxis">Vertical input, from -1 (down) to 1 (up).</param>
public readonly record struct PlayerInput(float XAxis, float YAxis, bool JumpHeld, bool PhaseHeld = false)
{
    public bool CanLook => YAxis != 0 && XAxis == 0 && !JumpHeld && !PhaseHeld;
}

/// <summary>
/// Turns player input into forces, impulses and traction on the entity it controls.
/// </summary>
public sealed class PlayerController : ICharacterController<WalkerEntity>
{
    // How far, from 0 to 1, the stick must be pushed for one of the eight directions to count as held.
    private const float DirectionDeadZone = 0.5f;

    private readonly Func<PlayerInput> _input;

    private bool _jumpWasHeld;
    private int _groundTicksLeft;
    private Vector3 _lastGroundVelocity = Vector3.Zero;
    private int _thrustTicksLeft;
    private float _phaseCooldownLeft;
    // Whether a phase has been made that the engine hasn't stepped since. Its bodies move just before the engine's step
    // on the first tick of play, so until then contacts are from before the phase.
    private bool _phaseMade;

    // The current time in the air, for telemetry: where the feet were on the last tick on the ground.
    private bool _inAir;
    private Vector2 _takeOffFeet;
    private float _highestFeet;
    private int _airTicks;

    /// <param name="input">Supplies the player's input, once per tick.</param>
    public PlayerController(Func<PlayerInput> input)
    {
        _input = input;
    }

    /// <summary>
    /// The phase being aimed, from when phase is held with a target in reach, after the cooldown, until it is released.
    /// </summary>
    public PhaseAction? Phase { get; private set; }

    /// <summary>The game is paused while a phase is aimed, so that the player can take its time over the direction.</summary>
    public bool RequestsPause => Phase is not null;

    /// <summary>
    /// Aims the phase while the game is paused for it. Releasing phase ends the aim, and the bodies move on the next
    /// tick of play.
    /// </summary>
    public void ApplyPaused(WalkerEntity entity, float dt)
    {
        PlayerInput input = _input();
        ApplyPhase(entity, input, dt);
        _jumpWasHeld = input.JumpHeld;
    }

    public void Apply(WalkerEntity entity, float dt)
    {
        PlayerInput input = _input();
        float weight = entity.Weight;
        float move = Math.Clamp(input.XAxis, -1, 1);

        if (entity.OnGround && !_phaseMade)
        {
            _groundTicksLeft = Features.Current.PlayerCoyoteTicks + 1; // +1 to include this frame in remaining ground ticks
            _lastGroundVelocity = entity.GroundVelocity;
        }

        if (!ApplyPhase(entity, input, dt))
        {
            ApplyRun(entity, move, weight);
            ApplyJump(entity, input.JumpHeld, weight);
        }

        // Set animation variables
        entity.IsGrounded = _groundTicksLeft > 0;
        entity.IsMoving = move != 0;
        entity.IsJumping = _thrustTicksLeft > 0;
        // Only change facing direction when on the ground or just after jumping (and allow setting to 0)
        if (_groundTicksLeft > 0 || _thrustTicksLeft > 0)
        {
            entity.Facing = Math.Sign(move);
        }

        Report(entity);

        _groundTicksLeft = Math.Max(0, _groundTicksLeft - 1);
        _thrustTicksLeft = Math.Max(0, _thrustTicksLeft - 1);
        _jumpWasHeld = input.JumpHeld;
        _phaseCooldownLeft = Math.Max(0, _phaseCooldownLeft - dt);
        _phaseMade = false;
    }

    /// <summary>Reports the player's measurements to <see cref="Telemetry"/>, as of the engine's last step.</summary>
    private void Report(WalkerEntity entity)
    {
        var feet = new Vector2(entity.Position.X, entity.Position.Y - entity.HalfSize.Y);
        var velocity = new Vector2(entity.LinearVelocity.X, entity.LinearVelocity.Y);
        Telemetry.Report(Metric.Feet, feet);
        Telemetry.Report(Metric.Velocity, velocity);
        Telemetry.Report(Metric.GroundSpeed, entity.GroundSpeed);
        Telemetry.Report(Metric.OnGround, entity.OnGround);
        Telemetry.Report(Metric.CoyoteTicks, entity.OnGround ? 0 : _groundTicksLeft);
        Telemetry.Report(Metric.ThrustTicks, _thrustTicksLeft);

        if (!entity.OnGround)
        {
            if (!_inAir)
            {
                _inAir = true;
                _highestFeet = _takeOffFeet.Y;
                _airTicks = 0;
            }
            _highestFeet = MathF.Max(_highestFeet, feet.Y);
            _airTicks++;
            return;
        }

        if (_inAir)
        {
            _inAir = false;
            Telemetry.Report(Metric.AirRise, _highestFeet - _takeOffFeet.Y);
            Telemetry.Report(Metric.AirRun, MathF.Abs(feet.X - _takeOffFeet.X));
            Telemetry.Report(Metric.AirLand, feet.Y - _takeOffFeet.Y);
            Telemetry.Report(Metric.AirTicks, _airTicks);
        }

        // The engine still reports the floor contact for a tick after a jump, when the feet have already
        // risen. Thrust ticks count from the jump, so they mark it.
        if (_thrustTicksLeft == 0)
        {
            _takeOffFeet = feet;
        }
    }

    /// <summary>
    /// Begins, holds, aims and releases a phase. Returns whether the phase takes the place of running and jumping on
    /// this tick: while it is aimed, and on the tick of play on which a phase made moves the bodies, when the contacts
    /// are from before it.
    /// </summary>
    private bool ApplyPhase(WalkerEntity entity, PlayerInput input, float dt)
    {
        // Made while paused, and moving on this tick.
        if (_phaseMade)
        {
            return true;
        }

        if (Phase is null && input.PhaseHeld && _phaseCooldownLeft <= 0 && TargetedEntity(entity, input) is Entity target)
        {
            Phase = new PhaseAction(
                player: entity,
                target: target,
                grabOffset: target.Position - entity.Position,
                gap: Features.Current.PlayerPhaseGap,
                phaseThrow: Features.Current.PlayerPhaseThrow,
                speed: Features.Current.PlayerPhaseSpeed);

            _phaseCooldownLeft = Features.Current.PlayerPhaseCooldownTime;
        }

        if (Phase is not PhaseAction phase)
        {
            return false;
        }

        phase.Grab(dt);
        phase.Direction = DirectionHeld(input);
        if (input.PhaseHeld)
        {
            return true;
        }

        if (phase.Direction != Vector3.Zero)
        {
            phase.Phase();
            _phaseMade = true;
        }
        Phase = null;

        // A phase ends a jump's thrust and coyote time, and so does cancelling one: the aim took them.
        _groundTicksLeft = 0;
        _thrustTicksLeft = 0;
        return _phaseMade;
    }

    /// <summary>One of the eight directions, from the input, as a unit vector in the plane of play, or zero if none.</summary>
    private static Vector3 DirectionHeld(PlayerInput input)
    {
        var held = new Vector2(input.XAxis, input.YAxis);
        return held.Length() >= DirectionDeadZone ? Vector2.EightWay(held).ToVector3() : Vector3.Zero;
    }

    /// <summary>
    /// The entity in reach, if any, whose box is nearest a point offset from the player's center in the direction
    /// held. Reach is a sphere around the player's center, which meets every entity touching the player's box.
    /// </summary>
    private Entity? TargetedEntity(WalkerEntity entity, PlayerInput input)
    {
        float radius = entity.HalfSize.Length() + Features.Current.PlayerPhaseReach;
        Vector2 from = (entity.Position + Features.Current.PlayerPhaseInputOffset * DirectionHeld(input)).ToVector2();
        // A sphere meets a box exactly when the box's nearest point is within its radius of its center.
        return entity.Overlapping(new SphereShape3D { Radius = radius }, entity.Position)
            .OfType<Entity>()
            .MinBy(other =>
            {
                Vector2 apart = ((from - other.Position.ToVector2()).Abs() - other.HalfSize).Max(Vector2.Zero);
                return apart.LengthSquared();
            });
    }

    private void ApplyRun(WalkerEntity entity, float move, float weight)
    {
        Features features = Features.Current;

        // Speed is relative to whatever the entity stands on, so running on a robot works like running on tiles.
        float speed = entity.GroundSpeed;
        bool reverse = speed != 0 && move * speed <= 0;

        float drive = 0;
        if (move != 0)
        {
            // The drive fades out as the entity nears top speed in the input direction.
            // It never slows an entity that is already faster than that.
            float headroom = Math.Clamp(1 - speed * MathF.Sign(move) / features.PlayerTopSpeed, 0, 1);
            float driveForce =
                _groundTicksLeft > 0
                    ? (reverse ? features.PlayerGroundReverseDriveForce : features.PlayerGroundForwardDriveForce)
                    : (reverse ? features.PlayerAirReverseDriveForce : features.PlayerAirForwardDriveForce);
            drive = MathF.Abs(move) * driveForce * headroom;
        }

        if (entity.OnGround)
        {
            // On the ground the drive is traction: the engine applies it, and pushes back on the support.
            // Friction adds to it unless the entity is driving the way it is already going, so it holds an
            // idle entity still on its support, and can stop a moving one but can't reverse it.
            float friction = move == 0 || reverse ? features.PlayerGroundFriction * weight : 0;
            entity.ApplyTraction(MathF.Sign(move) * features.PlayerTopSpeed, drive + friction);
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

        if (!_jumpWasHeld && _groundTicksLeft > 0 && _thrustTicksLeft == 0)
        {
            // The jump speed is relative to the ground, as it moved on the last tick on it: whatever the entity
            // gained or lost against it since, from falling in coyote ticks or being pushed off it, is undone.
            var relativeVelocityY = entity.LinearVelocity.Y - _lastGroundVelocity.Y;
            var impulse = new Vector3(0, entity.Mass * (Features.Current.PlayerJumpSpeed - relativeVelocityY), 0);
            entity.ApplyCentralImpulse(impulse);
            entity.Support?.ApplyCentralImpulse(-impulse);
            _groundTicksLeft = 0;
            _thrustTicksLeft = Features.Current.PlayerJumpThrustTicks + 1;

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
            var force = new Vector3(0, Features.Current.PlayerJumpThrustFrac * weight, 0);
            entity.ApplyCentralForce(force);
            entity.Support?.ApplyCentralForce(-force);
        }
    }
}
