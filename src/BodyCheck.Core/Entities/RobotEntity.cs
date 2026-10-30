using Godot;

namespace BodyCheck.Core.Entities;

/// <summary>Where a robot's laser is in its firing timeline. See docs/designs/characters.md.</summary>
public enum LaserStage
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

/// <summary>A robot's laser as of the last tick: its stage, and where its beam starts and ends, in world coordinates.</summary>
public readonly record struct Laser(LaserStage Stage, float Progress, Vector3 Origin, Vector3 End);

/// <summary>
/// A robot's body: a walker with a laser. Its controller sets the laser each tick, for what draws the robot to show.
/// </summary>
public partial class RobotEntity : WalkerEntity
{
    // Where the laser starts without a "LaserOrigin" in the visual: above the middle of the robot's box.
    private const float DefaultLaserHeight = 0.8f;

    // Where the laser starts, relative to the middle of the robot's box: how far above it, and how far ahead of it in the
    // direction the robot faces.
    private readonly float _laserHeight = DefaultLaserHeight;
    private readonly float _laserForward;

    /// <param name="visual">
    /// What to draw for the entity, centered on the middle of its box. A node in it named "LaserOrigin" says where the
    /// laser starts, as it is in the model's rest pose, facing the camera; see <see cref="LaserOrigin"/>.
    /// </param>
    public RobotEntity(BodyTuning tuning, Node3D? visual = null)
        : base(tuning, visual)
    {
        if (visual is not null && RestPosition(visual, "LaserOrigin") is Vector3 origin)
        {
            _laserHeight = origin.Y - HalfSize.Y;
            // The model faces +Z at rest, which turns to +X when it faces right.
            _laserForward = origin.Z;
        }
    }

    /// <summary>
    /// Where the robot's laser starts, in world coordinates, as of the last step, when it faces
    /// <paramref name="facing"/>: -1 for left, 0 for toward the camera, 1 for right. It is fixed to the robot's body, so
    /// that animation doesn't move it.
    /// </summary>
    public Vector3 LaserOrigin(float facing) => Position + new Vector3(facing * _laserForward, _laserHeight, 0);

    // Where the node named name is relative to visual, with any bone it is attached to in its rest pose. Null if there is
    // no such node.
    private static Vector3? RestPosition(Node3D visual, string name)
    {
        if (visual.FindChild(name, recursive: true, owned: false) is not Node3D node)
        {
            return null;
        }
        Transform3D toVisual = Transform3D.Identity;
        for (Node3D n = node; n != visual; n = n.GetParent<Node3D>())
        {
            Transform3D local = n is BoneAttachment3D bone && bone.GetParent() is Skeleton3D skeleton
                ? skeleton.GetBoneGlobalRest(bone.BoneIdx)
                : n.Transform;
            toVisual = local * toVisual;
        }
        return toVisual.Origin;
    }

    /// <summary>The robot's laser as of the last tick. Set by the controller.</summary>
    public Laser Laser { get; set; }
}
