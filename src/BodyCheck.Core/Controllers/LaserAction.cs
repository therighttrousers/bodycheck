using BodyCheck.Core.Entities;

using Godot;

namespace BodyCheck.Core.Controllers;

/// <summary>
/// How a <see cref="LaserAction"/> is tuned. Each value is a function that reads it from the features in effect when it
/// is used, so that a change to them takes effect at once. See the robot's, in <see cref="Features"/>, for what each
/// means.
/// </summary>
public sealed record LaserTuning
{
    public required Func<float> Range { get; init; }
    public required Func<float> MaxElevation { get; init; }
    public required Func<float> AimTime { get; init; }
    public required Func<float> LockTime { get; init; }
    public required Func<float> FireTime { get; init; }
    public required Func<float> CooldownTime { get; init; }
    public required Func<float> LoseTime { get; init; }
    public required Func<float> AirTime { get; init; }

    public static LaserTuning Robot { get; } = new()
    {
        Range = () => Features.Current.RobotLaserRange,
        MaxElevation = () => Features.Current.RobotLaserMaxElevation,
        AimTime = () => Features.Current.RobotLaserAimTime,
        LockTime = () => Features.Current.RobotLaserLockTime,
        FireTime = () => Features.Current.RobotLaserFireTime,
        CooldownTime = () => Features.Current.RobotLaserCooldownTime,
        LoseTime = () => Features.Current.RobotLaserLoseTime,
        AirTime = () => Features.Current.RobotLaserAirTime,
    };
}

/// <summary>
/// Firing a laser at a target: looking for it, then stepping through the laser's timeline, from aiming to firing and
/// cooling down. See docs/designs/characters.md.
/// </summary>
/// <param name="entity">The entity that fires: what it sees from, and what must keep its footing.</param>
/// <param name="tuning">How the action is tuned.</param>
public sealed class LaserAction(Entity entity, LaserTuning tuning)
{
    // What a laser's beam stops at, and what hides a target from it.
    private const PhysicsLayer Blockers = PhysicsLayer.DefaultCollisionMask;

    // How long the laser has been in its stage, or, while it is off, how long until it can aim again.
    private float _time;
    // How long the entity has lost sight of its target while aiming.
    private float _lostTime;
    // How long the entity has been off the ground while its laser is on.
    private float _airTime;
    // Which way the beam points: at the target while aiming, and fixed once it locks.
    private Vector3 _aim;

    /// <summary>Where the laser is in its timeline, as of the last tick.</summary>
    public LaserStage Stage { get; private set; } = LaserStage.Off;

    /// <summary>The target, if the entity could see it on the last tick.</summary>
    public Entity? Seen { get; private set; }

    /// <summary>Steps the laser through its timeline.</summary>
    /// <param name="origin">Where the beam starts, and where the entity sees from, in world coordinates.</param>
    /// <param name="facing">
    /// The way the entity faces: -1 for left, 1 for right, or 0, toward the camera, when it sees both ways.
    /// </param>
    /// <param name="target">What to aim at, or null for nothing.</param>
    /// <param name="gameDt">The tick's length in game time.</param>
    /// <returns>The laser, for what draws it.</returns>
    public Laser Apply(Vector3 origin, float facing, Entity? target, float gameDt)
    {
        Seen = target is not null && Sees(origin, facing, target) ? target : null;

        _time = Stage == LaserStage.Off ? MathF.Max(_time - gameDt, 0) : _time + gameDt;
        switch (Stage)
        {
            case LaserStage.Off when _time <= 0 && Seen is not null && entity.OnGround:
                Stage = LaserStage.Aiming;
                _time = 0;
                _lostTime = 0;
                _airTime = 0;
                break;

            case LaserStage.Aiming:
                _lostTime = Seen is null ? _lostTime + gameDt : 0;
                if (_lostTime >= tuning.LoseTime())
                {
                    Stage = LaserStage.Off;
                    _time = tuning.CooldownTime();
                }
                else if (_time >= tuning.AimTime())
                {
                    Stage = LaserStage.Locked;
                    _time = 0;
                }
                break;

            case LaserStage.Locked when _time >= tuning.LockTime():
                Stage = LaserStage.Firing;
                _time = 0;
                break;

            case LaserStage.Firing when _time >= tuning.FireTime():
                Stage = LaserStage.Off;
                _time = tuning.CooldownTime();
                break;
        }

        // In any stage, an entity that loses its footing for long, such as by being phased through or thrown, gives up.
        // One can be carried or pushed along while it shoots, which moves its beam.
        if (Stage != LaserStage.Off)
        {
            _airTime = entity.OnGround ? 0 : _airTime + gameDt;
            if (_airTime >= tuning.AirTime())
            {
                Stage = LaserStage.Off;
                _time = tuning.CooldownTime();
            }
        }

        // While aiming, the beam follows the target even where it has lost sight of it for a moment.
        if (Stage == LaserStage.Aiming && target is not null)
        {
            _aim = (target.Position - origin).Normalized();
        }

        if (Stage == LaserStage.Off)
        {
            return new Laser(LaserStage.Off, 0, origin, origin);
        }
        Vector3 end = origin + tuning.Range() * _aim;
        if (entity.CastRay(origin, end, Blockers) is (Vector3 hit, _))
        {
            end = hit;
        }
        return new Laser(Stage, _time / StageDuration(Stage), origin, end);
    }

    private float StageDuration(LaserStage stage)
    {
        return stage switch
        {
            LaserStage.Off => float.PositiveInfinity,
            LaserStage.Aiming => tuning.AimTime(),
            LaserStage.Locked => tuning.LockTime(),
            LaserStage.Firing => tuning.FireTime(),
        };
    }

    // Whether the entity can target the other from origin: in front of it, not too far above or below level, in range,
    // and with nothing in between.
    private bool Sees(Vector3 origin, float facing, Entity target)
    {
        Vector3 toTarget = target.Position - origin;
        float ahead = toTarget.X * (facing == 0 ? Math.Sign(toTarget.X) : facing);
        if (ahead <= 0
            || MathF.Abs(toTarget.Y) > ahead * MathF.Tan(Mathf.DegToRad(tuning.MaxElevation()))
            || toTarget.Length() > tuning.Range())
        {
            return false;
        }
        return entity.CastRay(origin, target.Position, Blockers) is (_, GodotObject collider) && collider == target;
    }
}
