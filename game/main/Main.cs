using System;
using System.Collections.Generic;
using System.Linq;

using BodyCheck.Core;
using BodyCheck.Core.Controllers;
using BodyCheck.Core.Levels;
using BodyCheck.Core.Simulation;
using BodyCheck.Core.WorldData;
using BodyCheck.Look;
using BodyCheck.Probes;

using Godot;

namespace BodyCheck;

/// <summary>
/// Plays one world, drawn as gray boxes: the levels of the LDtk project at <see cref="LevelsPath"/>, as a world of one
/// room per level, or a room from the <see cref="RoomLibrary"/> library, as a world of one room. It adds the camera, the lights and the player's input to a <see cref="WorldNode"/>,
/// and the rendering pipeline's switches (see <see cref="LookSwitches"/>). The game is drawn in a <see cref="WorldViewportContainer"/>, and the overlay over
/// it at the window's full resolution.
/// <para>
/// The worlds are the LDtk world first, then the library's. The first world plays unless one is named. Comma and period
/// switch to the previous and next world.
/// </para>
/// <para>
/// User arguments, after "--" on the command line: a world's name; "--tiles=single" for one collider per
/// tile instead of merged boxes; "--probe=" and a probe's name to run that probe and quit; "--overlay" to
/// start with the <see cref="DebugOverlay"/> showing; "--shapes" to start with the <see cref="ShapeWireframes"/> showing.
/// F3 switches the overlay on and off, and F4 the shapes.
/// </para>
/// </summary>
public partial class Main : Node
{
    private const string LevelsPath = "res://levels/main.ldtk";

    private IReadOnlyList<World> _worlds = null!;
    private int _worldIndex;
    private bool _mergeTiles = true;
    private WorldNode _world = null!;
    private readonly CameraRig _cameraRig = new();
    private LookSwitches _look = null!;
    private readonly WorldViewportContainer _worldView = new();
    private readonly DebugOverlay _overlay = new();
    private ShapeWireframes _shapes = null!;
    private IProbe? _probe;

    public override void _Ready()
    {
        AddInputActions();
        AddChild(_worldView);
        AddLook();
        _worldView.World.AddChild(_cameraRig);
        _shapes = new ShapeWireframes(_worldView);
        AddChild(_shapes);
        AddChild(_overlay);
        _worlds = [LdtkReader.ReadWorld(LevelsPath, ReadFile), .. RoomLibrary.All.Select(room => new World(room))];

        string? worldName = null;
        string? probeName = null;
        foreach (string arg in OS.GetCmdlineUserArgs())
        {
            if (arg == "--tiles=single")
            {
                _mergeTiles = false;
            }
            else if (arg == "--overlay")
            {
                _overlay.Visible = true;
            }
            else if (arg == "--shapes")
            {
                _shapes.Visible = true;
            }
            else if (arg.StartsWith("--probe="))
            {
                probeName = arg["--probe=".Length..];
            }
            else
            {
                worldName = arg;
            }
        }

        if (probeName is not null)
        {
            _probe = Probe.Start(probeName, world => LoadWorld(world), _mergeTiles);
            return;
        }

        int index = _worlds.ToList().FindIndex(
            world => world.Name.Equals(worldName, StringComparison.OrdinalIgnoreCase));
        LoadWorld(Math.Max(index, 0));
        _world.PlayerInput = () => new PlayerInput(
            Input.GetAxis("move_left", "move_right"),
            Input.GetAxis("move_down", "move_up"),
            Input.IsActionPressed("jump"));
    }

    public override void _PhysicsProcess(double delta)
    {
        // This runs before the world's own tick, because a parent is processed before its children.
        if (_probe is not null)
        {
            _probe.Tick((float)delta);
            if (_probe.Done)
            {
                GetTree().Quit();
            }
            return;
        }

        if (Input.IsActionJustPressed("next_world"))
        {
            LoadWorld(_worldIndex + 1);
        }
        else if (Input.IsActionJustPressed("previous_world"))
        {
            LoadWorld(_worldIndex - 1);
        }
    }

    public override void _Process(double delta) => ReportMouseTile();

    /// <summary>Reports the tile under the mouse, where the camera's ray through it meets the plane of play.</summary>
    private void ReportMouseTile()
    {
        Vector2 mouse = _worldView.World.GetMousePosition();
        Vector3 origin = _cameraRig.Camera.ProjectRayOrigin(mouse);
        Vector3 direction = _cameraRig.Camera.ProjectRayNormal(mouse);
        Vector3 point = origin + direction * (-origin.Z / direction.Z);
        Telemetry.Report(Metric.MouseTile, new Vector2I(Mathf.FloorToInt(point.X), Mathf.FloorToInt(point.Y)));
    }

