using BodyCheck.Core.Entities;

namespace BodyCheck.Core.Controllers;

public sealed class RobotController : ICharacterController<WalkerEntity>
{
    private float _facing = 1;
    private float _move = 0;

    public void Apply(WalkerEntity entity, float gameDt, float realDt, IEndOfTick endOfTick)
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
        if (!entity.OnGround)
        {
            return;
        }
        RunAction.Run(
            entity: entity,
            gameDt: gameDt,
            move: move,
            jumps: false,
            topSpeed: Features.Current.RobotTopSpeed,
            driveForce: Features.Current.RobotDriveForce,
            brakeForce: Features.Current.RobotBrakeForce);
    }
}
