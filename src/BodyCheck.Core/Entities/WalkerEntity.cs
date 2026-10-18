using Godot;

namespace BodyCheck.Core.Entities;

/// <summary>
/// A body that moves itself by its grip on what it stands on: the player, a robot. That grip is traction,
/// which the engine applies as the motor of a joint between the walker and whatever it stands on.
/// <para>
/// A walker has no engine friction. With it, a walker would grip walls too, and lose its speed on landing.
/// </para>
/// </summary>
public partial class WalkerEntity : Entity
{
    #region Properties for AnimationTree advancement expressions
    /// <summary>True if the entity is on the ground for animation purposes. Set by the controller.</summary>
    public bool IsGrounded { get; set; }
    /// <summary>True if the entity is walking or running. Set by the controller.</summary>
    public bool IsMoving { get; set; }
    /// <summary>True if the entity is jumping. Set by the controller.</summary>
    public bool IsJumping { get; set; }
    /// <summary>The entity's speed relative to its ground support (or to the game world, if no support).</summary>
    public float GroundSpeed => LinearVelocity.X - GroundVelocity.X;
    /// <summary>Facing direction: -1 for left, 0 for toward the camera, 1 for right. Set by the controller.</summary>
    public float Facing { get; set; }
    #endregion

    private readonly Generic6DofJoint3D _motor;
    private Entity? _motorAnchor;

    /// <param name="visual">What to draw for the entity, centered on the middle of its box.</param>
    public WalkerEntity(BodyTuning tuning, Node3D? visual = null)
        // Continuous detection keeps a walker's contacts exact, which its movement depends on.
        : base(tuning, visual, engineFriction: 0, continuousCd: true, axisLockAngularZ: true)
    {
        // A joint that constrains nothing: every limit is off, so its only effect is the X motor. The
        // walker is the joint's second body. The first is what it stands on, or nothing for tiles. The joint
        // is at the feet, where traction acts: higher, its push back would tip what the walker stands on.
        _motor = new Generic6DofJoint3D
        {
            Position = new Vector3(0, -tuning.HalfSize.Y, 0),
            NodeB = new NodePath(".."),
            ExcludeNodesFromCollision = false
        };
        foreach (var flag in (ReadOnlySpan<Generic6DofJoint3D.Flag>)
            [Generic6DofJoint3D.Flag.EnableLinearLimit, Generic6DofJoint3D.Flag.EnableAngularLimit])
        {
            _motor.SetFlagX(flag, false);
            _motor.SetFlagY(flag, false);
            _motor.SetFlagZ(flag, false);
        }
        AddChild(_motor);
    }

    /// <summary>What decides the walker's actions each tick. Without one it is only pushed around.</summary>
    public ICharacterController<WalkerEntity>? Controller { get; set; }

    /// <summary>
    /// Uses the walker's grip on whatever it stands on, for the next step: pushes against the ground to
    /// bring the walker's horizontal speed, relative to the ground, towards <paramref name="targetSpeed"/>,
    /// with at most <paramref name="maxForce"/>, and pushes the ground the other way equally. A target of
    /// zero is braking. It has no effect in the air.
    /// <para>
    /// The engine applies it, as the motor's target speed and force limit.
    /// </para>
    /// </summary>
    public void ApplyTraction(float targetSpeed, float maxForce)
    {
        if (!OnGround || maxForce <= 0)
        {
            return;
        }

        if (Support != _motorAnchor)
        {
            _motorAnchor = Support;
            _motor.NodeA = _motorAnchor is null ? new NodePath() : _motor.GetPathTo(_motorAnchor);
        }
        _motor.SetParamX(Generic6DofJoint3D.Param.LinearMotorTargetVelocity, targetSpeed);
        _motor.SetParamX(Generic6DofJoint3D.Param.LinearMotorForceLimit, maxForce);
        _motor.SetFlagX(Generic6DofJoint3D.Flag.EnableLinearMotor, true);
    }

    protected override void ReleaseMotors() => _motor.SetFlagX(Generic6DofJoint3D.Flag.EnableLinearMotor, false);

    protected override void Control(float dt)
    {
        Controller?.Apply(this, dt);
        UpdateAnimation(dt);
    }

    private void UpdateAnimation(float dt)
    {
        if (Visual is null) return;

        // TODO: This would be better done by a motor. Using a motor would:
        // - Allow stacked walkers/entities to influence each other's facing direction
        // - Require walker collision meshes to be cylinders
        var rot = Visual.Rotation;
        var target = 0.5f * MathF.PI * Facing;
        var newYaw = Mathf.MoveToward(rot.Y, target, 15 * dt);
        Visual.Rotation = new Vector3(rot.X, newYaw, rot.Z);

        if (AnimationTree is not null)
        {
            AnimationTree.Set("parameters/Move/BlendSpace1D/blend_position", Math.Abs(GroundSpeed));
            AnimationTree.Set("parameters/Move/TimeScale/scale", Math.Clamp(Math.Abs(GroundSpeed), 0, 30));
        }
    }
}