    private void LoadWorld(int index)
    {
        Func<PlayerInput>? input = _world?.PlayerInput;
        _worldIndex = Mathf.PosMod(index, _worlds.Count);
        LoadWorld(_worlds[_worldIndex]).PlayerInput = input;
    }

    /// <summary>Replaces the current world, if there is one, and has the camera follow the new one's player.</summary>
    private WorldNode LoadWorld(World world)
    {
        _world?.QueueFree();
        Telemetry.Reset();
        _world = new WorldNode(world, EntityVisuals.For, _mergeTiles);
        if (world.Rooms.Contains(RoomLibrary.Look))
        {
            _world.AddChild(new LookProps(_world.Tiles));
        }
        _worldView.World.AddChild(_world);
        _cameraRig.Follow(_world);
        ShowTitle();
        return _world;
    }

    private static string ReadFile(string path)
    {
        using FileAccess file = FileAccess.Open(path, FileAccess.ModeFlags.Read)
            ?? throw new System.IO.IOException($"Can't open {path}: {FileAccess.GetOpenError()}.");
        return file.GetAsText();
    }

    private void ShowTitle() => GetWindow().Title = $"Body Check: {_world.World.Name} ({_look.Summary})";

    /// <summary>Adds the lights, the colour pass, and the switches that turn parts of the pipeline on and off.</summary>
    private void AddLook()
    {
        var light = new DirectionalLight3D
        {
            RotationDegrees = new Vector3(-35, -25, 0),
            LightEnergy = 0.6f,
            ShadowEnabled = true,
        };
        _worldView.World.AddChild(light);

        var environment = new Godot.Environment
        {
            BackgroundMode = Godot.Environment.BGMode.Color,
            BackgroundColor = new Color(0.02f, 0.02f, 0.04f),
            AmbientLightSource = Godot.Environment.AmbientSource.Color,
            AmbientLightColor = new Color(0.5f, 0.55f, 0.7f),
            AmbientLightEnergy = 0.3f,
            SsaoEnabled = true,
            SsrEnabled = true,
            GlowEnabled = true,
        };
        _worldView.World.AddChild(new WorldEnvironment { Environment = environment });

        var colorPass = new ColorPass();
        _worldView.World.AddChild(colorPass);

        _look = new LookSwitches(_worldView, light, environment, colorPass);
        AddChild(_look);

        _look.Changed += ShowTitle;
    }

    private static void AddInputActions()
    {
        AddAction("move_left",
            new InputEventKey { PhysicalKeycode = Key.A },
            new InputEventKey { PhysicalKeycode = Key.Left },
            new InputEventJoypadMotion { Axis = JoyAxis.LeftX, AxisValue = -1 },
            new InputEventJoypadButton { ButtonIndex = JoyButton.DpadLeft });
        AddAction("move_right",
            new InputEventKey { PhysicalKeycode = Key.D },
            new InputEventKey { PhysicalKeycode = Key.Right },
            new InputEventJoypadMotion { Axis = JoyAxis.LeftX, AxisValue = 1 },
            new InputEventJoypadButton { ButtonIndex = JoyButton.DpadRight });
        AddAction("move_up",
            new InputEventKey { PhysicalKeycode = Key.W },
            new InputEventKey { PhysicalKeycode = Key.Up },
            new InputEventJoypadMotion { Axis = JoyAxis.LeftY, AxisValue = -1 },
            new InputEventJoypadButton { ButtonIndex = JoyButton.DpadUp });
        AddAction("move_down",
            new InputEventKey { PhysicalKeycode = Key.S },
            new InputEventKey { PhysicalKeycode = Key.Down },
            new InputEventJoypadMotion { Axis = JoyAxis.LeftY, AxisValue = 1 },
            new InputEventJoypadButton { ButtonIndex = JoyButton.DpadDown });
        AddAction("jump",
            new InputEventKey { PhysicalKeycode = Key.Space },
            new InputEventJoypadButton { ButtonIndex = JoyButton.A });
        AddAction("next_world",
            new InputEventKey { PhysicalKeycode = Key.Period },
            new InputEventJoypadButton { ButtonIndex = JoyButton.RightShoulder });
        AddAction("previous_world",
            new InputEventKey { PhysicalKeycode = Key.Comma },
            new InputEventJoypadButton { ButtonIndex = JoyButton.LeftShoulder });
    }

    private static void AddAction(string name, params InputEvent[] events)
    {
        InputMap.AddAction(name, deadzone: 0.2f);
        foreach (InputEvent e in events)
        {
            InputMap.ActionAddEvent(name, e);
        }
    }
}
