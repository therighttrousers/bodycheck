using Godot;

namespace BodyCheck.Core.Entities;

/// <summary>The joints that entities use for their motors, which constrain nothing themselves.</summary>
internal static class Joints
{
    /// <summary>
    /// Returns a joint that constrains nothing: every limit is off, so its only effects are the motors that are
    /// switched on. It has no bodies until it is first anchored.
    /// </summary>
    public static Generic6DofJoint3D FreeJoint()
    {
        var joint = new Generic6DofJoint3D { ExcludeNodesFromCollision = false };
        foreach (var flag in (ReadOnlySpan<Generic6DofJoint3D.Flag>)
            [Generic6DofJoint3D.Flag.EnableLinearLimit, Generic6DofJoint3D.Flag.EnableAngularLimit])
        {
            joint.SetFlagX(flag, false);
            joint.SetFlagY(flag, false);
            joint.SetFlagZ(flag, false);
        }
        return joint;
    }

    /// <summary>
    /// Anchors a joint to two nodes, at a point. A null node is the world, as for tiles.
    /// <para>
    /// Godot builds the engine's joint whenever a node path changes to a new value, and fixes the joint's place in each
    /// body then, from where the joint node is. So the joint is put at the point, level, first. Both paths
    /// are cleared before they are set, so that setting them builds the joint even when neither node has changed.
    /// </para>
    /// </summary>
    public static void Anchor(Generic6DofJoint3D joint, Node3D? nodeA, Node3D? nodeB, Vector3 point)
    {
        joint.GlobalTransform = new Transform3D(Basis.Identity, point);
        joint.NodeA = new NodePath();
        joint.NodeB = new NodePath();
        if (nodeA is not null) joint.NodeA = joint.GetPathTo(nodeA);
        if (nodeB is not null) joint.NodeB = joint.GetPathTo(nodeB);
    }
}
