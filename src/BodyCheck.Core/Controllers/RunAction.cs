using BodyCheck.Core.Entities;

using Godot;

namespace BodyCheck.Core.Controllers;

internal static class RunAction
{
    /// <summary>
    /// Makes an entity run, subject to the following rules:
    /// - If weightless, it doesn't run.
    /// - Otherwise, it tries to reach topSpeed relative to its support (or relative to the world if no support).
    /// - Drive force, if used (intent in the direction of motion), falls off smoothly near top speed.
    /// - Brake force, if used (intent against the direction of motion), is fully applied.
    /// - Carrying a load increases the force applied.
    /// </summary>
    /// <param name="entity">The entity to walk, run or roll.</param>
    /// <param name="gameDt">Game-time tick duration.</param>
    /// <param name="move">How the entity intends to move on this frame, between -1 and 1.</param>
    /// <param name="jumps">Whether the entity jumps on this tick.</param>
    /// <param name="topSpeed">Maximum speed the entity ever tries to go, relative to its support.</param>
    /// <param name="driveForce">Force applied when the entity intends to drive in the direction of motion.</param>
    /// <param name="brakeForce">Force applied when the entity intends to drive against the direction of motion.</param>
    public static void Run(WalkerEntity entity, float gameDt, float move, bool jumps, float topSpeed, float driveForce, float brakeForce)
    {
        // Don't try to run if weightless (e.g. being grabbed).
        if (entity.Weight == 0) return;

        // Speed is relative to whatever the entity stands on, so running on a platform or blocks works like running on tiles.
        float speed = entity.GroundSpeed;
        bool reverse = speed != 0 && move * speed <= 0;

        float drive = 0;
        if (move != 0)
        {
            // The drive fades out as the entity nears top speed in the input direction.
            // It never slows an entity that is already faster than that.
            float headroom = Math.Clamp(1 - speed * MathF.Sign(move) / topSpeed, 0, 1);
            drive = MathF.Abs(move) * (reverse ? brakeForce : driveForce) * headroom;
        }

        if (entity.OnGround)
        {
            // On the ground the drive is traction: the engine applies it, and pushes back on the support. It
            // adds to the entity's friction, which holds an idle entity still on its support. Driving the way
            // the entity is already going gets no friction, so the drive takes it off again. Reversing, it
            // keeps only as much as stops the entity and what it carries within the tick, so that friction
            // doesn't push on past zero towards the drive's target (019 §4.8). Jumping, it takes all of the friction
            // off, whichever way it drives, so that a jump leaves with the entity's speed: a landing's catch would
            // otherwise stop a jump buffered on an idle landing dead (019 §6.5).
            if (move != 0 || jumps)
            {
                float load = entity.ComputeLoad();
                float stackMass = load * entity.Mass / entity.Weight;
                float stopping = stackMass * MathF.Abs(speed) / gameDt;
                // A loaded walker drives harder, so that carrying slows it less (019 §6.4).
                drive *= load / entity.Weight;
                float extra = reverse && !jumps
                    ? drive + MathF.Min(entity.Friction, stopping) - entity.Friction
                    : drive - entity.Friction;
                entity.ApplyTraction(MathF.Sign(move) * topSpeed, extra);
            }
        }
        else if (drive > 0)
        {
            entity.ApplyCentralForce(new Vector3(MathF.Sign(move) * drive, 0, 0));
        }
    }
}
