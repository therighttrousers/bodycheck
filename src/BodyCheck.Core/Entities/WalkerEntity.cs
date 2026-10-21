using Godot;

namespace BodyCheck.Core.Entities;

/// <summary>
/// A body that moves itself by its grip on what it stands on: the player, a robot. Its controller adds
/// traction to the entity's friction.
/// <para>
/// A walker has no engine friction. With it, a walker would grip walls too, and lose its speed on landing.
/// Its friction with what it stands on is the entity's own, with a coefficient from its tuning.
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

    /// <param name="visual">What to draw for the entity, centered on the middle of its box.</param>
    public WalkerEntity(BodyTuning tuning, Node3D? visual = null)
        // Continuous detection keeps a walker's contacts exact, which its movement depends on.
        : base(tuning, visual, engineFriction: 0, continuousCd: true, axisLockAngularZ: true)
    {
    }

    /// <summary>What decides the walker's actions each tick. Without one it is only pushed around.</summary>
    public ICharacterController<WalkerEntity>? Controller { get; set; }

    /// <summary>
    /// Adds traction to the walker's friction, for the next step: pushes against the ground to bring the walker's
    /// horizontal speed, relative to the ground, towards <paramref name="targetSpeed"/>, with at most
    /// <see cref="Entity.Friction"/> plus <paramref name="extraForce"/>, and pushes the ground the other way equally.
    /// The extra force may be negative, to take off friction that the controller doesn't want: at most nothing is
    /// left. It has no effect in the air.
    /// <para>
    /// The engine applies it, as the target speed and force limit of the motor that friction uses (019 §1.6).
    /// </para>
    /// </summary>
    public void ApplyTraction(float targetSpeed, float extraForce) => DriveGrip(targetSpeed, Friction + extraForce);

    public override bool RequestsPause => Controller?.RequestsPause ?? false;

    protected override void ControlPaused(float realDt) => Controller?.ApplyPaused(this, realDt);

    protected override void Control(float gameDt, float realDt)
    {
        Controller?.Apply(this, gameDt, realDt);
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
