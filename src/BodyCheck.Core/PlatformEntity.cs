using Godot;

namespace BodyCheck.Core;

/// <summary>
/// A body that hovers: a platform. It has no motor yet. Its controller holds it up and moves it with
/// plain forces.
/// </summary>
public partial class PlatformEntity : Entity
{
    /// <param name="visual">What to draw for the entity, centered on the middle of its box.</param>
    public PlatformEntity(BodyTuning tuning, Node3D? visual = null)
        : base(tuning, visual)
    {
    }

    /// <summary>What decides the platform's actions each tick. Without one it only falls.</summary>
    public ICharacterController<PlatformEntity>? Controller { get; set; }

    protected override void Control(float dt) => Controller?.Apply(this, dt);
}
