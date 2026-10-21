using BodyCheck.Core.Entities;

using Godot;

namespace BodyCheck.Core.Controllers;

public sealed class RobotController : ICharacterController<WalkerEntity>
{
    private float _facing = 1;
    private float _move = 0;

    public void Apply(WalkerEntity entity, float gameDt, float realDt)
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

        ApplyRun(entity, _move, gameDt);

        // Set animation variables
        entity.IsGrounded = entity.OnGround;
        entity.IsMoving = _move != 0;
        entity.Facing = _facing;
    }

    private void ApplyRun(WalkerEntity entity, float move, float gameDt)
    {
        Features features = Features.Current;

        // Speed is relative to whatever the entity stands on.
        float speed = entity.GroundSpeed;
        bool reverse = speed != 0 && move * speed <= 0;

        float drive = 0;
        if (move != 0)
        {
            // The drive fades out as the entity nears top speed in the input direction.
            // It never slows an entity that is already faster than that.
            float headroom = Math.Clamp(1 - speed * MathF.Sign(move) / features.RobotTopSpeed, 0, 1);
            float force = reverse ? features.RobotReverseDriveForce : features.RobotForwardDriveForce;
            drive = MathF.Abs(move) * force * headroom;
        }

        // The drive is traction: the engine applies it, and pushes back on the support. It adds to the entity's
        // friction, which holds an idle entity still on its support. Driving the way the entity is already going
        // gets no friction, so the drive takes it off again. Reversing, it keeps only as much as stops the entity and
        // what it carries within the tick, so that friction doesn't push on past zero towards the drive's target
        // (019 §4.8). Neither does anything in the air.
        if (move != 0)
        {
            // A loaded walker drives harder, so that carrying slows it less (019 §6.4).
            drive *= entity.ComputeLoad() / entity.Weight;
            float stackMass = entity.ComputeLoad() * entity.Mass / entity.Weight;
            float stopping = stackMass * MathF.Abs(speed) / gameDt;
            float extra = reverse ? drive + MathF.Min(entity.Friction, stopping) - entity.Friction : drive - entity.Friction;
            entity.ApplyTraction(MathF.Sign(move) * features.RobotTopSpeed, extra);
        }
    }
}
