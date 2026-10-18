using BodyCheck.Look;

using Godot;

namespace BodyCheck;

/// <summary>
/// Draws every box collision shape in the game's viewport as a wireframe, over the game, so that a body's
/// box can be compared with its model. F4 switches it on and off.
/// <para>
/// It reads the shapes and the camera from the engine each frame, so it draws whatever world is loaded. Like
/// the <see cref="DebugOverlay"/>, it belongs in the window's own viewport, where it is drawn at the window's
/// full resolution and the colour pass doesn't touch it.
/// </para>
/// </summary>
public partial class ShapeWireframes : Node2D
{
    private static readonly Color LineColor = new(0.2f, 1, 0.4f, 0.8f);

    // The corners of a box with half size 1, and the pairs of them that its 12 edges join.
    private static readonly Vector3[] Corners = [
        new(-1, -1, -1), new(1, -1, -1), new(1, 1, -1), new(-1, 1, -1),
        new(-1, -1, 1), new(1, -1, 1), new(1, 1, 1), new(-1, 1, 1),
    ];
    private static readonly (int, int)[] Edges = [
        (0, 1), (1, 2), (2, 3), (3, 0),
        (4, 5), (5, 6), (6, 7), (7, 4),
        (0, 4), (1, 5), (2, 6), (3, 7),
    ];

    private readonly WorldViewportContainer _worldView;
    private readonly Vector2[] _projected = new Vector2[Corners.Length];

    public ShapeWireframes(WorldViewportContainer worldView)
    {
        _worldView = worldView;
        Visible = false;
    }

    public override void _Ready()
    {
        // Stay active when the game is paused.
        ProcessMode = ProcessModeEnum.Always;
    }

    public override void _UnhandledKeyInput(InputEvent @event)
    {
        if (@event is InputEventKey { Pressed: true, Echo: false, PhysicalKeycode: Key.F4 })
        {
            Visible = !Visible;
        }
    }

    public override void _Process(double delta)
    {
        if (Visible)
        {
            QueueRedraw();
        }
    }

    public override void _Draw()
    {
        Camera3D? camera = _worldView.World.GetCamera3D();
        if (camera is null)
        {
            return;
        }

        // From the game viewport's pixels to the window's, where the container draws it scaled up.
        Transform2D toWindow = _worldView.GetGlobalTransform();
        foreach (Node node in _worldView.World.FindChildren("*", nameof(CollisionShape3D), owned: false))
        {
            if (node is not CollisionShape3D { Disabled: false, Shape: BoxShape3D box } shape)
            {
                continue;
            }

            Transform3D transform = shape.GlobalTransform;
            for (int i = 0; i < Corners.Length; i++)
            {
                Vector3 corner = transform * (Corners[i] * box.Size / 2);
                _projected[i] = toWindow * camera.UnprojectPosition(corner);
            }
            foreach (var (a, b) in Edges)
            {
                DrawLine(_projected[a], _projected[b], LineColor);
            }
        }
    }
}
