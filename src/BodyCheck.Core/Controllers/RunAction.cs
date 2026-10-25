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
        // Driving into something it touches is driving, not reversing, whichever way the entity is moving against its
        // support: a support moving under an entity held by a wall would otherwise make it brake, and grip the wall
        // (027 §6).
        bool pushes = (move > 0 && entity.Contacts.HasFlag(Contacts.Right))
            || (move < 0 && entity.Contacts.HasFlag(Contacts.Left));
        bool reverse = !pushes && speed != 0 && move * speed <= 0;

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
            // On the ground the drive is traction: the engine applies it, and pushes back on the support. The
            // entity's friction holds it still on its support while idle. Driving the way the entity is already
            // going, it slips, so that friction doesn't hold it back. Reversing, friction stays on and helps to stop
            // it, and stops at no slip, so it doesn't push on past zero towards the drive's target (019 §4.8).
            // Jumping, it slips, whichever way it drives, so that a jump leaves with the entity's speed (019 §6.5).
            // (027 §2)
            if (move != 0 || jumps)
            {
                if (jumps || !reverse)
                {
                    entity.SlipAtEndOfTick();
                }
                entity.ApplyTraction(MathF.Sign(move) * topSpeed, drive);
            }
        }
        else if (drive > 0)
        {
            entity.ApplyCentralForce(new Vector3(MathF.Sign(move) * drive, 0, 0));
        }
    }
}
