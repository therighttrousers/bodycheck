using Godot;

namespace BodyCheck.Core.Entities;

/// <summary>
/// The player's body: a walker whose friction changes with what it touches and does, as no other body's does.
/// <para>
/// While it stands on something and its controller doesn't let it slip, it has its tuning's ground coefficient, rough,
/// so that it wins over what it stands on, tiles or a body, and grips what it carries. Otherwise, in the air or
/// slipping, it has its tuning's engine friction, 0, so that it slides along walls, and traction alone moves it.
/// See docs/designs/movement.md.
/// </para>
/// </summary>
public partial class PlayerEntity : WalkerEntity
{
    // Whether the controller lets the player slip this tick. See SlipAfterEntitySteps.
    private bool _slipAfterEntitySteps;

    /// <param name="visual">What to draw for the entity, centered on the middle of its box.</param>
    public PlayerEntity(BodyTuning tuning, Node3D? visual = null)
        : base(tuning, visual)
    {
    }

    /// <summary>
    /// Lets the player slip on what it stands on in this tick's step: after it steps, its friction goes off. For a tick
    /// in which friction shouldn't resist the player sliding against its support, such as when it moves itself along
    /// it, or leaves it with its speed. It lasts one tick.
    /// </summary>
    public void SlipAfterEntitySteps() => _slipAfterEntitySteps = true;

    // Set every tick after the player steps instead.
    protected override void UpdateFrictionFromFeatures()
    {
    }

    // From what the player stood on during the last step, and whether its controller let it slip this tick.
    protected override void SetFrictionAfterEntitySteps(IDefer defer)
    {
        defer.RunAfterEntitySteps(() =>
        {
            bool slips = _slipAfterEntitySteps;
            _slipAfterEntitySteps = false;
            if (!slips && OnGround && Tuning.GroundFriction?.Invoke() is float groundFriction)
            {
                SetFriction(groundFriction, rough: true);
            }
            else
            {
                SetFriction(Tuning.EngineFriction(), rough: false);
            }
        });
    }
}
