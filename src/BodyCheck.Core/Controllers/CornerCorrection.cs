using BodyCheck.Core.Entities;

using Godot;

namespace BodyCheck.Core.Controllers;

/// <summary>
/// Nudges an entity around the corner of a tile that it is about to clip, so that it doesn't stop (see
/// docs/designs/movement.md). It looks ahead by one tick, so the entity never meets the corner, and nothing it has,
/// such as its velocity, needs restoring.
/// </summary>
internal static class CornerCorrection
{
    // How far apart the nudges tried are: a quarter of a pixel, so that a nudge round a corner that the entity is
    // flush with leaves it little further from the corner than it must be.
    private const float Step = 0.25f / Constants.TilePixels;

    /// <summary>
    /// Nudges a rising entity sideways round the corner of a tile that it would bump. See <see cref="Correct"/>.
    /// </summary>
    /// <returns>Whether the entity is nudged.</returns>
    public static bool UpwardCorrect(
        Entity entity, Vector3 velocity, float gameDt, float maxNudge, float maxAngle, IDefer defer) =>
        velocity.Y > 0 && Correct(entity, velocity, Vector3.Up, gameDt, maxNudge, maxAngle, defer);

    /// <summary>
    /// If the entity's box would meet a tile on its way along <paramref name="direction"/> in this tick's step, as of
    /// the engine's last step, finds the smallest nudge across it, up to <paramref name="maxNudge"/>, from which it
    /// wouldn't, and makes it after the entity steps. The nudge, and the motion after it, must be clear of everything
    /// the entity collides with. Where something else, such as a block, is in the way first, the entity meets it as
    /// usual: correction is for the shape of the level, not for bodies.
    /// </summary>
    /// <returns>Whether the entity is nudged.</returns>
    /// <param name="entity">The entity to nudge.</param>
    /// <param name="velocity">
    /// The entity's velocity in this tick's step: its velocity as of the engine's last step, and any impulse applied
    /// since, which the engine doesn't show until its next step.
    /// </param>
    /// <param name="direction">The way the entity moves, along X or Y, as a unit vector. Only its velocity that way counts.</param>
    /// <param name="gameDt">The tick's length in game time.</param>
    /// <param name="maxNudge">The furthest the entity may be nudged.</param>
    /// <param name="maxAngle">
    /// How far, in degrees, the velocity may lean away from a side for the entity to be nudged to it. At 0, it is nudged
    /// only to a side it leans towards, or either side if it doesn't lean, and at 90, to either side however it leans.
    /// </param>
    /// <param name="defer">An object that allows running actions later in the tick.</param>
    private static bool Correct(
        Entity entity, Vector3 velocity, Vector3 direction, float gameDt, float maxNudge, float maxAngle, IDefer defer)
    {
        float distance = velocity.Dot(direction) * gameDt;
        if (distance <= 0)
        {
            return false;
        }
        Vector3 motion = distance * direction;
        Vector3 across = new(direction.Y, -direction.X, 0);
        // The cosine of the angle between the velocity and a side is the sine of how far the velocity leans towards it.
        float leastLean = -MathF.Sin(Mathf.DegToRad(maxAngle));
        Vector3[] sides = [.. new[] { -across, across }.Where(side => velocity.Normalized().Dot(side) >= leastLean)];
        Vector3 center = entity.Position;
        var all = (PhysicsLayer)entity.CollisionMask;

        var (wideCenter, wideHalfSize) = Widened(entity, center, across, all);
        float room = entity.RoomToMove(wideHalfSize, wideCenter, direction, distance, all);
        if (room >= distance - Entity.ContactTolerance
            || entity.RoomToMove(wideHalfSize, wideCenter, direction, distance, PhysicsLayer.Static) > room)
        {
            return false;
        }

        // Smallest first, trying every side at each size.
        for (int i = 1; i * Step <= maxNudge; i++)
        {
            foreach (Vector3 side in sides)
            {
                Vector3 nudge = i * Step * side;
                if (!IsClear(entity, center, entity.HalfSize, nudge, all))
                {
                    continue;
                }
                (wideCenter, wideHalfSize) = Widened(entity, center + nudge, across, all);
                if (IsClear(entity, wideCenter, wideHalfSize, motion, all))
                {
                    // Drawn sliding there over the tick, not jumping there.
                    defer.RunAfterEntitySteps(() => entity.MoveTo(center + nudge, interpolated: true));
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
    private static (Vector3 Center, Vector2 HalfSize) Widened(Entity entity, Vector3 center, Vector3 across, PhysicsLayer layers)
    {
        // `RoomToMove` casts a box smaller by the contact tolerance, so widening by that much more is widening by the
        // speculative contact distance, and the rest is slack.
        float reach = Constants.SpeculativeContactDistance + 2 * Entity.ContactTolerance;
        float before = entity.RoomToMove(entity.HalfSize, center, -across, reach, layers);
        float after = entity.RoomToMove(entity.HalfSize, center, across, reach, layers);
        return (center + (after - before) / 2 * across, entity.HalfSize + (before + after) / 2 * across.ToVector2().Abs());
    }

    private static bool IsClear(Entity entity, Vector3 center, Vector2 halfSize, Vector3 motion, PhysicsLayer layers)
    {
        float distance = motion.Length();
        return entity.RoomToMove(halfSize, center, motion / distance, distance, layers)
            >= distance - Entity.ContactTolerance;
    }
}
