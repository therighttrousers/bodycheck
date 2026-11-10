using System;
using System.Collections.Generic;
using System.Linq;

using BodyCheck.Core;
using BodyCheck.Core.Maths;
using BodyCheck.Core.Simulation;

using Godot;

using PhantomCamera;

namespace BodyCheck;

/// <summary>
/// The camera, which Phantom Camera moves: its host on the <see cref="Camera3D"/> puts the camera where the phantom
/// camera of highest priority is, and tweens it from one to the other when that changes. There are two. The player's
/// follows a target that this node moves each frame, with no damping of its own: <see cref="CameraFraming"/> moves the
/// target, toward a point above the player and ahead of the way the player last pressed, where its map lets the view's
/// centre be, and smooths it. The map's is far back, where it shows the whole world, and takes over in
/// <see cref="GameMode.Map"/>.
/// <para>
/// The rig processes while the tree is paused, so that the camera can move to the map and back. The player looks up
/// or down only while playing, not while paused or in a cutscene, and not when the user is giving any other movement
/// input.
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
    private static readonly float HalfHeight = (float)Constants.Resolution.Y / Constants.TilePixels / 2;

    /// <summary>How far above the player's centre the view's centre is, in tiles, when the world allows it.</summary>
    private const float Lift = 3;
    /// <summary>How far from the player's centre the view's centre is, in tiles, when the player looks up or down and the world allows it.</summary>
    private static readonly float LookOffset = 0.75f * HalfHeight;
    /// <summary>How long the user must hold up or down before panning starts, in real seconds.</summary>
    private const float LookDelay = 0.5f;
    /// <summary>How long it takes to pan fully up or down after panning starts, in real seconds.</summary>
    private const float LookTime = 1;
    /// <summary>How long the camera takes to move to the map and back, in real seconds.</summary>
    private const float MapMoveTime = 0.25f;

    private GameModeManager _gameModeManager = null!;

    /// <summary>The priority of the player's camera, which is above the map camera's when the map is closed.</summary>
    private const int CameraPriority = 50;
    /// <summary>The priority of the map camera, when it's enabled. Should be greater than all other priorities.</summary>
    private const int MapCameraPriority = 1000;

    private readonly Node3D _target = new() { PhysicsInterpolationMode = PhysicsInterpolationModeEnum.Off };
    private PhantomCamera3D _phantomCamera = null!;
    private PhantomCamera3D _mapPhantomCamera = null!;
    private List<PhantomCamera3D> _currentCameras = [];
    private WorldNode? _world;
    private CameraFraming? _framing;

    /// <summary>The framing of the world the rig follows; null until it follows one.</summary>
    public CameraFraming? Framing => _framing;

    /// <summary>How long the player has been looking, in real seconds; 0 while the player isn't.</summary>
    private float _lookHeldTime;

    /// <summary>How far ahead of the player the view wants to be, in tiles, to the right if positive.</summary>
    private float _lead;
    /// <summary>How fast <see cref="_lead"/> changes, in tiles per game second, as its spring moves it.</summary>
    private float _leadVelocity;
    /// <summary>How long, in game seconds, the lead holds before it starts back to 0, now that the player has let go.</summary>
    private float _leadReturnTimeLeft;

    public Camera3D Camera { get; } = new() { Fov = Fov, Current = true };

    public CameraRig(GameModeManager gameModeManager)
    {
        _gameModeManager = gameModeManager;
        _gameModeManager.GameModeChanged += OnGameModeChanged;
    }

    public override void _Ready()
    {
        // Allow the rig to move when the game is paused so the camera can show the entire world as the map.
        ProcessMode = ProcessModeEnum.Always;

        AddChild(_target);
        AddChild(Camera);
        Camera.AddChild(NewPhantomCameraNode<Node>("phantom_camera_host/phantom_camera_host.gd"));

        Node3D phantom = NewPhantomCameraNode<Node3D>("phantom_camera/phantom_camera_3d.gd");
        phantom.Set("follow_mode", (int)FollowMode3D.Simple);
        AddChild(phantom);
        _phantomCamera = phantom.AsPhantomCamera3D();
        // From the plane of play back to where a tile's front face, half a tile in front of it, is the right size.
        float distance = HalfHeight / Mathf.Tan(Mathf.DegToRad(Fov / 2));
        _phantomCamera.FollowOffset = new Vector3(0, 0, distance + 0.5f);
        _phantomCamera.FollowTarget = _target;
        _phantomCamera.Priority = CameraPriority;
        _phantomCamera.Node3D.Name = "PlayerCamera";
        _phantomCamera.TweenStarted += () => OnTweenStarted(_phantomCamera);
        _phantomCamera.TweenCompleted += () => OnTweenCompleted(_phantomCamera);

        Node3D mapPhantom = NewPhantomCameraNode<Node3D>("phantom_camera/phantom_camera_3d.gd");
        mapPhantom.Set("follow_mode", (int)FollowMode3D.None);
        AddChild(mapPhantom);
        _mapPhantomCamera = mapPhantom.AsPhantomCamera3D();
        _mapPhantomCamera.Priority = 0;
        _mapPhantomCamera.Node3D.Name = "MapCamera";
        _mapPhantomCamera.TweenStarted += () => OnTweenStarted(_mapPhantomCamera);
        _mapPhantomCamera.TweenCompleted += () => OnTweenCompleted(_mapPhantomCamera);
    }

    /// <summary>Follows the player of <paramref name="world"/>, from where the camera should be at once, undamped.</summary>
    public void Follow(WorldNode world)
    {
        _world = world;
        _framing = new CameraFraming(world.World);
        _lookHeldTime = 0;
        _lead = 0;
        _leadVelocity = 0;
        _leadReturnTimeLeft = 0;
        MoveMapCamera();
        MoveTarget(0);
        _phantomCamera.Node3D.Call("teleport_position");
    }

    /// <summary>Switches to the map camera in <see cref="GameMode.Map"/>, and back to the player's otherwise.</summary>
    private void OnGameModeChanged(GameModeState modeState)
    {
        // The host tweens to a camera over that camera's duration, in game time. In the map, and on the way back from
        // it, game time runs at the gameplay speed, whatever mode the map was opened from, so this keeps the move quick
        // however slow the game is.
        float duration = MapMoveTime * Features.Current.GameplaySpeed;
        _phantomCamera.TweenDuration = duration;
        _mapPhantomCamera.TweenDuration = duration;
        _mapPhantomCamera.Priority = modeState.Mode == GameMode.Map ? MapCameraPriority : 0;
    }

    private void OnTweenStarted(PhantomCamera3D camera)
    {
        _currentCameras.Add(camera);
        ReportCurrentCameras();
    }

    private void OnTweenCompleted(PhantomCamera3D camera)
    {
        _currentCameras = [camera];
        ReportCurrentCameras();
        if (!ReferenceEquals(camera, _mapPhantomCamera))
        {
            _gameModeManager.NotLookingAtMap();
        }
    }

    private void ReportCurrentCameras()
    {
        var names = _currentCameras.Select(camera => camera.Node3D.Name.ToString()).ToArray();
        Telemetry.Report(Metric.CurrentCamera, string.Join(" -> ", names));
    }

    // A parent is processed before its children, so the phantom camera reads where the target is this frame.
    public override void _Process(double delta)
    {
        MoveMapCamera();
        // On game time, as the camera's damping is.
        MoveTarget((float)delta);
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
        float h = 0.5f * _world.World.Size.Y;
        float w = 0.5f * _world.World.Size.X * viewport.Y / viewport.X;
        float mapDistance = Math.Max(h, w) / Mathf.Tan(Mathf.DegToRad(Fov / 2));
        _mapPhantomCamera.Node3D.Position = new Vector3(
            0.5f * _world.World.Size.X,
            0.5f * _world.World.Size.Y,
            mapDistance + 0.5f);
    }

    private void MoveTarget(float delta)
    {
        if (_world is null || _framing is null)
        {
            return;
        }

        StepLook(_world, delta);
        StepLead(_world, delta);
        // Not looking, the look amount is 0, and the framing's spring eases the view back.
        float lookOffset = Mathf.Lerp(Lift, LookOffset * _world.PlayerInput.YAxis, LookAmount);

        // The player's interpolated position, since the target moves every frame and the player every physics tick.
        Vector3 player = _world.Player.GetGlobalTransformInterpolated().Origin;
        Vector2 viewport = GetViewport().GetVisibleRect().Size;
        var halfSize = new Vector2(HalfHeight * viewport.X / viewport.Y, HalfHeight);
        var wanted = new Vector2(player.X + _lead, player.Y + lookOffset);
        CameraFraming.Result framed = _framing.Frame(new Vector2(player.X, player.Y), wanted, halfSize, delta);
        Telemetry.Report(Metric.CameraWanted, wanted);
        Telemetry.Report(Metric.CameraGoal, framed.Goal);
        Telemetry.Report(Metric.CameraTarget, framed.Centre);
        _target.GlobalPosition = new Vector3(framed.Centre.X, framed.Centre.Y, 0);
    }

    /// <summary>
    /// Times the look for <paramref name="delta"/> game seconds: while the game is playing and
    /// <see cref="PlayerInput.CanLook"/>, the player is looking, and otherwise not, so a look starts again from the
    /// beginning. The look is the player's hands, not the world, so it's timed in real seconds.
    /// </summary>
    private void StepLook(WorldNode world, float delta)
    {
        if (world.GameMode == GameMode.Playing && world.PlayerInput.CanLook)
        {
            // While playing, the time scale is the gameplay speed, which is never 0.
            _lookHeldTime += delta / (float)Engine.TimeScale;
        }
        else
        {
            _lookHeldTime = 0;
        }
    }

    /// <summary>
    /// How far the player has looked, from 0 (not at all) to 1 (fully): nothing for <see cref="LookDelay"/>, then
    /// eased in, quadratically, over <see cref="LookTime"/>.
    /// </summary>
    private float LookAmount
    {
        get
        {
            float t = Math.Clamp((_lookHeldTime - LookDelay) / LookTime, 0, 1);
            return t * t;
        }
    }

    /// <summary>
    /// Moves the lead for <paramref name="delta"/> game seconds, by the horizontal input the player's controller acts
    /// on: the user's while playing, and a script's in a cutscene. In every other mode, it stays as it is, since the
    /// input then either doesn't move the player or aims a phase.
    /// <para>
    /// The lead follows a critically damped spring. While there is input, its goal is <see cref="Features.CameraLead"/>
    /// the way it presses, however far, so that pressing against a wall leads too; its smoothing time is
    /// <see cref="Features.CameraLeadTime"/>, so that a tap moves it only a little. When the input stops, its goal is
    /// wherever it is, so that it settles and holds, for <see cref="Features.CameraLeadReturnDelay"/>, and then 0, with
    /// the smoothing time <see cref="Features.CameraLeadReturnTime"/>. Because the goal is set by input and not by the
    /// player's velocity, stopping doesn't pull the view back.
    /// </para>
    /// </summary>
    private void StepLead(WorldNode world, float delta)
    {
        if (world.GameMode is not (GameMode.Playing or GameMode.Cutscene))
        {
            return;
        }
        float intent = world.PlayerController.Input.XAxis;

        Features features = Features.Current;
        float goal;
        float time;
        if (Math.Abs(intent) > 1e-4f)
        {
            goal = Math.Sign(intent) * features.CameraLead;
            time = features.CameraLeadTime;
            _leadReturnTimeLeft = features.CameraLeadReturnDelay;
        }
        else if (_leadReturnTimeLeft > 0)
        {
            goal = _lead;
            time = features.CameraLeadTime;
            _leadReturnTimeLeft = Math.Max(0, _leadReturnTimeLeft - delta);
        }
        else
        {
            goal = 0;
            time = features.CameraLeadReturnTime;
        }
        (_lead, _leadVelocity) = Spring.CriticallyDamped(_lead, _leadVelocity, goal, time, delta);
    }

    /// <summary>Phantom Camera's nodes are GDScript classes, which the C# wrappers wrap but can't create.</summary>
    private static T NewPhantomCameraNode<T>(string script) where T : Node =>
        (T)GD.Load<GDScript>($"res://addons/phantom_camera/scripts/{script}").New();
}
