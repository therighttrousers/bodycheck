using BodyCheck.Core.Entities;

using Godot;

namespace BodyCheck.Core.Controllers;

/// <param name="XAxis">Horizontal input, from -1 (left) to 1 (right).</param>
/// <param name="YAxis">Vertical input, from -1 (down) to 1 (up).</param>
public readonly record struct PlayerInput(float XAxis, float YAxis, bool JumpHeld, bool PhaseHeld = false)
{
    public bool CanLook => YAxis != 0 && XAxis == 0 && !JumpHeld;
}

/// <summary>What momentum a phase gives the entity phased through.</summary>
public enum PhaseThrow
{
    /// <summary>The opposite of the player's new momentum, so every phase throws the same.</summary>
    Fixed,
    /// <summary>The opposite of the player's change in momentum, so momentum is conserved.</summary>
    Conserved,
}

public sealed record PlayerTuning
{
    /// <summary>Speed at which the run drive force falls to zero.</summary>
    public float TopSpeed { get; init; } = 12;

    /// <summary>Force applied when on the ground and moving in the direction of input.</summary>
    public float GroundForwardDriveForce { get; init; } = 67.5f;
    /// <summary>Force applied when on the ground and moving against the direction of input.</summary>
    public float GroundReverseDriveForce { get; init; } = 135;
    /// <summary>Force applied when in the air and moving in the direction of input.</summary>
    public float AirForwardDriveForce { get; init; } = 22.5f;
    /// <summary>Force applied when in the air and moving against the direction of input.</summary>
    public float AirReverseDriveForce { get; init; } = 45;

    /// <summary>
    /// Coefficient of friction with the ground, whether tiles or another body. Applied when not driving in
    /// the direction of travel.
    /// </summary>
    public float GroundFriction { get; init; } = 4;

    /// <summary>Upward speed given by the jump impulse.</summary>
    public float JumpSpeed { get; init; } = 12.7f;

    /// <summary>
    /// Number of frames the jump thrust lasts while the button is held. Includes the frame with the initial jump impulse.
    /// </summary>
    public int JumpThrustTicks { get; init; } = 14;

    /// <summary>Jump thrust as a fraction of the player's weight. At 1 it cancels gravity.</summary>
    public float JumpThrustFrac { get; init; } = 0.9f;

    /// <summary>
    /// Number of frames the player gets to jump, or turn around with ground thrust, *after* running off an edge.
    /// </summary>
    public int CoyoteTicks { get; init; } = 4;

    /// <summary>How far the box of the entity phased through may be from the player's box.</summary>
    public float PhaseReach { get; init; } = 1;

    /// <summary>
    /// How far apart the player's box and the box of the entity phased through end up, along the phase's direction.
    /// </summary>
    public float PhaseGap { get; init; } = 0.5f;

    /// <summary>The least distance between the centers of the player and the entity phased through, after a phase.</summary>
    public float PhaseMinSeparation { get; init; } = 2.5f;

    /// <summary>How far the player and the entity phased through may slide, as a fraction of their separation, to fit.</summary>
    public float PhaseMaxSlide { get; init; } = 1;

    /// <summary>The speed the player leaves a phase with.</summary>
    public float PhaseSpeed { get; init; } = 16;

    public PhaseThrow PhaseThrow { get; init; } = PhaseThrow.Fixed;
}

/// <summary>
/// Turns player input into forces, impulses and traction on the entity it controls.
/// </summary>
public sealed class PlayerController : ICharacterController<WalkerEntity>
{
    private readonly Func<PlayerInput> _input;

    private bool _jumpWasHeld;
    private bool _phaseWasHeld;
    private int _groundTicksLeft;
    private Vector3 _lastGroundVelocity = Vector3.Zero;
    private int _thrustTicksLeft;

    // The current time in the air, for telemetry: where the feet were on the last tick on the ground.
    private bool _inAir;
    private Vector2 _takeOffFeet;
    private float _highestFeet;
    private int _airTicks;

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
        float move = Math.Clamp(input.XAxis, -1, 1);

        if (entity.OnGround)
        {
            _groundTicksLeft = Tuning.CoyoteTicks + 1; // +1 to include this frame in remaining ground ticks
            _lastGroundVelocity = entity.GroundVelocity;
        }

