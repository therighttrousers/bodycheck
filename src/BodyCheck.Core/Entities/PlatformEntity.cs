using Godot;

namespace BodyCheck.Core.Entities;

/// <summary>
/// A hovering body that moves itself with thrust. Its controller publishes how hard that is, for its model's face.
/// </summary>
public partial class PlatformEntity : Entity
{
    #region Properties for AnimationTree advancement expressions
    /// <summary>
    /// How hard the platform works against what is pushing it around, from 0 to 1, its most: what it carries, what is in
    /// its way, or being off its line. Moving itself along its line doesn't count, nor does letting a load carry it the
    /// way it is going. Set by the controller.
    /// </summary>
    public float Strain { get; set; }
    #endregion

    /// <param name="visual">What to draw for the entity, with its origin at the body's anchor.</param>
    public PlatformEntity(BodyTuning tuning, Node3D? visual = null)
        : base(tuning, visual)
    {
    }
}
