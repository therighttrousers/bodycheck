using BodyCheck.Core.Entities;

using Godot;

namespace BodyCheck.Core.Controllers;

/// <param name="XAxis">Horizontal input, from -1 (left) to 1 (right).</param>
/// <param name="YAxis">Vertical input, from -1 (down) to 1 (up).</param>
public readonly record struct PlayerInput(float XAxis, float YAxis, bool JumpHeld, bool PhaseHeld = false)
{
    public bool CanLook => YAxis != 0 && XAxis == 0 && !JumpHeld && !PhaseHeld;
}

/// <summary>What momentum a phase gives the entity phased through.</summary>
public enum PhaseThrow
{
    /// <summary>The opposite of the player's new momentum, so every phase throws the same.</summary>
    Fixed,
    /// <summary>The opposite of the player's change in momentum, so momentum is conserved.</summary>
    Conserved,
}

/// <summary>A phase being aimed: its target, fixed when phase is pressed, and the direction held.</summary>
/// <param name="Center">The center of mass of the player and the target, which the phase keeps.</param>
/// <param name="Direction">
/// One of the eight directions, as a unit vector, or zero if none is held. Released with none, the phase is cancelled.
/// </param>
public readonly record struct PhaseAim(Entity Target, Vector3 Center, Vector2 Direction);

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
    public float PhaseReach { get; init; } = 0.25f;

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

    /// <summary>The minimum time between phase uses, according to the game clock.</summary>
    public float PhaseCooldownTime { get; init; } = 0.5f;

    public PhaseThrow PhaseThrow { get; init; } = PhaseThrow.Fixed;
}

/// <summary>
/// Turns player input into forces, impulses and traction on the entity it controls.
/// </summary>
public sealed class PlayerController : ICharacterController<WalkerEntity>
{
    private readonly Func<PlayerInput> _input;

    private bool _jumpWasHeld;
    private float _phaseCooldown;
    // A phase released, to be made on the next tick of play.
    private PhaseAim? _release;
    // Where the target was from the player when the aim began, which the grab holds.
    private Vector3 _grabOffset;
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

    /// <summary>The phase being aimed, from when phase is pressed with a target in reach until it is released.</summary>
    public PhaseAim? Aim { get; private set; }

    /// <summary>The game is paused while a phase is aimed, so that the player can take its time over the direction.</summary>
    public bool RequestsPause => Aim is not null;

    /// <summary>
    /// Aims the phase while the game is paused for it. Releasing phase ends the aim, and the phase happens on the
    /// next tick of play.
    /// </summary>
    public void ApplyPaused(WalkerEntity entity, float dt)
    {
        PlayerInput input = _input();
        UpdateAim(entity, input);
        _jumpWasHeld = input.JumpHeld;
    }

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

        // If nothing pauses the game for the aim, the player aims while it runs.
        if (Aim is null && input.PhaseHeld && _phaseCooldown <= 0 && entity.NearestEntity(Tuning.PhaseReach) is Entity target)
        {
            Aim = new PhaseAim(target, Vector3.Zero, Vector2.Zero);
            _grabOffset = target.Position - entity.Position;
            _phaseCooldown = Tuning.PhaseCooldownTime;
        }
        if (Aim is PhaseAim aim)
        {
            Grab(entity, aim.Target, dt);
        }
        UpdateAim(entity, input);

        if (_release is PhaseAim release && TryPhase(entity, release.Target, release.Direction))
        {
            _groundTicksLeft = 0;
            _thrustTicksLeft = 0;
        }
        else if (Aim is null)
        {
            ApplyRun(entity, move, weight);
            ApplyJump(entity, input.JumpHeld, weight);
        }
        _release = null;

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
        _phaseCooldown = Math.Max(0, _phaseCooldown - dt);
    }

    /// <summary>
    /// Follows the direction held while a phase is aimed, and ends the aim when phase is released: with a direction
    /// held, the phase is made on the next tick of play, and with none, it is cancelled.
    /// </summary>
    private void UpdateAim(WalkerEntity entity, PlayerInput input)
    {
        if (Aim is not PhaseAim aim)
        {
            return;
        }

        var held = new Vector2(input.XAxis, input.YAxis);
        aim = aim with
        {
            Center = CenterOfMass(entity, aim.Target),
            Direction = held.Length() >= 0.5f ? EightWay(held) : Vector2.Zero,
        };
        if (input.PhaseHeld)
        {
            Aim = aim;
            return;
        }

        Aim = null;
        if (aim.Direction != Vector2.Zero)
        {
            _release = aim;
        }
    }

    /// <summary>
    /// Holds the player and the target where they were from each other when the aim began, while they move together
    /// at the velocity of their center of mass, as in a perfectly inelastic collision. Done on every tick of play while
    /// a phase is aimed, so that they keep together while the game slows down to the pause, whatever else pushes on
    /// them, such as gravity on a player beside a hovering platform. Without it, they can drift far apart before the
    /// phase. Momentum is conserved: the impulses on the two are equal and opposite.
    /// </summary>
    private void Grab(Entity entity, Entity target, float dt)
    {
        float totalMass = entity.Mass + target.Mass;
        Vector3 velocity = (entity.Mass * entity.LinearVelocity + target.Mass * target.LinearVelocity) / totalMass;

        // The relative velocity that takes back how far they have drifted, over a step, but never over less than a
        // tick's length at full speed. Steps shrink with the time scale as the game slows down, and dividing by a tiny
        // one would give a velocity large enough to fling them both when play resumes.
        Vector3 drift = target.Position - entity.Position - _grabOffset;
        Vector3 closing = -drift / MathF.Max(dt, 1f / Engine.PhysicsTicksPerSecond);

        entity.ApplyCentralImpulse(entity.Mass * (velocity - target.Mass / totalMass * closing - entity.LinearVelocity));
        target.ApplyCentralImpulse(target.Mass * (velocity + entity.Mass / totalMass * closing - target.LinearVelocity));
    }

    private static Vector3 CenterOfMass(Entity entity, Entity target) =>
        (entity.Mass * entity.Position + target.Mass * target.Position) / (entity.Mass + target.Mass);

    /// <summary>
    /// Phases through the target in a direction, if they fit: the player and the target teleport as a pair, keeping
    /// their center of mass, onto the line through it in that direction, the player ahead of it and the target
    /// behind. If they don't fit there, they slide along the line, the nearest first. The player leaves at a fixed
    /// speed in that direction, and the target is thrown the other way.
    /// </summary>
    /// <returns>Whether the player phased.</returns>
    private bool TryPhase(WalkerEntity entity, Entity target, Vector2 direction)
    {
        var direction3 = new Vector3(direction.X, direction.Y, 0);

        // Each moves in proportion to the other's mass.
        float totalMass = entity.Mass + target.Mass;
        Vector3 center = CenterOfMass(entity, target);
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
        const float step = MathF.PI / 4;
        float angle = MathF.Round(direction.Angle() / step) * step;
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