        if (input.PhaseHeld && !_phaseWasHeld && TryPhase(entity, input))
        {
            _groundTicksLeft = 0;
            _thrustTicksLeft = 0;
        }
        else
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
        _phaseWasHeld = input.PhaseHeld;
    }

    /// <summary>
    /// Phases through the nearest entity in reach, if they fit: the player and the entity teleport as a pair,
    /// keeping their center of mass, onto the line through it in the direction of input, the player ahead of it and
    /// the entity behind. If they don't fit there, they slide along the line, the nearest first. The player leaves
    /// at a fixed speed in that direction, and the entity is thrown the other way.
    /// </summary>
    /// <returns>Whether the player phased.</returns>
    private bool TryPhase(WalkerEntity entity, PlayerInput input)
    {
        Entity? target = entity.NearestEntity(Tuning.PhaseReach);
        if (target is null)
        {
            return false;
        }

        // With no direction held, the other side is straight through the target.
        var held = new Vector2(input.XAxis, input.YAxis);
        Vector2 direction = EightWay(held.Length() >= 0.5f
            ? held
            : new Vector2(target.Position.X - entity.Position.X, target.Position.Y - entity.Position.Y));
        var direction3 = new Vector3(direction.X, direction.Y, 0);

        // Each moves in proportion to the other's mass.
        float totalMass = entity.Mass + target.Mass;
        Vector3 center = (entity.Mass * entity.Position + target.Mass * target.Position) / totalMass;
        float separation = Separation(entity, target, direction);
        Vector3 playerOffset = target.Mass / totalMass * separation * direction3;
        Vector3 targetOffset = -entity.Mass / totalMass * separation * direction3;

        // The nearest slide first: 0, +1, -1, +2, -2, ... steps.
        const float SlideStep = 1f / 16;
        int slides = (int)(Tuning.PhaseMaxSlide * separation / SlideStep);
        for (int i = 0; i <= 2 * slides; i++)
        {
            float slide = (i + 1) / 2 * (i % 2 == 1 ? 1 : -1) * SlideStep;
            Vector3 playerDestination = center + slide * direction3 + playerOffset;
            Vector3 targetDestination = center + slide * direction3 + targetOffset;
            if (entity.CanTeleport(playerDestination, target) && target.CanTeleport(targetDestination, entity))
            {
                Phase(entity, target, playerDestination, targetDestination, direction3);
                if (direction.X != 0)
                {
                    entity.Facing = MathF.Sign(direction.X);
                }
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// How far apart the centers of the player and the target end up: enough to clear their boxes in the
    /// direction of the phase, plus a gap, but at least the minimum.
    /// </summary>
    private float Separation(Entity entity, Entity target, Vector2 direction)
    {
        Vector2 halfSizes = entity.HalfSize + target.HalfSize;
        // Along a diagonal, the boxes clear when they clear on either axis, whichever comes first.
        float clear = MathF.Min(
            direction.X == 0 ? float.PositiveInfinity : halfSizes.X / MathF.Abs(direction.X),
            direction.Y == 0 ? float.PositiveInfinity : halfSizes.Y / MathF.Abs(direction.Y));
        return MathF.Max(clear + Tuning.PhaseGap, Tuning.PhaseMinSeparation);
    }

    private void Phase(Entity entity, Entity target, Vector3 playerDestination, Vector3 targetDestination, Vector3 direction)
    {
        entity.Teleport(playerDestination);
        target.Teleport(targetDestination);

        Vector3 momentum = entity.Mass * Tuning.PhaseSpeed * direction;
        Vector3 impulse = momentum - entity.Mass * entity.LinearVelocity;
        entity.ApplyCentralImpulse(impulse);
        target.ApplyCentralImpulse(Tuning.PhaseThrow switch
        {
            PhaseThrow.Fixed => -momentum,
            PhaseThrow.Conserved => -impulse,
        });
    }

    /// <summary>The nearest of the eight directions to <paramref name="direction"/>, whatever its length, as a unit vector.</summary>
    private static Vector2 EightWay(Vector2 direction)
    {
        const float Step = MathF.PI / 4;
        float angle = MathF.Round(direction.Angle() / Step) * Step;
        // Rounded, so that the axes are exactly zero.
        return new Vector2(MathF.Round(MathF.Cos(angle), 6), MathF.Round(MathF.Sin(angle), 6));
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

        if (!_jumpWasHeld && _groundTicksLeft > 0 && _thrustTicksLeft == 0)
        {
            // The jump speed is relative to the ground, as it moved on the last tick on it: whatever the entity
            // gained or lost against it since, from falling in coyote ticks or being pushed off it, is undone.
            var relativeVelocityY = entity.LinearVelocity.Y - _lastGroundVelocity.Y;
            var impulse = new Vector3(0, entity.Mass * (Tuning.JumpSpeed - relativeVelocityY), 0);
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
