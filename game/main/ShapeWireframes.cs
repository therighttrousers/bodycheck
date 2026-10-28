using BodyCheck.Look;

using Godot;

namespace BodyCheck;

/// <summary>
/// Shows every enabled collision shape in the game's viewport as Godot's own debug wireframe, so that a body's shape
/// can be compared with its model. F4 switches it on and off.
/// <para>
/// Each shape shows as a child mesh, added to it while showing and removed when not. While showing, the shapes are
/// read from the engine each frame, so it shows whatever world is loaded, and entities added since. While not, it
/// doesn't process, so it costs nothing.
/// </para>
/// </summary>
public partial class ShapeWireframes : Node
{
    // The name of the mesh added to each shape, by which it is found again.
    private const string MeshName = "DebugMeshInstance";

    private readonly WorldViewportContainer _worldView;

    public ShapeWireframes(WorldViewportContainer worldView) => _worldView = worldView;

    /// <summary>Whether the shapes show, which is whether this processes.</summary>
    public bool Visible
    {
        get => IsProcessing();
        set
        {
            SetProcess(value);
            // At once, so that the meshes go when it stops processing.
            ShowMeshes(value);
        }
    }

    public override void _Ready()
    {
        // Stay active when the game is paused.
        ProcessMode = ProcessModeEnum.Always;
        // Godot turns processing on before this, because _Process is overridden.
        SetProcess(false);
    }

    public override void _UnhandledKeyInput(InputEvent @event)
    {
        if (@event is InputEventKey { Pressed: true, Echo: false, PhysicalKeycode: Key.F4 })
        {
            Visible = !Visible;
        }
    }

    public override void _Process(double delta) => ShowMeshes(true);

    /// <summary>Adds every enabled shape's debug mesh, or removes them all.</summary>
    private void ShowMeshes(bool show)
    {
        foreach (Node node in _worldView.World.FindChildren("*", nameof(CollisionShape3D), owned: false))
        {
            if (node is CollisionShape3D shape)
            {
                ShowMesh(shape, show && !shape.Disabled);
            }
        }
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
}
