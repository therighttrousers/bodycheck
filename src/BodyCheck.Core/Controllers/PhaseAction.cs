using BodyCheck.Core.Entities;

using Godot;

namespace BodyCheck.Core.Controllers;

/// <summary>What a tick of <see cref="PhaseAction.Apply"/> did.</summary>
public enum PhaseOutcome
{
    /// <summary>Nothing: no phase is aimed.</summary>
    Idle,

    /// <summary>A phase is aimed, begun on this tick or before.</summary>
    Aiming,

    /// <summary>The aim was released with a direction, and the phase made. The bodies move after the entity steps.</summary>
    Phased,

    /// <summary>The aim was released with no direction, and nothing moved.</summary>
    Cancelled,
}

/// <summary>
/// How a <see cref="PhaseAction"/> is tuned. Each value is a function that reads it from the features in effect when it
/// is used, so that a change to them takes effect at once. See the player's, in <see cref="Features"/>, for what each
/// means.
/// </summary>
public sealed record PhaseTuning
{
    public required Func<float> Reach { get; init; }
    public required Func<float> InputOffset { get; init; }
    public required Func<float> Gap { get; init; }
    public required Func<float> Speed { get; init; }
    public required Func<PhaseThrow> Throw { get; init; }
    public required Func<int> ReleaseTicks { get; init; }
    public required Func<float> ThrowCooldownTime { get; init; }
    public required Func<float> CancelCooldownTime { get; init; }

    public static PhaseTuning Player { get; } = new()
    {
        Reach = () => Features.Current.PlayerPhaseReach,
        InputOffset = () => Features.Current.PlayerPhaseInputOffset,
        Gap = () => Features.Current.PlayerPhaseGap,
        Speed = () => Features.Current.PlayerPhaseSpeed,
        Throw = () => Features.Current.PlayerPhaseThrow,
        ReleaseTicks = () => Features.Current.PlayerPhaseReleaseTicks,
        ThrowCooldownTime = () => Features.Current.PlayerPhaseThrowCooldownTime,
        CancelCooldownTime = () => Features.Current.PlayerPhaseCancelCooldownTime,
    };
}

/// <summary>
/// Phasing through another entity: choosing it, aiming, and releasing, which phases in the direction held or cancels.
/// A phase being aimed is <see cref="Aim"/>.
/// </summary>
/// <param name="entity">The walker that phases.</param>
/// <param name="tuning">How the action is tuned.</param>
public sealed class PhaseAction(WalkerEntity entity, PhaseTuning tuning)
{
    // In game time: how long until a phase can be aimed again.
    private float _cooldownLeft;
    // In real time: how much longer the last direction held while a phase is aimed still counts, once let go of.
    private float _directionTimeLeft;

    /// <summary>
    /// The phase being aimed, from when phase is held with a target in reach, after the cooldown, until it is released.
    /// </summary>
    public PhaseAim? Aim { get; private set; }

    /// <summary>Begins, holds, aims and releases a phase.</summary>
    /// <param name="held">Whether phase is held.</param>
    /// <param name="direction">
    /// The direction held: one of the eight directions, as a unit vector in the plane of play, or zero if none.
    /// </param>
    /// <param name="gameDt">The tick's length in game time: 0 while the game is paused.</param>
    /// <param name="realDt">The tick's length in real time.</param>
    /// <param name="defer">An object that allows running actions later in the tick.</param>
    public PhaseOutcome Apply(bool held, Vector3 direction, float gameDt, float realDt, IDefer defer)
    {
        PhaseOutcome outcome = Step(held, direction, gameDt, realDt, defer);
        _cooldownLeft = Math.Max(0, _cooldownLeft - gameDt);
        return outcome;
    }

