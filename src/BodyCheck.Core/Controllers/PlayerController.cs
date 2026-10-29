using BodyCheck.Core.Entities;

using Godot;

namespace BodyCheck.Core.Controllers;

/// <param name="XAxis">Horizontal input, from -1 (left) to 1 (right).</param>
/// <param name="YAxis">Vertical input, from -1 (down) to 1 (up).</param>
public readonly record struct PlayerInput(
    float XAxis,
    float YAxis,
    bool JumpHeld,
    bool PhaseHeld = false,
    bool SpawnHeld = false)
{
    public bool CanLook => YAxis != 0 && XAxis == 0 && !JumpHeld && !PhaseHeld && !SpawnHeld;
}

/// <summary>
/// Turns player input into forces, impulses and traction on the entity it controls.
/// </summary>
public sealed class PlayerController : IController<PlayerEntity>
{
    // How far, from 0 to 1, the stick must be pushed for one of the eight directions to count as held.
    private const float DirectionDeadZone = 0.5f;

    private readonly PlayerEntity _entity;
    private readonly Func<PlayerInput> _input;
    private readonly Entity _block;

    // Timers count down in game time, except where they say otherwise.
    private bool _jumpWasHeld;
    // In real time: how much longer the player counts as on the ground, so that jumping just after running off an edge
    // jumps.
    private float _groundTimeLeft;
    private Vector3 _lastGroundVelocity = Vector3.Zero;
    private float _thrustTimeLeft;
    // In real time: how much longer the last press of jump counts, so that pressing it just before landing jumps.
    private float _jumpBufferLeft;
    private float _phaseCooldownLeft;
    // In real time: how much longer the last direction held while a phase is aimed still counts, once let go of.
    private float _phaseDirectionTimeLeft;
    private bool _spawnWasHeld;
    private float _spawnCooldownLeft;

    // The current time in the air, for telemetry: where the feet were on the last tick on the ground.
    private bool _inAir;
    private Vector2 _takeOffFeet;
    private float _highestFeet;
    private float _airTime;

    /// <param name="entity">The player, which the controller controls.</param>
    /// <param name="input">Supplies the player's input, once per tick.</param>
    /// <param name="block">
    /// The player's block, which it spawns. Until then it is disabled and hidden, so that it is out of the world.
    /// </param>
    public PlayerController(PlayerEntity entity, Func<PlayerInput> input, Entity block)
    {
        _entity = entity;
        _input = input;
        _block = block;
    }

    public PlayerEntity Entity => _entity;

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
    public void ApplyPaused(float realDt, IDefer defer)
    {
        PlayerInput input = _input();
        ApplyPhase(input, 0, realDt, defer);
        _jumpWasHeld = input.JumpHeld;
        _spawnWasHeld = input.SpawnHeld;
    }

    public void Apply(float gameDt, float realDt, IDefer defer)
    {
        PlayerInput input = _input();
        float weight = _entity.Weight;
        float move = Math.Clamp(input.XAxis, -1, 1);

        if (_entity.OnGround)
        {
            _groundTimeLeft = Features.TicksToTime(Features.Current.PlayerCoyoteTicks + 1); // +1 to include this tick
            _lastGroundVelocity = _entity.GroundVelocity;
        }

        if (input.JumpHeld && !_jumpWasHeld)
        {
            _jumpBufferLeft = Features.TicksToTime(Features.Current.PlayerJumpBufferTicks);
        }

        if (!ApplyPhase(input, gameDt, realDt, defer))
        {
            ApplySpawn(input, defer);

            // A press buffered in the air jumps on landing even if jump has been let go of since, as a tap.
            bool jumps = _jumpBufferLeft > 0 && _groundTimeLeft > 0 && _thrustTimeLeft == 0;
            Vector3 runDeltaV = ApplyRun(move, gameDt, jumps);
            Vector3 jumpDeltaV = ApplyJump(input.JumpHeld, weight, gameDt, jumps);

            // Predicted player velocity after the engine integrates forces this tick. Used for corner correction.
            Vector3 gravDeltaV = _entity.GetGravity() * gameDt;
            Vector3 dragDeltaV = -_entity.Tuning.Drag * _entity.LinearVelocity * gameDt / _entity.Mass;
            Vector3 predictedVelocity = _entity.LinearVelocity + runDeltaV + jumpDeltaV + gravDeltaV + dragDeltaV;
            Telemetry.Report(Metric.PredictedVelocity, predictedVelocity.ToVector2());

            // If rising into the bottom of a tile, possibly nudge the player left or right to clear a corner.
            bool nudged = CornerCorrection.UpwardCorrect(
                entity: _entity,
                velocity: predictedVelocity,
                gameDt: gameDt,
                maxNudge: Features.Current.PlayerUpwardCornerCorrection,
                maxAngle: Features.Current.PlayerUpwardCornerCorrectionAngle,
                defer: defer);

            // When moving horizontally and hasn't been nudged yet, possibly nudge the player up or down to clear
            // ledges, run over gaps in the floor, or climb steps.
            if (!nudged)
            {
                CornerCorrection.ForwardCorrect(
                    entity: _entity,
                    velocity: predictedVelocity,
                    gameDt: gameDt,
                    maxNudge: Features.Current.PlayerForwardCornerCorrection,
                    maxAngle: Features.Current.PlayerForwardCornerCorrectionAngle,
                    defer: defer);
            }
        }

        // Set animation variables
        _entity.IsGrounded = _groundTimeLeft > 0;
        _entity.IsMoving = move != 0;
        _entity.IsJumping = _thrustTimeLeft > 0;
        // Face forward when in the air; otherwise use movement direction.
        _entity.Facing = _entity.IsGrounded ? Math.Sign(move) : 0;

        Report(gameDt);

        _groundTimeLeft = Math.Max(0, _groundTimeLeft - realDt);
        _thrustTimeLeft = Math.Max(0, _thrustTimeLeft - gameDt);
        _jumpBufferLeft = Math.Max(0, _jumpBufferLeft - realDt);
        _jumpWasHeld = input.JumpHeld;
        _phaseCooldownLeft = Math.Max(0, _phaseCooldownLeft - gameDt);
        _spawnWasHeld = input.SpawnHeld;
        _spawnCooldownLeft = Math.Max(0, _spawnCooldownLeft - gameDt);
    }

