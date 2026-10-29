using Godot;

namespace BodyCheck.Core.Entities;

/// <summary>
/// A body that moves itself by its grip on what it stands on: the player, a robot. Its controller drives it
/// with traction. Its friction is its own, as any body's is, except the player's; see <see cref="PlayerEntity"/>.
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

    // How far the grip's joint may be above or below where it should act before it is anchored again there.
    private const float GripDrift = 0.05f;

    // The joint whose motor grips what the walker stands on: the walker's traction. It is one of Joints' free joints,
    // so its only effect is the X motor. The walker is the joint's second body, and the first is what it is anchored
    // to, or nothing for tiles.
    private readonly Generic6DofJoint3D _grip;
    private bool _gripAnchored;
    private Entity? _gripAnchor;
    // Where the joint is, in the walker's frame: where the engine's joint acts on it.
    private Vector3 _gripPoint;

    /// <param name="visual">What to draw for the entity, centered on the middle of its box.</param>
    public WalkerEntity(BodyTuning tuning, Node3D? visual = null)
        : base(tuning, visual, axisLockAngularZ: true)
    {
        _grip = Joints.FreeJoint();
        AddChild(_grip);
    }

    /// <summary>
    /// Applies traction, at once, for this tick's step: pushes against the ground to bring the walker's horizontal
    /// speed, relative to the ground, towards <paramref name="targetSpeed"/>, with at most
    /// <paramref name="maxForce"/>, and pushes the ground the other way equally. It has no effect in the air.
    /// <para>
    /// The engine applies it, as the target speed and force limit of a motor (019 §1.6).
    /// </para>
    /// </summary>
    public void ApplyTraction(float targetSpeed, float maxForce)
    {
        if (!OnGround || GroundContact is not Vector3 groundContact || maxForce <= 0)
        {
            _grip.SetFlagX(Generic6DofJoint3D.Flag.EnableLinearMotor, false);
            return;
        }

        // The grip acts where the walker touches what it stands on (019 §2.2). The motor pushes only along X, so only
        // the joint's height changes what it does.
        if (!_gripAnchored || Support != _gripAnchor || MathF.Abs(ToGlobal(_gripPoint).Y - groundContact.Y) > GripDrift)
        {
            _gripAnchored = true;
            _gripAnchor = Support;
            _gripPoint = ToLocal(groundContact);
            Joints.Anchor(_grip, nodeA: Support, nodeB: this, groundContact);
        }
        _grip.SetParamX(Generic6DofJoint3D.Param.LinearMotorTargetVelocity, targetSpeed);
        _grip.SetParamX(Generic6DofJoint3D.Param.LinearMotorForceLimit, maxForce);
        _grip.SetFlagX(Generic6DofJoint3D.Flag.EnableLinearMotor, true);
    }

    protected override void DisableMotors()
    {
        base.DisableMotors();
        _grip.SetFlagX(Generic6DofJoint3D.Flag.EnableLinearMotor, false);
    }

    protected override void Control(float gameDt, float realDt, IDefer defer)
    {
        base.Control(gameDt, realDt, defer);
        UpdateAnimation(gameDt);
    }

    private void UpdateAnimation(float gameDt)
    {
        if (Visual is null) return;

        // TODO: This would be better done by a motor. Using a motor would:
        // - Allow stacked walkers/entities to influence each other's facing direction
        // - Require walker collision meshes to be cylinders
        var rot = Visual.Rotation;
        var target = 0.5f * MathF.PI * Facing;
        var newYaw = Mathf.MoveToward(rot.Y, target, 15 * gameDt);
        Visual.Rotation = new Vector3(rot.X, newYaw, rot.Z);

        if (AnimationTree is not null)
        {
            AnimationTree.Set("parameters/Move/BlendSpace1D/blend_position", Math.Abs(GroundSpeed));
            AnimationTree.Set("parameters/Move/TimeScale/scale", Math.Clamp(Math.Abs(GroundSpeed), 0, 30));
        }
    }
}
