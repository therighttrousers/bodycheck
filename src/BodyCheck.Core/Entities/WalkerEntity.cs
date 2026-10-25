using Godot;

namespace BodyCheck.Core.Entities;

/// <summary>
/// A body that moves itself by its grip on what it stands on: the player, a robot. Its controller drives it
/// with traction.
/// <para>
/// A walker's engine friction is only with what it stands on, with its tuning's ground coefficient: off the
/// ground it has none, so that it slides along walls. While it drives, its friction is off and traction alone
/// moves it (027).
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
        : base(tuning, visual, continuousCd: true, axisLockAngularZ: true)
    {
    }

    /// <summary>What decides the walker's actions each tick. Without one it is only pushed around.</summary>
    public ICharacterController<WalkerEntity>? Controller { get; set; }

    /// <summary>
    /// Applies traction, at once, for this tick's step: pushes against the ground to bring the walker's horizontal
    /// speed, relative to the ground, towards <paramref name="targetSpeed"/>, with at most <paramref name="force"/>, and
    /// pushes the ground the other way equally. It has no effect in the air.
    /// <para>
    /// The engine applies it, as the target speed and force limit of a motor (019 §1.6).
    /// </para>
    /// </summary>
    /// <param name="slipAtEndOfTick">
    /// Whether the walker's friction goes off at the end of the tick, for this tick's step, so that the traction alone
    /// moves it against its support. Otherwise friction acts too, against any slip, as when it holds an idle walker
    /// still.
    /// </param>
    public void ApplyTraction(float targetSpeed, float force, bool slipAtEndOfTick)
    {
        if (slipAtEndOfTick)
        {
            SlipAtEndOfTick();
        }
        DriveGrip(targetSpeed, force);
    }

    public override bool RequestsPause => Controller?.RequestsPause ?? false;

    protected override void ControlPaused(float realDt, IEndOfTick endOfTick) =>
        Controller?.ApplyPaused(this, realDt, endOfTick);

    protected override void Control(float gameDt, float realDt, IEndOfTick endOfTick)
    {
        Controller?.Apply(this, gameDt, realDt, endOfTick);
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
