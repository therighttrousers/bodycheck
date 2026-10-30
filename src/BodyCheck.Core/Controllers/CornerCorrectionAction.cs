using BodyCheck.Core.Entities;

using Godot;

namespace BodyCheck.Core.Controllers;

/// <summary>
/// How a <see cref="CornerCorrectionAction"/> is tuned. Each value is a function that reads it from the features in
/// effect when it is used, so that a change to them takes effect at once. See the player's, in <see cref="Features"/>,
/// for what each means.
/// </summary>
public sealed record CornerCorrectionTuning
{
    /// <summary>How far the entity may be nudged sideways while rising.</summary>
    public required Func<float> UpwardMaxNudge { get; init; }
    /// <summary>How much, in degrees, a sideways nudge may work against velocity.</summary>
    public required Func<float> UpwardMaxAngle { get; init; }
    /// <summary>How far the entity may be nudged up or down while moving sideways.</summary>
    public required Func<float> ForwardMaxNudge { get; init; }
    /// <summary>How much, in degrees, an up or down nudge may work against velocity.</summary>
    public required Func<float> ForwardMaxAngle { get; init; }

    public static CornerCorrectionTuning Player { get; } = new()
    {
        UpwardMaxNudge = () => Features.Current.PlayerUpwardCornerCorrection,
        UpwardMaxAngle = () => Features.Current.PlayerUpwardCornerCorrectionAngle,
        ForwardMaxNudge = () => Features.Current.PlayerForwardCornerCorrection,
        ForwardMaxAngle = () => Features.Current.PlayerForwardCornerCorrectionAngle,
    };
}

/// <summary>
/// Nudges an entity around the corner of a tile that it is about to clip, so that it doesn't stop (see
/// docs/designs/movement.md). It looks ahead by one tick, so the entity never meets the corner, and nothing it has,
/// such as its velocity, needs restoring.
/// </summary>
/// <param name="entity">The entity to nudge.</param>
/// <param name="tuning">How the action is tuned.</param>
public sealed class CornerCorrectionAction(Entity entity, CornerCorrectionTuning tuning)
{
    // How far apart the nudges tried are: a quarter of a pixel, so that a nudge round a corner that the entity is
    // flush with leaves it little further from the corner than it must be.
    private const float Step = 0.25f / Constants.TilePixels;

    /// <summary>
    /// Possibly nudges the entity: left or right if it is rising into the bottom of a tile, to clear a corner; and if
    /// not, up or down if it is moving sideways, to clear ledges, run over gaps in the floor, or climb steps.
    /// </summary>
    /// <param name="velocity">
    /// A prediction of the entity's velocity after the engine integrates forces in this tick: its velocity as of the
    /// last tick, plus the delta-v of all known impulses or forces applied in this tick.
    /// </param>
    /// <param name="gameDt">The tick's length in game time.</param>
    /// <param name="defer">An object that allows running actions later in the tick.</param>
    public void Apply(Vector3 velocity, float gameDt, IDefer defer)
    {
        if (!UpwardCorrect(velocity, gameDt, defer))
        {
            ForwardCorrect(velocity, gameDt, defer);
        }
    }

    /// <summary>
    /// Possibly nudges a rising entity left or right to avoid bumping the bottom of a tile. See <see cref="Correct"/>.
    /// </summary>
    /// <returns>Whether the entity is nudged.</returns>
    private bool UpwardCorrect(Vector3 velocity, float gameDt, IDefer defer) =>
        velocity.Y > 0 && Correct(velocity, Vector3.Down, gameDt, tuning.UpwardMaxNudge(), tuning.UpwardMaxAngle(), defer);

    /// <summary>
    /// Possibly nudges a sideways-moving entity up or down to avoid bumping the side of a tile. See <see cref="Correct"/>.
    /// </summary>
    /// <returns>Whether the entity is nudged.</returns>
    private bool ForwardCorrect(Vector3 velocity, float gameDt, IDefer defer) =>
        velocity.X != 0 && Correct(
            velocity, MathF.Sign(velocity.X) * Vector3.Left, gameDt, tuning.ForwardMaxNudge(), tuning.ForwardMaxAngle(), defer);


