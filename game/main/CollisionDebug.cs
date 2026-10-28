using System.Collections.Generic;

using BodyCheck.Core;
using BodyCheck.Look;

using Godot;

namespace BodyCheck;

/// <summary>
/// Shows the collisions in the game's viewport as Godot does with visible collision shapes on: every enabled
/// collision shape as Godot's own debug wireframe, so that a body's shape can be compared with its model, and every
/// contact point as a small diamond, in Godot's colour for contacts. F4 switches it on and off.
/// <para>
/// Each shape shows as a child mesh, added to it while showing and removed when not. The contacts are those of the
/// bodies that report theirs, which every entity does, as of the engine's last step: a contact between two of them
/// shows twice, a point on each. While showing, the shapes and contacts are read from the engine each frame, so it
/// shows whatever world is loaded, and entities added since. While not, it doesn't process, so it costs nothing.
/// </para>
/// </summary>
public partial class CollisionDebug : Node
{
    // The name of the mesh added to each shape, by which it is found again.
    private const string MeshName = "DebugMeshInstance";

    // A contact point's radius, in tiles.
    private const float PointRadius = 1.5f / Constants.TilePixels;

    private readonly WorldViewportContainer _worldView;
    private readonly MultiMeshInstance3D _points;
    // This frame's contact positions, kept to save allocating.
    private readonly List<Vector3> _positions = [];

    public CollisionDebug(WorldViewportContainer worldView)
    {
        _worldView = worldView;
        var material = new StandardMaterial3D
        {
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            // Seen through the bodies, as the points are inside them.
            NoDepthTest = true,
            AlbedoColor = ProjectSettings.GetSetting("debug/shapes/collision/contact_color").AsColor(),
        };
        // A sphere with four sides and one ring is a diamond.
        var mesh = new SphereMesh
        {
            Radius = PointRadius,
            Height = 2 * PointRadius,
            RadialSegments = 4,
            Rings = 1,
            Material = material,
        };
        _points = new MultiMeshInstance3D
        {
            Multimesh = new MultiMesh { TransformFormat = MultiMesh.TransformFormatEnum.Transform3D, Mesh = mesh },
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            // An instance is whichever contact comes at its index each frame, so it mustn't be interpolated from the
            // last one there.
            PhysicsInterpolationMode = PhysicsInterpolationModeEnum.Off,
            Visible = false,
        };
    }

    /// <summary>Whether the collisions show, which is whether this processes.</summary>
    public bool Visible
    {
        get => IsProcessing();
        set
        {
            SetProcess(value);
            _points.Visible = value;
            // At once, so that the meshes go when it stops processing.
            Show(value);
        }
    }

    public override void _Ready()
    {
        // Stay active when the game is paused.
        ProcessMode = ProcessModeEnum.Always;
        // Godot turns processing on before this, because _Process is overridden.
        SetProcess(false);
        _worldView.World.AddChild(_points);
    }

    public override void _UnhandledKeyInput(InputEvent @event)
    {
        if (@event is InputEventKey { Pressed: true, Echo: false, PhysicalKeycode: Key.F4 })
        {
            Visible = !Visible;
        }
    }

    public override void _Process(double delta) => Show(true);

    /// <summary>Adds every enabled shape's debug mesh and shows every contact, or removes the meshes.</summary>
    private void Show(bool show)
    {
        _positions.Clear();
        // Every shape is a child of one of these, since a shape works only there.
        foreach (Node node in _worldView.World.FindChildren("*", nameof(CollisionObject3D), owned: false))
        {
            foreach (Node child in node.GetChildren())
            {
                if (child is CollisionShape3D shape)
                {
                    ShowMesh(shape, show && !shape.Disabled);
                }
            }
            if (show && node is RigidBody3D { ContactMonitor: true } body)
            {
                AddContacts(body);
            }
        }
        ShowPoints();
    }

    /// <summary>Adds the shape's debug mesh, or removes it, unless it is already there or gone.</summary>
    private static void ShowMesh(CollisionShape3D shape, bool show)
    {
        var mesh = shape.GetNodeOrNull<MeshInstance3D>(MeshName);
        if (show && mesh is null && shape.Shape is not null)
        {
            shape.AddChild(new MeshInstance3D { Name = MeshName, Mesh = shape.Shape.GetDebugMesh() });
        }
        else if (!show && mesh is not null)
        {
            mesh.QueueFree();
        }
    }

    /// <summary>Adds the body's contact positions to this frame's.</summary>
    private void AddContacts(RigidBody3D body)
    {
        // A body out of the space, such as a disabled one, has no state to read.
        if (!PhysicsServer3D.BodyGetSpace(body.GetRid()).IsValid)
        {
            return;
        }
        PhysicsDirectBodyState3D state = PhysicsServer3D.BodyGetDirectState(body.GetRid());
        for (int i = 0; i < state.GetContactCount(); i++)
        {
            // Global, whatever the method's name says.
            _positions.Add(state.GetContactLocalPosition(i));
        }
    }

    /// <summary>Puts a point at each of this frame's contact positions.</summary>
    private void ShowPoints()
    {
        MultiMesh multimesh = _points.Multimesh;
        if (_positions.Count > multimesh.InstanceCount)
        {
            // Grown only, since changing it reallocates the instances.
            multimesh.InstanceCount = _positions.Capacity;
        }
        for (int i = 0; i < _positions.Count; i++)
        {
            multimesh.SetInstanceTransform(i, new Transform3D(Basis.Identity, _positions[i]));
        }
        multimesh.VisibleInstanceCount = _positions.Count;
    }
}
