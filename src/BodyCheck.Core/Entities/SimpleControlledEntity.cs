using Godot;

namespace BodyCheck.Core.Entities;

/// <summary>
/// A body that has a controller and moves only by adding forces and impulses. (It has no motors.)
/// </summary>
public partial class SimpleControlledEntity(BodyTuning tuning, Node3D? visual = null) : Entity(tuning, visual)
{
    /// <summary>What decides the entity's actions each tick.</summary>
    public ICharacterController<SimpleControlledEntity>? Controller { get; set; }

    public override bool RequestsPause => Controller?.RequestsPause ?? false;

    protected override void Control(float gameDt, float realDt, IEndOfTick endOfTick) =>
        Controller?.Apply(this, gameDt, realDt, endOfTick);

    protected override void ControlPaused(float realDt, IEndOfTick endOfTick) =>
        Controller?.ApplyPaused(this, realDt, endOfTick);
}
