using BodyCheck.Core.Entities;

using Godot;

namespace BodyCheck.Core.Controllers;

public sealed class RobotController : ICharacterController<WalkerEntity>
{
    private const float ArbitraryFirstMove = -1;
    private float _move;
    private readonly StallDetector _stall = new();

    public void Apply(WalkerEntity entity, float gameDt, float realDt, IDefer defer)
    {
        // Patrol if on tiles. Turn around when stalled.
        if (entity.OnGround && entity.Support is null)
        {
            bool stalled = _stall.Update(
                entity.LinearVelocity.X * _move,
                gameDt,
                Features.Current.RobotStallSpeed,
                Features.Current.RobotStallTime);

            bool contactLeft = entity.Contacts.HasFlag(Contacts.Left);
            bool contactRight = entity.Contacts.HasFlag(Contacts.Right);
            if (_move == 0)
            {
                _move = contactLeft ? (contactRight ? 0 : 1) : contactRight ? -1 : ArbitraryFirstMove;
                _stall.Reset();
            }
            else if (_move < 0 && stalled)
            {
                _move = contactRight ? 0 : 1;
                _stall.Reset();
            }
            else if (_move > 0 && stalled)
            {
                _move = contactLeft ? 0 : -1;
                _stall.Reset();
            }
        }
        else
        {
            _move = 0;
            _stall.Reset();
        }

        ApplyRun(entity, _move, gameDt);

        // Set animation variables.
        entity.IsGrounded = entity.OnGround;
        entity.IsMoving = _move != 0;
        entity.Facing = _move;
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
