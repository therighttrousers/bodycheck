namespace BodyCheck.Core;

using Godot;

public sealed record RobotTuning
{
    /// <summary>Speed at which the run drive force falls to zero.</summary>
    public float TopSpeed { get; init; } = 4;

    /// <summary>Force applied when on the ground and moving in the direction of input.</summary>
    public float ForwardDriveForce { get; init; } = 20;
    /// <summary>Force applied when on the ground and moving against the direction of input.</summary>
    public float ReverseDriveForce { get; init; } = 40;

    /// <summary>
    /// Coefficient of friction with the ground, whether tiles or another body. Applied when not driving in
    /// the direction of travel.
    /// </summary>
    public float GroundFriction { get; init; } = 4;
}

public sealed class RobotController : ICharacterController<WalkerEntity>
{
    private float _facing = 1;
    private float _move = 0;

    public RobotController(RobotTuning tuning)
    {
        Tuning = tuning;
    }

    public RobotTuning Tuning { get; }

    private void UpdateVisuals(Entity entity, float dt)
    {
        if (entity.Visual is null) return;

        var rot = entity.Visual.Rotation;
        var target = 0.5f * MathF.PI * _facing;
        var newYaw = Mathf.MoveToward(rot.Y, target, 10 * dt);
        entity.Visual.Rotation = new Vector3(rot.X, newYaw, rot.Z);
    }

    public void Apply(WalkerEntity entity, float dt)
    {
        // Patrol if on tiles. Bounce off of anything solid (including the player).
        if (entity.OnGround && entity.Support is null)
        {
            if (_move == 0)
            {
                _move = _facing;
            }
            else if (_move < 0 && entity.Contacts.HasFlag(Contacts.Left))
            {
                _facing = _move = 1;
            }
            else if (_move > 0 && entity.Contacts.HasFlag(Contacts.Right))
            {
                _facing = _move = -1;
            }
        }
        else
        {
            _move = 0;
        }

        ApplyRun(entity, _move);

        UpdateVisuals(entity, dt);
    }

    private void ApplyRun(WalkerEntity entity, float move)
    {
        // Speed is relative to whatever the entity stands on.
        float speed = entity.LinearVelocity.X - entity.GroundVelocity.X;
        bool reverse = speed != 0 && move * speed <= 0;

        float drive = 0;
        if (move != 0)
        {
            // The drive fades out as the entity nears top speed in the input direction.
            // It never slows an entity that is already faster than that.
            float headroom = Math.Clamp(1 - speed * MathF.Sign(move) / Tuning.TopSpeed, 0, 1);
            float force = reverse ? Tuning.ReverseDriveForce : Tuning.ForwardDriveForce;
            drive = MathF.Abs(move) * force * headroom;
        }

        // The drive is traction: the engine applies it, and pushes back on the support. Friction adds to it
        // unless the entity is driving the way it is already going, so it holds an idle entity still on its
        // support, and can stop a moving one but can't reverse it. Neither does anything in the air.
        float friction = move == 0 || reverse ? Tuning.GroundFriction * entity.Weight : 0;
        entity.ApplyTraction(MathF.Sign(move) * Tuning.TopSpeed, drive + friction);
    }
}
