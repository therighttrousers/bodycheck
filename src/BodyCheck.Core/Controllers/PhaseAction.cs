using BodyCheck.Core.Entities;

using Godot;

namespace BodyCheck.Core.Controllers;

/// <summary>What momentum a phase gives the player and the entity phased through.</summary>
public enum PhaseThrow
{
    /// <summary>
    /// Replaces the momentum of each with equal and opposite momentum, along the phase's direction. Every phase leaves
    /// them moving the same, however they were moving before, which makes juggling easy and predictable. Momentum is
    /// not conserved.
    /// </summary>
    Fixed,

    /// <summary>
    /// Adds equal and opposite momentum to each, along the phase's direction, so momentum is conserved. Where they end
    /// up moving depends on how they were moving: phases in a row can build speed, and to juggle a robot high, the
    /// player has to meet it just before the top of its rise.
    /// </summary>
    Conserved,
}

/// <summary>
/// A phase being aimed: the player and its target, held together by <see cref="Grab"/>, and what is needed to make the
/// phase when it is released, with <see cref="TryPhase"/>.
/// </summary>
/// <param name="Player">The walker phasing: the player.</param>
/// <param name="Target">The entity phased through, fixed when the aim begins.</param>
/// <param name="GrabOffset">Where the target was from the player when the aim began, which the grab holds.</param>
/// <param name="Gap">How far apart the two boxes end up, along the phase's direction.</param>
/// <param name="Direction">
/// One of the eight directions, as a unit vector in the plane of play, or zero if none is held. Released with none,
/// the phase is cancelled.
/// </param>
/// <param name="MaxSlide">How far the pair may slide along the line to fit, as a fraction of their separation.</param>
/// <param name="Throw">What momentum the phase gives the player and the target.</param>
/// <param name="Speed">The speed of the momentum the phase gives the player, along its direction.</param>
public readonly record struct PhaseAction(
    WalkerEntity Player,
    Entity Target,
    Vector3 GrabOffset,
    float Gap,
    Vector3 Direction,
    float MaxSlide,
    PhaseThrow Throw,
    float Speed)
{
    /// <summary>Computes the center of mass of the player+target system.</summary>
    public Vector3 CenterOfMass() =>
        (Player.Mass * Player.Position + Target.Mass * Target.Position) / (Player.Mass + Target.Mass);

    /// <summary>
    /// The impulses on the player and the target, as of now, if the phase were made: the player's along the phase's
    /// direction, and the target's opposite.
    /// </summary>
    public (Vector3, Vector3) Impulses()
    {
        Vector3 momentum = Player.Mass * Speed * Direction;
        return Throw switch
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
    /// them, such as gravity on a player beside a hovering platform. Without it, they can drift far apart before the
    /// phase. Momentum is conserved: the impulses on the two are equal and opposite.
    /// </summary>
    public void Grab(float dt)
    {
        float totalMass = Player.Mass + Target.Mass;
        Vector3 velocity = (Player.Mass * Player.LinearVelocity + Target.Mass * Target.LinearVelocity) / totalMass;

        // The relative velocity that takes back how far they have drifted, over a step, but never over less than a
        // tick's length at full speed. Steps shrink with the time scale as the game slows down, and dividing by a tiny
        // one would give a velocity large enough to fling them both when play resumes.
        Vector3 drift = Target.Position - Player.Position - GrabOffset;
        Vector3 closing = -drift / MathF.Max(dt, 1f / Engine.PhysicsTicksPerSecond);

        Player.ApplyCentralImpulse(Player.Mass * (velocity - Target.Mass / totalMass * closing - Player.LinearVelocity));
        Target.ApplyCentralImpulse(Target.Mass * (velocity + Player.Mass / totalMass * closing - Target.LinearVelocity));
    }

    /// <summary>
    /// Phases through the target in a direction, if they fit: the player and the target teleport as a pair, keeping
    /// their center of mass, onto the line through it in that direction, the player ahead of it and the target
    /// behind. If they don't fit there, they are offset from each other across the line, as far as lines up their
    /// edges on either side, and slide along the line, the nearest first. The player is thrown in that direction, and
    /// the target the other way, as <see cref="Throw"/> says.
    /// </summary>
    /// <returns>Whether the player phased.</returns>
    public bool TryPhase()
    {
        // Each moves in proportion to the other's mass.
        float totalMass = Player.Mass + Target.Mass;
        Vector3 center = CenterOfMass();
        float separation = Separation();
        Vector3 playerOffset = Target.Mass / totalMass * separation * Direction;
        Vector3 targetOffset = -Player.Mass / totalMass * separation * Direction;

        // Across the line, the player may be offset from the target as far as lines up their edges on one side or the
        // other: centers level is the middle, and, for a horizontal phase, bottoms or tops level are the ends. Split by
        // mass, as along the line, so that the center of mass stays where it is. For bodies the same size, it is 0.
        var across = new Vector3(-Direction.Y, Direction.X, 0);
        float maxOffset = MathF.Abs(Extent(Player.HalfSize, across) - Extent(Target.HalfSize, across));

        const float step = 1f / 16;
        foreach (float offset in NearestFirst(maxOffset, step))
        {
            Vector3 playerAcross = Target.Mass / totalMass * offset * across;
            Vector3 targetAcross = -Player.Mass / totalMass * offset * across;
            foreach (float slide in NearestFirst(MaxSlide * separation, step))
            {
                Vector3 playerDestination = center + slide * Direction + playerOffset + playerAcross;
                Vector3 targetDestination = center + slide * Direction + targetOffset + targetAcross;
                if (Player.CanTeleport(playerDestination, Target) &&
                    Target.CanTeleport(targetDestination, Player))
                {
                    Phase(playerDestination, targetDestination);
                    if (Direction.X != 0)
                    {
                        Player.Facing = MathF.Sign(Direction.X);
                    }
                    return true;
                }
            }
        }
        return false;
    }

    /// <summary>How far a box with a half size reaches from its center in a direction, given as a unit vector.</summary>
    private static float Extent(Vector2 halfSize, Vector3 direction) =>
        MathF.Abs(direction.X) * halfSize.X + MathF.Abs(direction.Y) * halfSize.Y;

    /// <summary>0, then steps either way, the nearest first, out to <paramref name="max"/> exactly.</summary>
    private static IEnumerable<float> NearestFirst(float max, float step)
    {
        yield return 0;
        for (int i = 1; (i - 1) * step < max; i++)
        {
            float value = MathF.Min(i * step, max);
            yield return value;
            yield return -value;
        }
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
        return clear + Gap;
    }

    private void Phase(Vector3 playerDestination, Vector3 targetDestination)
    {
        Player.Teleport(playerDestination);
        Target.Teleport(targetDestination);
        var (playerImpulse, targetImpulse) = Impulses();
        Player.ApplyCentralImpulse(playerImpulse);
        Target.ApplyCentralImpulse(targetImpulse);
    }
}

