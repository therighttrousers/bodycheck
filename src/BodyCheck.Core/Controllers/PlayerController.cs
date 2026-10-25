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
public sealed class PlayerController : ICharacterController<WalkerEntity>
{
    // How far, from 0 to 1, the stick must be pushed for one of the eight directions to count as held.
    private const float DirectionDeadZone = 0.5f;

    private readonly Func<PlayerInput> _input;
    private readonly Entity _block;

    // Timers count down in game time, except where they say otherwise.
    private bool _jumpWasHeld;
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

    /// <param name="input">Supplies the player's input, once per tick.</param>
    /// <param name="block">
    /// The player's block, which it spawns. Until then it is disabled and hidden, so that it is out of the world.
    /// </param>
    public PlayerController(Func<PlayerInput> input, Entity block)
    {
        _input = input;
        _block = block;
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
    public void ApplyPaused(WalkerEntity entity, float realDt, IDefer defer)
    {
        PlayerInput input = _input();
        ApplyPhase(entity, input, 0, realDt, defer);
        _jumpWasHeld = input.JumpHeld;
        _spawnWasHeld = input.SpawnHeld;
    }

    public void Apply(WalkerEntity entity, float gameDt, float realDt, IDefer defer)
    {
        PlayerInput input = _input();
        float weight = entity.Weight;
        float move = Math.Clamp(input.XAxis, -1, 1);

        if (entity.OnGround)
        {
            _groundTimeLeft = Features.TicksToTime(Features.Current.PlayerCoyoteTicks + 1); // +1 to include this tick
            _lastGroundVelocity = entity.GroundVelocity;
        }

        if (input.JumpHeld && !_jumpWasHeld)
        {
            _jumpBufferLeft = Features.TicksToTime(Features.Current.PlayerJumpBufferTicks);
        }

        if (!ApplyPhase(entity, input, gameDt, realDt, defer))
        {
            ApplySpawn(entity, input, defer);

            // A press buffered in the air jumps on landing even if jump has been let go of since, as a tap.
            bool jumps = _jumpBufferLeft > 0 && _groundTimeLeft > 0 && _thrustTimeLeft == 0;
            ApplyRun(entity, move, gameDt, jumps);
            ApplyJump(entity, input.JumpHeld, weight, jumps);
        }

        // Set animation variables
        entity.IsGrounded = _groundTimeLeft > 0;
        entity.IsMoving = move != 0;
        entity.IsJumping = _thrustTimeLeft > 0;
        // Only change facing direction when on the ground or just after jumping (and allow setting to 0)
        if (_groundTimeLeft > 0 || _thrustTimeLeft > 0)
        {
            entity.Facing = Math.Sign(move);
        }

        Report(entity, gameDt);

        _groundTimeLeft = Math.Max(0, _groundTimeLeft - gameDt);
        _thrustTimeLeft = Math.Max(0, _thrustTimeLeft - gameDt);
        _jumpBufferLeft = Math.Max(0, _jumpBufferLeft - realDt);
        _jumpWasHeld = input.JumpHeld;
        _phaseCooldownLeft = Math.Max(0, _phaseCooldownLeft - gameDt);
        _spawnWasHeld = input.SpawnHeld;
        _spawnCooldownLeft = Math.Max(0, _spawnCooldownLeft - gameDt);
    }

    /// <summary>Reports the player's measurements to <see cref="Telemetry"/>, as of the engine's last step.</summary>
    private void Report(WalkerEntity entity, float gameDt)
    {
        var feet = new Vector2(entity.Position.X, entity.Position.Y - entity.HalfSize.Y);
        var velocity = new Vector2(entity.LinearVelocity.X, entity.LinearVelocity.Y);
        Telemetry.Report(Metric.Feet, feet);
        Telemetry.Report(Metric.Velocity, velocity);
        Telemetry.Report(Metric.GroundSpeed, entity.GroundSpeed);
        Telemetry.Report(Metric.OnGround, entity.OnGround);
        Telemetry.Report(Metric.CoyoteTime, entity.OnGround ? 0 : _groundTimeLeft);
        Telemetry.Report(Metric.ThrustTime, _thrustTimeLeft);
        Telemetry.Report(Metric.JumpBufferTime, _jumpBufferLeft);

        if (!entity.OnGround)
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
    private bool ApplyPhase(WalkerEntity entity, PlayerInput input, float gameDt, float realDt, IDefer defer)
    {
        if (Phase is null && input.PhaseHeld && _phaseCooldownLeft <= 0 && TargetedEntity(entity, input) is Entity target)
        {
            Phase = new PhaseAction(
                player: entity,
                target: target,
                gap: Features.Current.PlayerPhaseGap,
                phaseThrow: Features.Current.PlayerPhaseThrow,
                speed: Features.Current.PlayerPhaseSpeed,
                defer: defer);

            _phaseDirectionTimeLeft = 0;
            entity.RaiseCue(EntityCue.PhaseBegan);
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
            entity.RaiseCue(EntityCue.PhaseThrown);
        }
        else
        {
            _phaseCooldownLeft = Features.Current.PlayerPhaseCancelCooldownTime;
            entity.RaiseCue(EntityCue.PhaseCancelled);
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
    private void ApplySpawn(WalkerEntity entity, PlayerInput input, IDefer defer)
    {
        if (!input.SpawnHeld || _spawnWasHeld || _spawnCooldownLeft > 0)
        {
            return;
        }

        if (SpawnPlace(entity) is not (Vector3 place, float rise))
        {
            _spawnCooldownLeft = Features.Current.PlayerSpawnCancelCooldownTime;
            entity.RaiseCue(EntityCue.SpawnCancelled);
            return;
        }
        _spawnCooldownLeft = Features.Current.PlayerSpawnCooldownTime;

        Vector3 velocity = Features.Current.PlayerSpawnMotion == SpawnMotion.WithPlayer ? entity.LinearVelocity : Vector3.Zero;
        Vector3 center = entity.Position;
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
                entity.MoveTo(center + new Vector3(0, rise, 0));
            }
            _block.RaiseCue(EntityCue.Spawned);
        });
    }

    /// <summary>
    /// Where the block goes, and how far the player rises onto it, or null if it doesn't fit beside the tiles or the
    /// player has no room to rise onto it. Found by casting boxes against tiles, as of the engine's last step, each
    /// from where it is clear of them, so that no move starts inside a tile.
    /// </summary>
    private (Vector3 Place, float Rise)? SpawnPlace(WalkerEntity entity)
    {
        Vector2 playerHalf = entity.HalfSize;
        Vector2 blockHalf = _block.HalfSize;
        Vector3 center = entity.Position;

        // A box of the player's width and the block's height, at the player's center, is inside the player, so clear of
        // tiles. How far it can move to each side is the room the block has there. The block is wider than the player
        // by `extra`, so it fits where the room on both sides adds up to that, and starts as near the player's center as
        // it fits. The casts may come up short by the contact tolerance, so a block that only just fits, as in a shaft
        // one tile wide, counts as fitting, and starts in the middle of the room.
        float extra = 2 * (blockHalf.X - playerHalf.X);
        var side = new Vector2(playerHalf.X, blockHalf.Y);
        float left = entity.RoomToMove(side, center, Vector3.Left, extra, PhysicsLayer.Static);
        float right = entity.RoomToMove(side, center, Vector3.Right, extra, PhysicsLayer.Static);
        if (left + right < extra - Entity.ContactTolerance)
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
        float fall = entity.RoomToMove(blockHalf, start, Vector3.Down, drop, PhysicsLayer.Static);
        float rise = drop - fall;
        if (rise > 0 && entity.RoomToMove(playerHalf, center, Vector3.Up, rise, PhysicsLayer.Static) < rise)
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

    /// <param name="jumps">Whether the entity jumps on this tick.</param>
    private void ApplyRun(WalkerEntity entity, float move, float gameDt, bool jumps)
    {
        Features features = Features.Current;
        RunAction.Run(
            entity: entity,
            gameDt: gameDt,
            move: move,
            jumps: jumps,
            topSpeed: features.PlayerTopSpeed,
            driveForce: _groundTimeLeft > 0 ? features.PlayerGroundDriveForce : features.PlayerAirDriveForce,
            brakeForce: _groundTimeLeft > 0 ? features.PlayerGroundBrakeForce : features.PlayerAirBrakeForce);
    }

    /// <param name="jumps">Whether the entity jumps on this tick.</param>
    private void ApplyJump(WalkerEntity entity, bool jumpHeld, float weight, bool jumps)
    {
        if (jumps)
        {
            // The jump speed is relative to the ground, as it moved on the last tick on it: whatever the entity
            // gained or lost against it since, from falling in coyote ticks or being pushed off it, is undone.
            var relativeVelocityY = entity.LinearVelocity.Y - _lastGroundVelocity.Y;
            var impulse = new Vector3(0, entity.Mass * (Features.Current.PlayerJumpSpeed - relativeVelocityY), 0);
            entity.ApplyCentralImpulse(impulse);
            entity.Support?.ApplyCentralImpulse(-impulse);
            _groundTimeLeft = 0;
            _jumpBufferLeft = 0;
            _thrustTimeLeft = Features.TicksToTime(Features.Current.PlayerJumpThrustTicks + 1); // +1 to include this tick

            entity.RaiseCue(EntityCue.Jumped);

            // Thrust starts on the next tick.
            return;
        }

        if (!jumpHeld)
        {
            return;
        }

        // If stopped rising (e.g. hit a ceiling), cancel thrust.
        if (entity.LinearVelocity.Y <= 0)
        {
            _thrustTimeLeft = 0;
        }

        if (_thrustTimeLeft > 0)
        {
            var force = new Vector3(0, Features.Current.PlayerJumpThrustFrac * weight, 0);
            entity.ApplyCentralForce(force);
            entity.Support?.ApplyCentralForce(-force);
        }
    }
}