    private PhaseOutcome Step(bool held, Vector3 direction, float gameDt, float realDt, IDefer defer)
    {
        if (Aim is null && held && _cooldownLeft <= 0 && TargetedEntity(direction) is Entity target)
        {
            Aim = new PhaseAim(
                player: entity,
                target: target,
                gap: tuning.Gap(),
                phaseThrow: tuning.Throw(),
                speed: tuning.Speed(),
                defer: defer);

            _directionTimeLeft = 0;
            entity.RaiseCue(EntityCue.PhaseBegan);
        }

        if (Aim is null)
        {
            return PhaseOutcome.Idle;
        }

        // The direction held, or the last one held if it was let go of only just now, so that letting go of it just
        // before phase, or with it, still phases.
        if (direction != Vector3.Zero)
        {
            Aim.Direction = direction;
            _directionTimeLeft = Features.TicksToTime(tuning.ReleaseTicks() + 1); // +1 to include this tick
        }
        else
        {
            _directionTimeLeft = Math.Max(0, _directionTimeLeft - realDt);
            if (_directionTimeLeft == 0)
            {
                Aim.Direction = Vector3.Zero;
            }
        }

        if (held)
        {
            // Only on ticks of play: while paused, nothing drifts, and the velocities read are from the engine's last
            // step, so every grab would add the same impulses again. And not on the release, so that the phase reads
            // the velocities it replaces.
            if (gameDt > 0)
            {
                Aim.Grab(gameDt);
            }
            return PhaseOutcome.Aiming;
        }

        PhaseOutcome outcome;
        if (Aim.Direction != Vector3.Zero)
        {
            Aim.Phase(defer);
            _cooldownLeft = tuning.ThrowCooldownTime();
            entity.RaiseCue(EntityCue.PhaseThrown);
            outcome = PhaseOutcome.Phased;
        }
        else
        {
            _cooldownLeft = tuning.CancelCooldownTime();
            entity.RaiseCue(EntityCue.PhaseCancelled);
            outcome = PhaseOutcome.Cancelled;
        }

        Aim.Stop(defer);
        Aim = null;
        return outcome;
    }

    /// <summary>
    /// The entity in reach, if any, whose box is nearest a point offset from the entity's center in the direction
    /// held. Reach is a sphere around the entity's center, which meets every entity touching its box.
    /// </summary>
    private Entity? TargetedEntity(Vector3 direction)
    {
        float radius = entity.HalfSize.Length() + tuning.Reach();
        Vector2 from = (entity.Position + tuning.InputOffset() * direction).ToVector2();
        // A sphere meets a box exactly when the box's nearest point is within its radius of its center.
        return entity.Overlapping(new SphereShape3D { Radius = radius }, entity.Position)
            .OfType<Entity>()
            .MinBy(other =>
            {
                Vector2 apart = ((from - other.Position.ToVector2()).Abs() - other.HalfSize).Max(Vector2.Zero);
                return apart.LengthSquared();
            });
    }

    /// <summary>
    /// A phase being aimed: the player and its target, held together by <see cref="Grab"/>, and what is needed to make the
    /// phase when it is released, with <see cref="Phase"/>. It is made when an aim begins, and dropped on the
    /// release.
    /// </summary>
    public sealed class PhaseAim
    {
        private readonly float _gap;
        private readonly PhaseThrow _phaseThrow;
        private readonly float _speed;
        /// <summary>The initial vector from player to target, which Grab tries to maintain.</summary>
        private readonly Vector3 _grabOffset;
        /// <summary>Player.GravityScale on the first tick of a phase.</summary>
        private readonly float _playerGravityScale;
        /// <summary>Target.GravityScale on the first tick of a phase.</summary>
        private readonly float _targetGravityScale;

        /// <summary>
        /// Constructs a PhaseAim and turns off gravity for the player and target.
        /// </summary>
        /// <param name="player">The walker phasing: the player.</param>
        /// <param name="target">The entity phased through, fixed when the aim begins.</param>
        /// <param name="gap">How far apart the two boxes end up, along the phase's direction.</param>
        /// <param name="phaseThrow">What momentum the phase gives the player and the target.</param>
        /// <param name="speed">The speed of the momentum the phase gives the player, along its direction.</param>
        /// <param name="defer">An object that allows running actions later in the tick.</param>
        public PhaseAim(WalkerEntity player, Entity target, float gap, PhaseThrow phaseThrow, float speed, IDefer defer)
        {
            Player = player;
            Target = target;
            _gap = gap;
            _phaseThrow = phaseThrow;
            _speed = speed;
            _grabOffset = target.Position - player.Position;

            _playerGravityScale = Player.GravityScale;
            _targetGravityScale = Target.GravityScale;
            Player.SetGravityScaleAfterPhysicsStep(0, defer);
            Target.SetGravityScaleAfterPhysicsStep(0, defer);
        }

        /// <summary>The walker phasing: the player.</summary>
        public WalkerEntity Player { get; }

        /// <summary>The entity phased through, fixed when the aim begins.</summary>
        public Entity Target { get; }

        /// <summary>
        /// One of the eight directions, as a unit vector in the plane of play, or zero if none is held. Released with none,
        /// the phase is cancelled.
        /// </summary>
        public Vector3 Direction { get; set; }

        /// <summary>Computes the center of mass of the player+target system.</summary>
        public Vector3 CenterOfMass() =>
            (Player.Mass * Player.Position + Target.Mass * Target.Position) / (Player.Mass + Target.Mass);

