using BodyCheck.Core;

using Godot;

using PhantomCamera;

namespace BodyCheck;

/// <summary>
/// The camera, which Phantom Camera moves: its host on the <see cref="Camera3D"/> puts the camera where the one
/// phantom camera is, and the phantom camera follows a target that this node moves each frame. The target is above
/// the player, kept where the view shows only the room, as far as the room is bigger than the view.
/// </summary>
public partial class CameraRig : Node3D
{
    private const float Fov = 50;

    /// <summary>
    /// Half the view's height, in tiles, at the plane of the tiles' front faces. The view is as many tiles high as a
    /// 320x240 screen, at every size of the viewport, so a tile's face is TilePixels high at 320x240 and the other
    /// sizes show the same framing.
    /// </summary>
    private const float HalfHeight = 240f / World.TilePixels / 2;

    /// <summary>How far above the player's centre the view's centre is, in tiles, when the room allows it.</summary>
    private const float Lift = 3;
    /// <summary>How far the camera target moves, in tiles, when the player looks up or down.</summary>
    private const float Look = 5;

    private readonly Node3D _target = new() { PhysicsInterpolationMode = PhysicsInterpolationModeEnum.Off };
    private PhantomCamera3D _phantomCamera = null!;
    private RoomNode? _room;

    public Camera3D Camera { get; } = new() { Fov = Fov, Current = true };

    public override void _Ready()
    {
        AddChild(_target);
        AddChild(Camera);
        Camera.AddChild(NewPhantomCameraNode<Node>("phantom_camera_host/phantom_camera_host.gd"));

        Node3D phantom = NewPhantomCameraNode<Node3D>("phantom_camera/phantom_camera_3d.gd");
        phantom.Set("follow_mode", (int)FollowMode3D.Simple);
        phantom.Set("follow_damping", true);
        phantom.Set("follow_damping_value", new Vector3(0.2f, 0.2f, 0));
        AddChild(phantom);
        _phantomCamera = phantom.AsPhantomCamera3D();
        // From the plane of play back to where a tile's front face, half a tile in front of it, is the right size.
        float distance = HalfHeight / Mathf.Tan(Mathf.DegToRad(Fov / 2));
        _phantomCamera.FollowOffset = new Vector3(0, 0, distance + 0.5f);
        _phantomCamera.FollowTarget = _target;
    }

    /// <summary>Follows the player of <paramref name="room"/>, from where the camera should be at once, undamped.</summary>
    public void Follow(RoomNode room)
    {
        _room = room;
        MoveTarget();
        _phantomCamera.Node3D.Call("teleport_position");
    }

    // A parent is processed before its children, so the phantom camera reads where the target is this frame.
    public override void _Process(double delta) => MoveTarget();

    private void MoveTarget()
    {
        if (_room is null)
        {
            return;
        }

        PlayerInput? input = _room.PlayerInput?.Invoke();
        float lookOffset = Look * (input?.Look ?? 0);

        // The player's interpolated position, since the target moves every frame and the player every physics tick.
        Vector3 player = _room.Player.GetGlobalTransformInterpolated().Origin;
        Vector2 viewport = GetViewport().GetVisibleRect().Size;
        float halfWidth = HalfHeight * viewport.X / viewport.Y;
        _target.GlobalPosition = new Vector3(
            Clamp(player.X, halfWidth, _room.Tiles.Width),
            Clamp(player.Y + Lift + lookOffset, HalfHeight, _room.Tiles.Height),
            0);
    }

    /// <summary>
    /// Keeps the view's centre where a view of <paramref name="half"/> either side of it stays between 0 and
    /// <paramref name="size"/>, or in the middle when the room is smaller than the view.
    /// </summary>
    private static float Clamp(float centre, float half, float size) =>
        size <= 2 * half ? size / 2 : Mathf.Clamp(centre, half, size - half);

    /// <summary>Phantom Camera's nodes are GDScript classes, which the C# wrappers wrap but can't create.</summary>
    private static T NewPhantomCameraNode<T>(string script) where T : Node =>
        (T)GD.Load<GDScript>($"res://addons/phantom_camera/scripts/{script}").New();
}