    /// <summary>Reports the player's measurements to <see cref="Telemetry"/>, as of the engine's last step.</summary>
    private void Report(float gameDt)
    {
        var feet = new Vector2(_entity.Position.X, _entity.Position.Y - _entity.HalfSize.Y);
        var velocity = new Vector2(_entity.LinearVelocity.X, _entity.LinearVelocity.Y);
        Telemetry.Report(Metric.Feet, feet);
        Telemetry.Report(Metric.Velocity, velocity);
        Telemetry.Report(Metric.GroundSpeed, _entity.GroundSpeed);
        Telemetry.Report(Metric.OnGround, _entity.OnGround);
        Telemetry.Report(Metric.CoyoteTime, _entity.OnGround ? 0 : _groundTimeLeft);
        Telemetry.Report(Metric.ThrustTime, _thrustTimeLeft);
        Telemetry.Report(Metric.JumpBufferTime, _jumpBufferLeft);

        if (!_entity.OnGround)
        {
            if (!_inAir)
            {
                _inAir = true;
                _highestFeet = _takeOffFeet.Y;
                _airTime = 0;
            }
            _highestFeet = MathF.Max(_highestFeet, feet.Y);
            _airTime += gameDt;
            return;
        }

        if (_inAir)
        {
            _inAir = false;
            Telemetry.Report(Metric.AirRise, _highestFeet - _takeOffFeet.Y);
            Telemetry.Report(Metric.AirRun, MathF.Abs(feet.X - _takeOffFeet.X));
            Telemetry.Report(Metric.AirLand, feet.Y - _takeOffFeet.Y);
            Telemetry.Report(Metric.AirTime, _airTime);
        }

        // The engine still reports the floor contact for a tick after a jump, when the feet have already
        // risen. Thrust time counts from the jump, so it marks it.
        if (_thrustTimeLeft == 0)
        {
            _takeOffFeet = feet;
        }
    }