        /// <summary>
        /// The impulses on the player and the target, as of now, if the phase were made: the player's along the phase's
        /// direction, and the target's opposite.
        /// </summary>
        public (Vector3, Vector3) Impulses()
        {
            Vector3 momentum = Player.Mass * _speed * Direction;
            return _phaseThrow switch
            {
                PhaseThrow.Fixed => (
                     momentum - Player.Mass * Player.LinearVelocity,
                    -momentum - Target.Mass * Target.LinearVelocity),
                PhaseThrow.Conserved => (momentum, -momentum),
            };
        }

        /// <summary>
        /// Holds the player and the target where they were from each other when the aim began, while they move together
        /// at the velocity of their center of mass, as in a perfectly inelastic collision. Done on every tick of play while
        /// a phase is aimed, so that they keep together while the game slows down to the pause, whatever else pushes on
        /// them, such as a flying block that collides with the player. Without it, they can drift far apart before the
        /// phase. Momentum is conserved: the impulses on the two are equal and opposite.
        /// </summary>
        public void Grab(float gameDt)
        {
            float totalMass = Player.Mass + Target.Mass;
            Vector3 velocity = (Player.Mass * Player.LinearVelocity + Target.Mass * Target.LinearVelocity) / totalMass;

            // The relative velocity that takes back how far they have drifted, over a step, but never over less than a
            // tick's length at full speed. Steps shrink with the time scale as the game slows down, and dividing by a tiny
            // one would give a velocity large enough to fling them both when play resumes.
            Vector3 drift = Target.Position - Player.Position - _grabOffset;
            Vector3 closing = -drift / MathF.Max(gameDt, 1f / Engine.PhysicsTicksPerSecond);

            Player.ApplyCentralImpulse(Player.Mass * (velocity - Target.Mass / totalMass * closing - Player.LinearVelocity));
            Target.ApplyCentralImpulse(Target.Mass * (velocity + Player.Mass / totalMass * closing - Target.LinearVelocity));
        }

        /// <summary>
        /// Phases through the target in <see cref="Direction"/>: the player and the target move as a pair, keeping their
        /// center of mass, onto the line through it in that direction, the player ahead of it and the target behind. Each
        /// moves through entities, but not through tiles, sliding along them, and the engine pushes apart whatever ends up
        /// overlapping. The player is thrown in that direction, and the target the other way, as the throw says.
        /// </summary>
        /// <param name="defer">Where the moves wait, so that every controller this tick sees the bodies where they
        /// were.</param>
        public void Phase(IDefer defer)
        {
            // Each moves in proportion to the other's mass.
            float totalMass = Player.Mass + Target.Mass;
            Vector3 center = CenterOfMass();
            float separation = Separation();
            Vector3 playerDestination = center + Target.Mass / totalMass * separation * Direction;
            Vector3 targetDestination = center - Player.Mass / totalMass * separation * Direction;
            defer.RunAfterEntitySteps(() =>
            {
                Player.MoveThroughEntities(playerDestination);
                Target.MoveThroughEntities(targetDestination);
            });

            var (playerImpulse, targetImpulse) = Impulses();
            Player.ApplyCentralImpulse(playerImpulse);
            Target.ApplyCentralImpulse(targetImpulse);

            // Facing the camera, as in a jump without moving, for a phase straight up or down.
            Player.Facing = Math.Sign(Direction.X);
        }

        /// <summary>
        /// How far apart the centers of the player and the target end up: enough to clear their boxes in the
        /// direction of the phase, plus the gap.
        /// </summary>
        private float Separation()
        {
            Vector2 halfSizes = Player.HalfSize + Target.HalfSize;
            // Along a diagonal, the boxes clear when they clear on either axis, whichever comes first.
            float clear = MathF.Min(
                Direction.X == 0 ? float.PositiveInfinity : halfSizes.X / MathF.Abs(Direction.X),
                Direction.Y == 0 ? float.PositiveInfinity : halfSizes.Y / MathF.Abs(Direction.Y));
            return clear + _gap;
        }

        /// <summary>
        /// Restores gravity for the player and target, after the engine's step, so that what controllers reckoned with
        /// this tick holds through it. This should always be called on the last tick of a phase.
        /// </summary>
        public void Stop(IDefer defer)
        {
            Player.SetGravityScaleAfterPhysicsStep(_playerGravityScale, defer);
            Target.SetGravityScaleAfterPhysicsStep(_targetGravityScale, defer);
        }
    }
}
