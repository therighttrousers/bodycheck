using BodyCheck.Core.Entities;

namespace BodyCheck.Core.Controllers;

/// <param name="entity">The robot, which the controller controls.</param>
/// <param name="target">What the robot aims its laser at, or null for nothing.</param>
public sealed class RobotController(RobotEntity entity, Func<Entity?> target) : IController<RobotEntity>
{
    private const float ArbitraryFirstMove = 1;

    private float _move;
    // The way the robot faces: -1 or 1, or 0, toward the camera, while it is in the air or overheated, when it looks both
    // ways.
    private float _facing = ArbitraryFirstMove;
    private readonly StallDetector _stall = new();
    private readonly OverheatDetector _stallHeat = new();
    private readonly RunAction _run = new(entity, RunTuning.Robot);
    private readonly LaserAction _laser = new(entity, LaserTuning.Robot);

    public RobotEntity Entity => entity;

    public void Apply(float gameDt, float realDt, IDefer defer)
    {
        var features = Features.Current;

        _stallHeat.CoolDown(features.RobotStallCooldownRate * gameDt);

        entity.Laser = _laser.Apply(entity.LaserOrigin(_facing), _facing, target(), gameDt);
        bool shooting = _laser.Stage != LaserStage.Off;

        // Stand still and face the target while it can be seen, but not once the beam has locked, so that the robot faces
        // the way it fires. Otherwise patrol if on tiles, unless shooting. Turn around when stalled. Pause when stalled
        // too often.
        if (_laser.Seen is not null)
        {
            if (_laser.Stage is not (LaserStage.Locked or LaserStage.Firing))
            {
                _facing = Math.Sign(_laser.Seen.Position.X - entity.Position.X);
            }
            _move = 0;
            _stall.Reset();
        }
        else if (entity.OnGround && entity.Support is null && !shooting)
        {
            bool contactLeft = entity.Contacts.HasFlag(Contacts.Left);
            bool contactRight = entity.Contacts.HasFlag(Contacts.Right);

            bool stalled = _stall.Update(
                entity.LinearVelocity.X * _move,
                gameDt,
                features.RobotStallSpeed,
                features.RobotStallTime);

            if (stalled)
            {
                _stallHeat.HeatUp(features.RobotStallHeat, features.RobotStallMaxHeat);
            }

            if (_stallHeat.Overheated)
            {
                _facing = _move = 0;
                _stall.Reset();
            }
            else if (_move == 0)
            {
                _facing = _move = contactLeft ? (contactRight ? 0 : 1) : contactRight ? -1 : _facing != 0 ? _facing : ArbitraryFirstMove;
                _stall.Reset();
            }
            else if (_move < 0 && stalled)
            {
                _facing = _move = contactRight ? 0 : 1;
                _stall.Reset();
            }
            else if (_move > 0 && stalled)
            {
                _facing = _move = contactLeft ? 0 : -1;
                _stall.Reset();
            }
        }
        else if (!shooting)
        {
            _facing = _move = 0;
            _stall.Reset();
        }
        else
        {
            // Stand still to shoot, then go on the way it faces.
            _move = 0;
            _stall.Reset();
        }

        // Robots don't slip: their friction is their own, all the time.
        _run.Apply(_move, grounded: entity.OnGround, jumps: false, gameDt);

        // Set animation variables.
        entity.IsGrounded = entity.OnGround;
        entity.IsMoving = _move != 0;
        entity.Facing = _facing;
    }
}
