using BodyCheck.Core.Entities;

using Godot;

namespace BodyCheck.Core.Controllers;

/// <summary>
/// How a <see cref="RunAction"/> is tuned. Each value is a function that reads it from the features in effect when it is
/// used, so that a change to them takes effect at once.
/// </summary>
public sealed record RunTuning
{
    /// <summary>The most speed the entity ever tries to go, relative to its support.</summary>
    public required Func<float> TopSpeed { get; init; }
    /// <summary>The force with which the entity drives the way it moves, on the ground.</summary>
    public required Func<float> GroundDriveForce { get; init; }
    /// <summary>The force with which the entity drives against the way it moves, on the ground.</summary>
    public required Func<float> GroundBrakeForce { get; init; }
    /// <summary>The force with which the entity drives the way it moves, in the air.</summary>
    public required Func<float> AirDriveForce { get; init; }
    /// <summary>The force with which the entity drives against the way it moves, in the air.</summary>
    public required Func<float> AirBrakeForce { get; init; }

    public static RunTuning Player { get; } = new()
    {
        TopSpeed = () => Features.Current.PlayerTopSpeed,
        GroundDriveForce = () => Features.Current.PlayerGroundDriveForce,
        GroundBrakeForce = () => Features.Current.PlayerGroundBrakeForce,
        AirDriveForce = () => Features.Current.PlayerAirDriveForce,
        AirBrakeForce = () => Features.Current.PlayerAirBrakeForce,
    };

    /// <summary>A robot, which doesn't drive in the air.</summary>
    public static RunTuning Robot { get; } = new()
    {
        TopSpeed = () => Features.Current.RobotTopSpeed,
        GroundDriveForce = () => Features.Current.RobotDriveForce,
        GroundBrakeForce = () => Features.Current.RobotBrakeForce,
        AirDriveForce = () => 0,
        AirBrakeForce = () => 0,
    };
}

/// <summary>Walking, running or rolling: a walker driving itself along the ground, or through the air.</summary>
/// <param name="entity">The walker that runs.</param>
/// <param name="tuning">How the action is tuned.</param>
public sealed class RunAction(WalkerEntity entity, RunTuning tuning)
{
    /// <summary>
    /// Makes the entity run, subject to the following rules:
    /// - If weightless, it doesn't run.
    /// - Otherwise, it tries to reach its top speed relative to its support (or relative to the world if no support).
    /// - Drive force, if used (intent in the direction of motion), falls off smoothly near top speed.
    /// - Brake force, if used (intent against the direction of motion), is fully applied.
    /// </summary>
    /// <returns>
    /// The change in the entity's velocity that the drive makes in this tick's step, which shows only after the step,
    /// and whether the entity should slip on its support this tick, if it is one that slips: the player does, robots
    /// don't (see docs/designs/movement.md).
    /// </returns>
    /// <param name="move">How the entity intends to move on this tick, between -1 and 1.</param>
    /// <param name="grounded">
    /// Whether it drives with its forces for the ground: when it is on the ground, or, for the player, just after it left.
    /// </param>
    /// <param name="jumps">Whether the entity jumps on this tick.</param>
    /// <param name="gameDt">The tick's length in game time.</param>
    public (Vector3 DeltaV, bool Slips) Apply(float move, bool grounded, bool jumps, float gameDt)
    {
        // Don't try to run if weightless (e.g. being grabbed).
        if (entity.Weight == 0) return (Vector3.Zero, false);

        float topSpeed = tuning.TopSpeed();
        float driveForce = grounded ? tuning.GroundDriveForce() : tuning.AirDriveForce();
        float brakeForce = grounded ? tuning.GroundBrakeForce() : tuning.AirBrakeForce();

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
        // The drive moves what the entity carries too.
        float mass = entity.LoadedMass;

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
