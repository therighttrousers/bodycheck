using System;

using BodyCheck.Core;
using BodyCheck.Core.Controllers;
using BodyCheck.Core.Simulation;

using Godot;

using PhantomCamera;

namespace BodyCheck;

/// <summary>
/// The camera, which Phantom Camera moves: its host on the <see cref="Camera3D"/> puts the camera where the phantom
/// camera of highest priority is, and tweens it from one to the other when that changes. There are two. The player's
/// follows a target that this node moves each frame. The target is above the player, kept where the view shows only
/// the world, as far as the world is bigger than the view. The map's is far back, where it shows the whole world, and
/// takes over in <see cref="GameMode.Map"/>.
/// <para>
/// The rig processes while the tree is paused, so that the camera can move to the map and back. The player doesn't
/// look up or down while it's paused.
/// </para>
/// </summary>
public partial class CameraRig : Node3D
{
    private const float Fov = 50;

    /// <summary>
    /// Half the view's height, in tiles, at the plane of the tiles' front faces. The view is as many tiles high as a
    /// 320x240 screen, at every size of the viewport, so a tile's face is TilePixels high at 320x240 and the other
    /// sizes show the same framing.
    /// </summary>
    private const float HalfHeight = 240f / Constants.TilePixels / 2;

    /// <summary>How far above the player's centre the view's centre is, in tiles, when the world allows it.</summary>
    private const float Lift = 3;
    /// <summary>How far the camera target moves, in tiles, when the player looks up or down.</summary>
    private const float Look = 5;

    private readonly Node3D _target = new() { PhysicsInterpolationMode = PhysicsInterpolationModeEnum.Off };
    private PhantomCamera3D _phantomCamera = null!;
    private WorldNode? _world;

    private Node3D _mapPhantom = null!;
    private PhantomCamera3D _mapPhantomCamera = null!;

    /// <summary>The priority of the player's camera, which is above the map camera's when the map is closed.</summary>
    private const int CameraPriority = 50;
    /// <summary>The priority of the map camera, when it's enabled. Should be greater than all other priorities.</summary>
    private const int MapCameraPriority = 1000;

    public Camera3D Camera { get; } = new() { Fov = Fov, Current = true };

    public override void _Ready()
    {
        // Allow the rig to move when the game is paused so the camera can show the entire world as the map.
        ProcessMode = ProcessModeEnum.Always;

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
        _phantomCamera.Priority = CameraPriority;
        _phantomCamera.TweenDuration = 0.25f;

        _mapPhantom = NewPhantomCameraNode<Node3D>("phantom_camera/phantom_camera_3d.gd");
        _mapPhantom.Set("follow_mode", (int)FollowMode3D.None);
        AddChild(_mapPhantom);
        _mapPhantomCamera = _mapPhantom.AsPhantomCamera3D();
        _mapPhantomCamera.Priority = 0;
        _mapPhantomCamera.TweenDuration = 0.25f;
    }

    /// <summary>Follows the player of <paramref name="world"/>, from where the camera should be at once, undamped.</summary>
    public void Follow(WorldNode world)
    {
        _world = world;
        MoveMapCamera();
        MoveTarget();
        _phantomCamera.Node3D.Call("teleport_position");
    }

    /// <summary>Switches to the map camera in <see cref="GameMode.Map"/>, and back to the player's otherwise.</summary>
    public void OnGameModeChanged(GameMode gameMode)
    {
        _mapPhantomCamera.Priority = gameMode == GameMode.Map ? MapCameraPriority : 0;
    }

    // A parent is processed before its children, so the phantom camera reads where the target is this frame.
    public override void _Process(double delta)
    {
        MoveMapCamera();
        MoveTarget();
    }

    /// <summary>
    /// Puts the map camera over the middle of the world, back to where the whole world fits in the view at the tiles'
    /// front faces, in both directions. The viewport is read each frame, as for the player's camera.
    /// </summary>
    private void MoveMapCamera()
    {
        if (_world is null)
        {
            return;
        }

        Vector2 viewport = GetViewport().GetVisibleRect().Size;
        float h = 0.5f * _world.Tiles.Height;
        float w = 0.5f * _world.Tiles.Width * viewport.Y / viewport.X;
        float mapDistance = Math.Max(h, w) / Mathf.Tan(Mathf.DegToRad(Fov / 2));
        _mapPhantom.Position = new Vector3(
            0.5f * _world.Tiles.Width,
            0.5f * _world.Tiles.Height,
            mapDistance + 0.5f
        );
    }

    private void MoveTarget()
    {
        if (_world is null)
        {
            return;
        }

        PlayerInput? input = _world.PlayerInput?.Invoke();
        float lookOffset = GetTree().Paused ? 0 : Look * (input?.Look ?? 0);

        // The player's interpolated position, since the target moves every frame and the player every physics tick.
        Vector3 player = _world.Player.GetGlobalTransformInterpolated().Origin;
        Vector2 viewport = GetViewport().GetVisibleRect().Size;
        float halfWidth = HalfHeight * viewport.X / viewport.Y;
        _target.GlobalPosition = new Vector3(
            Clamp(player.X, halfWidth, _world.Tiles.Width),
            Clamp(player.Y + Lift + lookOffset, HalfHeight, _world.Tiles.Height),
            0);
    }

    /// <summary>
    /// Keeps the view's centre where a view of <paramref name="half"/> either side of it stays between 0 and
    /// <paramref name="size"/>, or in the middle when the world is smaller than the view.
    /// </summary>
    private static float Clamp(float centre, float half, float size) =>
        size <= 2 * half ? size / 2 : Mathf.Clamp(centre, half, size - half);

    /// <summary>Phantom Camera's nodes are GDScript classes, which the C# wrappers wrap but can't create.</summary>
    private static T NewPhantomCameraNode<T>(string script) where T : Node =>
        (T)GD.Load<GDScript>($"res://addons/phantom_camera/scripts/{script}").New();
}
