using BodyCheck.Core.Entities;

using Godot;

namespace BodyCheck.Core.Controllers;

/// <param name="target">What the robot aims its laser at, or null for nothing.</param>
public sealed class RobotController(Func<Entity?> target) : ICharacterController<RobotEntity>
{
    private const float ArbitraryFirstMove = 1;

    // Where the laser starts, above the robot's center: about where its eyes are.
    private const float EyeHeight = 0.8f;

    // What a laser's beam stops at, and what hides a target from it.
    private const PhysicsLayer LaserBlockers = PhysicsLayer.DefaultCollisionMask;

    private float _move;
    // The way the robot faces: -1 or 1, or 0, toward the camera, while it is in the air or overheated, when it looks both
    // ways.
    private float _facing = ArbitraryFirstMove;
    private readonly StallDetector _stall = new();
    private readonly OverheatDetector _stallHeat = new();

    private Entity? _seen;
    private LaserPhase _laserPhase = LaserPhase.Off;
    // How long the laser has been in its phase, or, while it is off, how long until it can aim again.
    private float _laserTime;
    // How long the robot has lost sight of its target while aiming.
    private float _lostTime;
    // How long the robot has been off the ground while its laser is on.
    private float _airTime;
    // Which way the beam points: at the target while aiming, and fixed once it locks.
    private Vector3 _aim;

    public void Apply(RobotEntity entity, float gameDt, float realDt, IDefer defer)
    {
        var features = Features.Current;

        _stallHeat.CoolDown(features.RobotStallCooldownRate * gameDt);

        ApplyLaser(entity, gameDt);
        bool shooting = _laserPhase != LaserPhase.Off;

        // Stand still and face the target while it can be seen, but not once the beam has locked, so that the robot faces
        // the way it fires. Otherwise patrol if on tiles, unless shooting. Turn around when stalled. Pause when stalled
        // too often.
        if (_seen is not null)
        {
            if (_laserPhase is not (LaserPhase.Locked or LaserPhase.Firing))
            {
                _facing = Math.Sign(_seen.Position.X - entity.Position.X);
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

        ApplyRun(entity, _move, gameDt);

        // Set animation variables.
        entity.IsGrounded = entity.OnGround;
        entity.IsMoving = _move != 0;
        entity.Facing = _facing;
    }

    // Steps the laser through its timeline (see docs/designs/characters.md), and sets the entity's laser for what draws it.
    private void ApplyLaser(RobotEntity entity, float gameDt)
    {
        var features = Features.Current;
        Vector3 origin = entity.Position + new Vector3(0, EyeHeight, 0);
        _seen = target() is Entity t && Sees(entity, origin, t) ? t : null;

        _laserTime = _laserPhase == LaserPhase.Off ? MathF.Max(_laserTime - gameDt, 0) : _laserTime + gameDt;
        switch (_laserPhase)
        {
            case LaserPhase.Off when _laserTime <= 0 && _seen is not null && entity.OnGround:
                _laserPhase = LaserPhase.Aiming;
                _laserTime = 0;
                _lostTime = 0;
                _airTime = 0;
                break;

            case LaserPhase.Aiming:
                _lostTime = _seen is null ? _lostTime + gameDt : 0;
                if (_lostTime >= features.RobotLaserLoseTime)
                {
                    _laserPhase = LaserPhase.Off;
                    _laserTime = features.RobotLaserCooldownTime;
                }
                else if (_laserTime >= features.RobotLaserAimTime)
                {
                    _laserPhase = LaserPhase.Locked;
                    _laserTime = 0;
                }
                break;

            case LaserPhase.Locked when _laserTime >= features.RobotLaserLockTime:
                _laserPhase = LaserPhase.Firing;
                _laserTime = 0;
                break;

            case LaserPhase.Firing when _laserTime >= features.RobotLaserFireTime:
                _laserPhase = LaserPhase.Off;
                _laserTime = features.RobotLaserCooldownTime;
                break;
        }

        // In any phase, a robot that loses its footing for long, such as by being phased through or thrown, gives up. A
        // robot can be carried or pushed along while it shoots, which moves its beam.
        if (_laserPhase != LaserPhase.Off)
        {
            _airTime = entity.OnGround ? 0 : _airTime + gameDt;
            if (_airTime >= features.RobotLaserAirTime)
            {
                _laserPhase = LaserPhase.Off;
                _laserTime = features.RobotLaserCooldownTime;
            }
        }

        // While aiming, the beam follows the target even where it has lost sight of it for a moment.
        if (_laserPhase == LaserPhase.Aiming && target() is Entity aimed)
        {
            _aim = (aimed.Position - origin).Normalized();
        }

        if (_laserPhase == LaserPhase.Off)
        {
            entity.Laser = new Laser(LaserPhase.Off, origin, origin);
            return;
        }
        Vector3 end = origin + features.RobotLaserRange * _aim;
        if (entity.CastRay(origin, end, LaserBlockers) is (Vector3 hit, _))
        {
            end = hit;
        }
        entity.Laser = new Laser(_laserPhase, origin, end);
    }

    // Whether the robot can target the entity from its eyes: in front of it, not too far above or below level, in range,
    // and with nothing in between.
    private bool Sees(RobotEntity entity, Vector3 origin, Entity target)
    {
        var features = Features.Current;
        Vector3 toTarget = target.Position - origin;
        float ahead = toTarget.X * (_facing == 0 ? Math.Sign(toTarget.X) : _facing);
        if (ahead <= 0
            || MathF.Abs(toTarget.Y) > ahead * MathF.Tan(Mathf.DegToRad(features.RobotLaserMaxElevation))
            || toTarget.Length() > features.RobotLaserRange)
        {
            return false;
        }
        return entity.CastRay(origin, target.Position, LaserBlockers) is (_, GodotObject collider) && collider == target;
    }

    private void ApplyRun(WalkerEntity entity, float move, float gameDt)
    {
        if (!entity.OnGround)
        {
            return;
        }
        // Robots don't slip: their friction is their own, all the time.
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