    /// <summary>
    /// Does corner correction: mitigates bonking on surfaces with normal <paramref name="surface"/> by nudging
    /// <paramref name="entity"/> perpendicular to the surface. That is, if all of the following are true:
    /// <list type="bullet">
    /// <item><paramref name="velocity"/> opposes the surface normal (within 90 degrees)</item>
    /// <item>moving <paramref name="entity"/> along <paramref name="velocity"/>*<paramref name="gameDt"/> would collide with a tile</item>
    /// <item>the contacted surface normal is the same as <paramref name="surface"/></item>
    /// <item>a position near the entity's current position but offset perpendicular to <paramref name="surface"/> can be found that would not cause a collision</item>
    /// </list>
    /// then this method moves <paramref name="entity"/> to that position (after all entity steps), and what it carries by
    /// the same nudge.
    /// </summary>
    /// <returns>Whether the entity will be nudged.</returns>
    /// <param name="velocity">
    /// A prediction of the entity's velocity after the engine integrates forces in this tick: its velocity as of the
    /// last tick, plus the delta-v of all known impulses or forces applied in this tick.
    /// </param>
    /// <param name="surface">The normal of surfaces this tries to avoid bonking on.</param>
    /// <param name="gameDt">The tick's length in game time.</param>
    /// <param name="maxNudge">The furthest the entity may be nudged.</param>
    /// <param name="maxAngle">
    /// How much a nudge is allowed to oppose velocity, given as an angle. At 0, nudging may not oppose velocity. At 90,
    /// nudging may be exactly opposite velocity.
    /// </param>
    /// <param name="defer">An object that allows running actions later in the tick.</param>
    private bool Correct(
        Vector3 velocity, Vector3 surface, float gameDt, float maxNudge, float maxAngle, IDefer defer)
    {
        // If velocity doesn't oppose the surface normal, don't correct.
        if (velocity.Normalized().Dot(surface) > -0.0001f)
        {
            return false;
        }

        Vector3 across = new(surface.Y, -surface.X, 0);
        Vector3 center = entity.Position;
        // Only tiles are checked, an approximation: correction is for the shape of the level, and what the entity carries
        // moves with it. A nudge into any other entity leaves the engine to push the two apart, as when any two meet.
        var tiles = PhysicsLayer.Static;

        // We need to work with distances (because physics engine tolerances are distances), so define motion and
        // distance such that motion*distance = velocity*gameDt.
        Vector3 motion = velocity * gameDt;
        float distance = motion.Length();
        motion /= distance;
        // But look at least as far ahead as the engine makes contacts. `RoomToMove` casts a box smaller by the contact
        // tolerance, so a short motion that mostly slides along a wall the entity touches, such as rising against it
        // while pushing into it, doesn't reach across that gap, and the wall would be seen only at the top of the rise.
        distance = MathF.Max(distance, Constants.SpeculativeContactDistance);

        var (wideCenter, wideHalfSize) = Widened(center, across, tiles);
        float room = entity.RoomToMove(wideHalfSize, wideCenter, motion, distance, tiles);
        // If the entity can move all the way along velocity*gameDt, don't correct.
        if (room >= distance)
        {
            return false;
        }
        // If the contacted surface normal isn't the given surface normal, don't correct. The cast's box, smaller by the
        // contact tolerance, first touches what it meets at the room plus the tolerance; a tolerance further, it
        // overlaps it, which it must to find the contact.
        var contact = entity.GetNearestContact(wideHalfSize, wideCenter + motion * (room + Entity.ContactTolerance * 2), tiles);
        if (contact is not ContactPoint c || surface.Dot(c.Normal) < 0.999f)
        {
            return false;
        }

        // The maximum cosine of the angle between the velocity and a vector perpendicular to surface. (Yes, we use sin. Not a bug.)
        float leastCos = -MathF.Sin(Mathf.DegToRad(maxAngle));
        Vector3[] nudgeDirs = [.. new[] { -across, across }.Where(nudgeDir => velocity.Normalized().Dot(nudgeDir) >= leastCos)];

        // Try different distances along nudgeDirs, smallest first.
        for (float nudgeDist = Step; nudgeDist <= maxNudge; nudgeDist += Step)
        {
            foreach (Vector3 nudgeDir in nudgeDirs)
            {
                // Try to move from center along nudgeDir*nudgeDist.
                if (entity.RoomToMove(entity.HalfSize, center, nudgeDir, nudgeDist, tiles) < nudgeDist - Entity.ContactTolerance)
                {
                    continue;
                }
                Vector3 nudge = nudgeDir * nudgeDist;
                Vector3 nudgedCenter = center + nudge;
                // If a widened entity, moving from the nudged center along the original vector*gameDt, doesn't hit
                // anything, we've found our correction.
                (wideCenter, wideHalfSize) = Widened(nudgedCenter, across, tiles);
                if (entity.RoomToMove(wideHalfSize, wideCenter, motion, distance, tiles) >= distance)
                {
                    defer.RunAfterEntitySteps(() =>
                    {
                        // What it carries goes with it, sliding along any tile in the way, so that the entity isn't
                        // nudged into it and pushed back. Not what that carries in turn: a stack is rarely nudged, since
                        // carrying one, the entity can't jump high.
                        foreach (Entity carried in entity.Carried)
                        {
                            carried.MoveThroughEntities(carried.Position + nudge, interpolated: true);
                        }
                        entity.MoveTo(nudgedCenter, interpolated: true);
                    });
                    Telemetry.Report(Metric.CornerCorrectionTime, Time.GetTicksMsec() / 1000f);
                    Telemetry.Report(Metric.CornerCorrectionCenter, center.ToVector2());
                    Telemetry.Report(Metric.CornerCorrectionNudge, nudge.ToVector2());
                    return true;
                }
            }
        }
        return false;
    }

    /// <summary>
    /// The entity's box at a place, widened across the motion, on each side as far as it has room up to the speculative
    /// contact distance and some slack. Otherwise a corner that the box would only pass near would be missed, though the
    /// engine can stop the entity at it. Not into what is beside it, since a cast that starts overlapping anything misses
    /// everything.
    /// </summary>
    private (Vector3 Center, Vector2 HalfSize) Widened(Vector3 center, Vector3 across, PhysicsLayer layers)
    {
        // `RoomToMove` casts a box smaller by the contact tolerance, so widening by that much more is widening by the
        // speculative contact distance, and the rest is slack.
        float reach = Constants.SpeculativeContactDistance + 2 * Entity.ContactTolerance;
        float before = entity.RoomToMove(entity.HalfSize, center, -across, reach, layers);
        float after = entity.RoomToMove(entity.HalfSize, center, across, reach, layers);
        return (center + (after - before) / 2 * across, entity.HalfSize + (before + after) / 2 * across.ToVector2().Abs());
    }
}
