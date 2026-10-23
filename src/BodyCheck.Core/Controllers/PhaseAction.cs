using BodyCheck.Core.Entities;

using Godot;

namespace BodyCheck.Core.Controllers;

/// <summary>
/// A phase being aimed: the player and its target, held together by <see cref="Grab"/>, and what is needed to make the
/// phase when it is released, with <see cref="Phase"/>.
/// </summary>
public sealed class PhaseAction
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
    /// Constructs a PhaseAction and turns off gravity for the player and target.
    /// </summary>
    /// <param name="player">The walker phasing: the player.</param>
    /// <param name="target">The entity phased through, fixed when the aim begins.</param>
    /// <param name="gap">How far apart the two boxes end up, along the phase's direction.</param>
    /// <param name="phaseThrow">What momentum the phase gives the player and the target.</param>
    /// <param name="speed">The speed of the momentum the phase gives the player, along its direction.</param>
    /// <param name="endOfTick">An object that allows running code at the end of the tick.</param>
    public PhaseAction(WalkerEntity player, Entity target, float gap, PhaseThrow phaseThrow, float speed, IEndOfTick endOfTick)
    {
        Player = player;
        Target = target;
        _gap = gap;
        _phaseThrow = phaseThrow;
        _speed = speed;
        _grabOffset = target.Position - player.Position;

        _playerGravityScale = Player.GravityScale;
        _targetGravityScale = Target.GravityScale;
        Player.SetGravityScaleDeferred(0, endOfTick);
        Target.SetGravityScaleDeferred(0, endOfTick);
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
    /// <param name="endOfTick">Where the moves wait, so that every controller this tick sees the bodies where they
    /// were.</param>
    public void Phase(IEndOfTick endOfTick)
    {
        // Each moves in proportion to the other's mass.
        float totalMass = Player.Mass + Target.Mass;
        Vector3 center = CenterOfMass();
        float separation = Separation();
        Vector3 playerDestination = center + Target.Mass / totalMass * separation * Direction;
        Vector3 targetDestination = center - Player.Mass / totalMass * separation * Direction;
        endOfTick.Defer(() =>
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
    /// Restores gravity for the player and target - deferred until the end of the tick. This should always be
    /// called on the last tick of a phase.
    /// </summary>
    public void Stop(IEndOfTick endOfTick)
    {
        Player.SetGravityScaleDeferred(_playerGravityScale, endOfTick);
        Target.SetGravityScaleDeferred(_targetGravityScale, endOfTick);
    }
}
