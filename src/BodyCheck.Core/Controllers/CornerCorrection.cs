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
    // How far apart the nudges tried are: a pixel.
    private const float Step = 1f / Constants.TilePixels;

    /// <summary>
    /// If the entity's box would meet a tile on its way along <paramref name="motion"/>, as of the engine's last step,
    /// finds the smallest nudge to one of the <paramref name="sides"/>, up to <paramref name="maxNudge"/>, from which
    /// it wouldn't, and makes it after the entity steps. The nudge, and the motion after it, must be clear of everything
    /// the entity collides with. Where something else, such as a block, is in the way first, the entity meets it as
    /// usual: correction is for the shape of the level, not for bodies.
    /// </summary>
    /// <returns>Whether the entity is nudged.</returns>
    /// <param name="entity">The entity to nudge.</param>
    /// <param name="motion">Where the entity is expected to move in this tick's step.</param>
    /// <param name="sides">Unit vectors, across the motion, in which the entity may be nudged.</param>
    /// <param name="maxNudge">The furthest the entity may be nudged.</param>
    /// <param name="defer">An object that allows running actions later in the tick.</param>
    public static bool Correct(Entity entity, Vector3 motion, Vector3[] sides, float maxNudge, IDefer defer)
    {
        float distance = motion.Length();
        if (distance == 0)
        {
            return false;
        }
        Vector3 direction = motion / distance;
        Vector3 center = entity.Position;
        var all = (PhysicsLayer)entity.CollisionMask;

        float room = entity.RoomToMove(entity.HalfSize, center, direction, distance, all);
        if (room >= distance - Entity.ContactTolerance
            || entity.RoomToMove(entity.HalfSize, center, direction, distance, PhysicsLayer.Static) > room)
        {
            return false;
        }

        // Smallest first, trying every side at each size.
        for (int i = 1; i * Step <= maxNudge; i++)
        {
            foreach (Vector3 side in sides)
            {
                Vector3 nudge = i * Step * side;
                if (IsClear(entity, center, nudge, all) && IsClear(entity, center + nudge, motion, all))
                {
                    // Drawn sliding there over the tick, not jumping there.
                    defer.RunAfterEntitySteps(() => entity.MoveTo(center + nudge, interpolated: true));
                    return true;
                }
            }
        }
        return false;
    }

    private static bool IsClear(Entity entity, Vector3 center, Vector3 motion, PhysicsLayer layers)
    {
        float distance = motion.Length();
        return entity.RoomToMove(entity.HalfSize, center, motion / distance, distance, layers)
            >= distance - Entity.ContactTolerance;
    }
}
