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
    /// <returns>
    /// The change in the entity's velocity that the drive makes in this tick's step, which shows only after the step,
    /// and whether the entity should slip on its support this tick, if it is one that slips: the player does, robots
    /// don't (see docs/designs/movement.md).
    /// </returns>
    /// <param name="entity">The entity to walk, run or roll.</param>
    /// <param name="gameDt">Game-time tick duration.</param>
    /// <param name="move">How the entity intends to move on this frame, between -1 and 1.</param>
    /// <param name="jumps">Whether the entity jumps on this tick.</param>
    /// <param name="topSpeed">Maximum speed the entity ever tries to go, relative to its support.</param>
    /// <param name="driveForce">Force applied when the entity intends to drive in the direction of motion.</param>
    /// <param name="brakeForce">Force applied when the entity intends to drive against the direction of motion.</param>
    public static (Vector3 DeltaV, bool Slips) Run(WalkerEntity entity, float gameDt, float move, bool jumps, float topSpeed, float driveForce, float brakeForce)
    {
        // Don't try to run if weightless (e.g. being grabbed).
        if (entity.Weight == 0) return (Vector3.Zero, false);

        // Speed is relative to whatever the entity stands on, so running on a platform or blocks works like running on tiles.
        float speed = entity.GroundSpeed;
        // Driving into something it touches is driving, not braking, whichever way the entity is moving against its
        // support: a support moving under an entity held by a wall would otherwise make it brake, and grip the wall
        // (027 §6).
        bool pushes = (move > 0 && entity.Contacts.HasFlag(Contacts.Right))
            || (move < 0 && entity.Contacts.HasFlag(Contacts.Left));
        bool brake = !pushes && speed != 0 && move * speed <= 0;

        bool slips = false;
        float drive = 0;
        if (move != 0)
        {
            // The drive fades out as the entity nears top speed in the input direction.
            // It never slows an entity that is already faster than that.
            float headroom = Math.Clamp(1 - speed * MathF.Sign(move) / topSpeed, 0, 1);
            drive = MathF.Abs(move) * (brake ? brakeForce : driveForce) * headroom;
        }

        Vector3 deltaV = Vector3.Zero;
        // The drive moves what the entity carries too. Only what it carries directly counts, an approximation, like the
        // rest of the prediction.
        float mass = entity.Mass + entity.Above.Where(above => above.Tuning.CanBeCarried).Sum(above => above.Mass);

        if (entity.OnGround)
        {
            // On the ground the drive is traction: the engine applies it, and pushes back on the support. The
            // entity's friction holds it still on its support while idle. Driving the way the entity is already
            // going, it slips, so that friction doesn't hold it back. Braking, friction stays on and helps to stop
            // it, and stops at no slip, so it doesn't push on past zero towards the drive's target. Jumping, it
            // slips, whichever way it drives, so that a jump leaves with the entity's speed.
            if (move != 0 || jumps)
            {
                slips = jumps || !brake;
                float targetSpeed = MathF.Sign(move) * topSpeed;
                entity.ApplyTraction(targetSpeed, drive);
                // The engine's motor changes the speed relative to the support towards the target by at most the
                // drive's change in a tick, and not past it. Friction, while the entity doesn't slip, makes the change
                // smaller.
                float most = drive * gameDt / mass;
                deltaV = new Vector3(Math.Clamp(targetSpeed - speed, -most, most), 0, 0);
            }
        }
        else if (drive > 0)
        {
            entity.ApplyCentralForce(new Vector3(MathF.Sign(move) * drive, 0, 0));
            deltaV = new Vector3(MathF.Sign(move) * drive * gameDt / mass, 0, 0);
        }
        return (deltaV, slips);
    }
}
