using Godot;

namespace BodyCheck.Core;

/// <summary>
/// A body that has a controller and moves only by adding forces and impulses. (It has no motors.)
/// </summary>
public partial class SimpleControlledEntity(BodyTuning tuning, Node3D? visual = null) : Entity(tuning, visual)
{
    /// <summary>What decides the entity's actions each tick.</summary>
    public ICharacterController<SimpleControlledEntity>? Controller { get; set; }

    protected override void Control(float dt) => Controller?.Apply(this, dt);
}