    /// <summary>
    /// Begins, holds, aims and releases a phase. Returns whether the phase takes the place of running and jumping on
    /// this tick: while it is aimed, and on the tick it is made, when the bodies move after the entity steps and the
    /// contacts are from before it.
    /// </summary>
    /// <param name="gameDt">The tick's length in game time: 0 while the game is paused.</param>
    /// <param name="realDt">The tick's length in real time.</param>
    /// <param name="defer">An object that allows running actions later in the tick.</param>
    private bool ApplyPhase(PlayerInput input, float gameDt, float realDt, IDefer defer)
    {
        if (Phase is null && input.PhaseHeld && _phaseCooldownLeft <= 0 && TargetedEntity(input) is Entity target)
        {
            Phase = new PhaseAction(
                player: _entity,
                target: target,
                gap: Features.Current.PlayerPhaseGap,
                phaseThrow: Features.Current.PlayerPhaseThrow,
                speed: Features.Current.PlayerPhaseSpeed,
                defer: defer);

            _phaseDirectionTimeLeft = 0;
            _entity.RaiseCue(EntityCue.PhaseBegan);
        }

        if (Phase is null)
        {
            return false;
        }

        // The direction held, or the last one held if it was let go of only just now, so that letting go of it just
        // before phase, or with it, still phases.
        Vector3 held = DirectionHeld(input);
        if (held != Vector3.Zero)
        {
            Phase.Direction = held;
            _phaseDirectionTimeLeft = Features.TicksToTime(Features.Current.PlayerPhaseReleaseTicks + 1); // +1 to include this tick
        }
        else
        {
            _phaseDirectionTimeLeft = Math.Max(0, _phaseDirectionTimeLeft - realDt);
            if (_phaseDirectionTimeLeft == 0)
            {
                Phase.Direction = Vector3.Zero;
            }
        }

        if (input.PhaseHeld)
        {
            // Only on ticks of play: while paused, nothing drifts, and the velocities read are from the engine's last
            // step, so every grab would add the same impulses again. And not on the release, so that the phase reads
            // the velocities it replaces.
            if (gameDt > 0)
            {
                Phase.Grab(gameDt);
            }
            return true;
        }

        bool phased = Phase.Direction != Vector3.Zero;
        if (phased)
        {
            Phase.Phase(defer);
            _phaseCooldownLeft = Features.Current.PlayerPhaseThrowCooldownTime;
            _entity.RaiseCue(EntityCue.PhaseThrown);
        }
        else
        {
            _phaseCooldownLeft = Features.Current.PlayerPhaseCancelCooldownTime;
            _entity.RaiseCue(EntityCue.PhaseCancelled);
        }

        Phase.Stop(defer);
        Phase = null;

        // A phase ends a jump's thrust, coyote time and buffered press, and so does cancelling one: the aim took them.
        _groundTimeLeft = 0;
        _thrustTimeLeft = 0;
        _jumpBufferLeft = 0;
        return phased;
    }

    /// <summary>
    /// Spawns the player's block under the player, on a press of spawn, after the cooldown. The block goes at the
    /// player's height, as near its center as it fits beside the tiles, and as far straight down from there, through
    /// entities, as it can go until it is under the player, or meets a tile. Then the player moves up through entities by
    /// as much as the block fell short, onto it: on the ground, by the block's height, and in the open air, not at all.
    /// Where the block doesn't fit, or the player has no room to rise onto it, the spawn is cancelled. There is only one block, so spawning it
    /// takes it from wherever it was. The moves wait until after the entity steps, like a phase's.
    /// </summary>
    private void ApplySpawn(PlayerInput input, IDefer defer)
    {
        if (!input.SpawnHeld || _spawnWasHeld || _spawnCooldownLeft > 0)
        {
            return;
        }

        if (SpawnPlace() is not (Vector3 place, float rise))
        {
            _spawnCooldownLeft = Features.Current.PlayerSpawnCancelCooldownTime;
            _entity.RaiseCue(EntityCue.SpawnCancelled);
            return;
        }
        _spawnCooldownLeft = Features.Current.PlayerSpawnCooldownTime;

        Vector3 velocity = Features.Current.PlayerSpawnMotion == SpawnMotion.WithPlayer ? _entity.LinearVelocity : Vector3.Zero;
        Vector3 center = _entity.Position;
        defer.RunAfterEntitySteps(() =>
        {
            // A new block, as far as anyone can tell: back in the world, level, and moving only as the spawn says.
            _block.ProcessMode = Node.ProcessModeEnum.Inherit;
            _block.Visible = true;
            _block.GlobalTransform = Transform3D.Identity;
            _block.LinearVelocity = velocity;
            _block.AngularVelocity = Vector3.Zero;
            _block.MoveTo(place);
            if (rise > 0)
            {
                _entity.MoveTo(center + new Vector3(0, rise, 0));
            }
            _block.RaiseCue(EntityCue.Spawned);
        });
    }

