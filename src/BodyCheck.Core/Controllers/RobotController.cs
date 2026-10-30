using BodyCheck.Core.Entities;

namespace BodyCheck.Core.Controllers;

/// <param name="entity">The robot, which the controller controls.</param>
/// <param name="target">What the robot aims its laser at, or null for nothing.</param>
public sealed class RobotController(RobotEntity entity, Func<Entity?> target) : IController<RobotEntity>
{
    // The way the robot faces: -1 or 1, or 0, toward the camera, while it is in the air or overheated, when it looks both
    // ways.
    private float _facing = 1;
    private readonly PatrolBehaviour _patrol = new(entity, PatrolTuning.Robot);
    private readonly RunAction _run = new(entity, RunTuning.Robot);
    private readonly LaserAction _laser = new(entity, LaserTuning.Robot);

    public RobotEntity Entity => entity;

    public void Apply(float gameDt, float realDt, IDefer defer)
    {
        entity.Laser = _laser.Apply(entity.LaserOrigin(_facing), _facing, target(), gameDt);
        bool shooting = _laser.Stage != LaserStage.Off;

        // Patrol, unless the target can be seen, or the robot is shooting: then stand still.
        bool patrols = _laser.Seen is null && !shooting;
        (float move, _facing) = _patrol.Apply(patrols, _facing, gameDt);

        // Face the target while it can be seen, but not once the beam has locked, so that the robot faces the way it
        // fires.
        if (_laser.Seen is Entity seen && _laser.Stage is not (LaserStage.Locked or LaserStage.Firing))
        {
            _facing = Math.Sign(seen.Position.X - entity.Position.X);
        }

        // Robots don't slip: their friction is their own, all the time.
        _run.Apply(move, grounded: entity.OnGround, jumps: false, gameDt);

        // Set animation variables.
        entity.IsGrounded = entity.OnGround;
        entity.IsMoving = move != 0;
        entity.Facing = _facing;
    }
}
