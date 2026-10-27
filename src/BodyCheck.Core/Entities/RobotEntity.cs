using Godot;

namespace BodyCheck.Core.Entities;

/// <summary>Where a robot's laser is in its firing timeline. See docs/designs/characters.md.</summary>
public enum LaserPhase
{
    /// <summary>No beam: the robot is looking for a target, or cooling down.</summary>
    Off,

    /// <summary>A thin beam that follows the target.</summary>
    Aiming,

    /// <summary>A thicker beam that has stopped following the target.</summary>
    Locked,

    /// <summary>The full beam, which would do damage.</summary>
    Firing,
}

/// <summary>A robot's laser as of the last tick: its phase, and where its beam starts and ends, in world coordinates.</summary>
public readonly record struct Laser(LaserPhase Phase, float Progress, Vector3 Origin, Vector3 End);

/// <summary>
/// A robot's body: a walker with a laser. Its controller sets the laser each tick, for what draws the robot to show.
/// </summary>
public partial class RobotEntity : WalkerEntity
{
    /// <param name="visual">What to draw for the entity, centered on the middle of its box.</param>
    public RobotEntity(BodyTuning tuning, Node3D? visual = null)
        : base(tuning, visual)
    {
    }

    /// <summary>
    /// What decides the robot's actions each tick. It hides <see cref="WalkerEntity.Controller"/>, which a robot
    /// doesn't run, so that the controller can set the robot's laser.
    /// </summary>
    public new ICharacterController<RobotEntity>? Controller { get; set; }

    /// <summary>The robot's laser as of the last tick. Set by the controller.</summary>
    public Laser Laser { get; set; }

    public override bool RequestsPause => Controller?.RequestsPause ?? false;

    protected override void ControlPaused(float realDt, IDefer defer) =>
        Controller?.ApplyPaused(this, realDt, defer);

    protected override void ApplyController(float gameDt, float realDt, IDefer defer) =>
        Controller?.Apply(this, gameDt, realDt, defer);
}