    /// <summary>
    /// Where the block goes, and how far the player rises onto it, or null if it doesn't fit beside the tiles or the
    /// player has no room to rise onto it. Found by casting boxes against tiles, as of the engine's last step, each
    /// from where it is clear of them, so that no move starts inside a tile.
    /// </summary>
    private (Vector3 Place, float Rise)? SpawnPlace()
    {
        Vector2 playerHalf = _entity.HalfSize;
        Vector2 blockHalf = _block.HalfSize;
        Vector3 center = _entity.Position;

        // A box of the player's width and the block's height, at the player's center, is inside the player, so clear of
        // tiles. How far it can move to each side is the room the block has there. The block is wider than the player
        // by `extra`, so it fits where the room on both sides adds up to that, and starts as near the player's center as
        // it fits. The casts may come up short by the contact tolerance, so a block that only just fits, as in a shaft
        // one tile wide, counts as fitting, and starts in the middle of the room.
        float extra = 2 * (blockHalf.X - playerHalf.X);
        var side = new Vector2(playerHalf.X, blockHalf.Y);
        float left = _entity.RoomToMove(side, center, Vector3.Left, extra, PhysicsLayer.Static);
        float right = _entity.RoomToMove(side, center, Vector3.Right, extra, PhysicsLayer.Static);
        if (left + right < extra - Entities.Entity.ContactTolerance)
        {
            return null;
        }
        // Not Math.Clamp, which throws if rounding puts the bounds the wrong way round when the block only just fits.
        float shift = left + right < extra
            ? (right - left) / 2
            : MathF.Min(MathF.Max(0, extra / 2 - left), right - extra / 2);
        Vector3 start = center + new Vector3(shift, 0, 0);

        // From there, the block falls until it is under the player, or meets a tile. The player rises by as much as it
        // falls short, which needs that much room above the player.
        float drop = playerHalf.Y + blockHalf.Y;
        float fall = _entity.RoomToMove(blockHalf, start, Vector3.Down, drop, PhysicsLayer.Static);
        float rise = drop - fall;
        if (rise > 0 && _entity.RoomToMove(playerHalf, center, Vector3.Up, rise, PhysicsLayer.Static) < rise)
        {
            return null;
        }
        return (start - new Vector3(0, fall, 0), rise);
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
    private Entity? TargetedEntity(PlayerInput input)
    {
        float radius = _entity.HalfSize.Length() + Features.Current.PlayerPhaseReach;
        Vector2 from = (_entity.Position + Features.Current.PlayerPhaseInputOffset * DirectionHeld(input)).ToVector2();
        // A sphere meets a box exactly when the box's nearest point is within its radius of its center.
        return _entity.Overlapping(new SphereShape3D { Radius = radius }, _entity.Position)
            .OfType<Entity>()
            .MinBy(other =>
            {
                Vector2 apart = ((from - other.Position.ToVector2()).Abs() - other.HalfSize).Max(Vector2.Zero);
                return apart.LengthSquared();
            });
    }

    /// <param name="jumps">Whether the entity jumps on this tick.</param>
    private Vector3 ApplyRun(float move, float gameDt, bool jumps)
    {
        Features features = Features.Current;
        (Vector3 deltaV, bool slips) = RunAction.Run(
            entity: _entity,
            gameDt: gameDt,
            move: move,
            jumps: jumps,
            topSpeed: features.PlayerTopSpeed,
            driveForce: _groundTimeLeft > 0 ? features.PlayerGroundDriveForce : features.PlayerAirDriveForce,
            brakeForce: _groundTimeLeft > 0 ? features.PlayerGroundBrakeForce : features.PlayerAirBrakeForce);
        if (slips)
        {
            _entity.SlipAfterEntitySteps();
        }
        return deltaV;
    }

    /// <returns>
    /// The change in the entity's velocity from the jump's impulse or thrust, which shows only after the engine's step.
    /// What the entity carries is pushed up with it, so it shares the change.
    /// </returns>
    /// <param name="jumps">Whether the entity jumps on this tick.</param>
    private Vector3 ApplyJump(bool jumpHeld, float weight, float gameDt, bool jumps)
    {
        if (jumps)
        {
            // The jump speed is relative to the ground, as it moved on the last tick on it: whatever the entity
            // gained or lost against it since, from falling in coyote ticks or being pushed off it, is undone.
            var relativeVelocityY = _entity.LinearVelocity.Y - _lastGroundVelocity.Y;
            var impulse = new Vector3(0, _entity.Mass * (Features.Current.PlayerJumpSpeed - relativeVelocityY), 0);
            _entity.ApplyCentralImpulse(impulse);
            _entity.Support?.ApplyCentralImpulse(-impulse);
            _groundTimeLeft = 0;
            _jumpBufferLeft = 0;
            _thrustTimeLeft = Features.Current.PlayerJumpThrustTime + gameDt; // + gameDt to include this tick

            _entity.RaiseCue(EntityCue.Jumped);

            // Thrust starts on the next tick.
            return impulse / _entity.LoadedMass;
        }

        if (!jumpHeld)
        {
            return Vector3.Zero;
        }

        // If stopped rising (e.g. hit a ceiling), cancel thrust.
        if (_entity.LinearVelocity.Y <= 0)
        {
            _thrustTimeLeft = 0;
        }

        if (_thrustTimeLeft > 0)
        {
            // The last tick of thrust gets only the part of it that the thrust time covers, so that the thrust adds the
            // same speed whatever the ticks' length in game time.
            float share = Math.Min(1, _thrustTimeLeft / gameDt);
            var force = new Vector3(0, share * Features.Current.PlayerJumpThrustFrac * weight, 0);
            _entity.ApplyCentralForce(force);
            _entity.Support?.ApplyCentralForce(-force);
            return force * gameDt / _entity.LoadedMass;
        }
        return Vector3.Zero;
    }
}
