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
/// Plays one world, drawn as gray boxes: a world of the LDtk project at <see cref="LevelsPath"/>, with one room per
/// level, or a room from the <see cref="RoomLibrary"/> library, as a world of one room. It adds the camera, the lights and the player's input to a <see cref="WorldNode"/>,
/// and the rendering pipeline's switches (see <see cref="LookSwitches"/>). The game is drawn in a <see cref="WorldViewportContainer"/>, and the overlay over
/// it at the window's full resolution.
/// <para>
/// The worlds are the LDtk project's first, in its order, then the library's. The first world plays
/// unless one is named. Comma and period switch to the previous and next world.
/// </para>
/// <para>
/// The game plays with a configuration of <see cref="Features"/>: the defaults, or one of the files in
/// <see cref="ConfigurationsDirectory"/>, named after the file. The defaults are used unless one is named. F6 and F7
/// switch to the previous and next configuration, without restarting the world. F5 reads the LDtk project and the
/// configurations again, and restarts the world. An error in a file is shown, and play goes on without it (decision record 017).
/// </para>
/// <para>
/// Escape and Tab, or the gamepad's Start and Back, open and close the menu and the map (see
/// <see cref="GameModeManager"/>), which pause the game. The camera, and the debug tools, work while it's paused.
/// </para>
/// <para>
/// User arguments, after "--" on the command line: a world's name; "--configuration=" and a configuration's
/// name; "--probe=" and a probe's name to run that probe and quit; "--overlay" to start with the
/// <see cref="DebugOverlay"/> showing; "--shapes" to start with the <see cref="ShapeWireframes"/> showing. F3 switches the overlay on and off, and F4 the shapes.
/// </para>
/// </summary>
public partial class Main : Node
{
    private const string LevelsPath = "res://levels/main.ldtk";
    private const string ConfigurationsDirectory = "res://configurations";
    private const string ConfigurationExtension = ".jsonc";
    private const string DefaultConfiguration = "defaults";

    private readonly GameModeManager _gameModeManager = new();
    private readonly AssetLibrary _assets = new();
    private IReadOnlyList<World> _worlds = null!;
    private int _worldIndex;
    private IReadOnlyList<string> _configurations = [DefaultConfiguration];
    private int _configurationIndex;
    private string? _levelsError;
    private string? _configurationError;
    private WorldNode _world = null!;
    private readonly CameraRig _cameraRig = new();
    private LookSwitches _look = null!;
    private readonly WorldViewportContainer _worldView = new();
    private readonly DebugOverlay _overlay = new();
    private readonly ErrorOverlay _errorOverlay = new();
    private ShapeWireframes _shapes = null!;
    private PhaseAimMarker _phaseAimMarker = null!;
    private IProbe? _probe;

    public override void _Ready()
    {
        AddInputActions();
        AddChild(_worldView);
        AddLook();
        _worldView.World.AddChild(_cameraRig);
        _shapes = new ShapeWireframes(_worldView);
        AddChild(_shapes);
        _phaseAimMarker = new PhaseAimMarker(_worldView);
        AddChild(_phaseAimMarker);
        AddChild(_overlay);
        AddChild(_errorOverlay);

        _gameModeManager.GameModeChanged += _cameraRig.OnGameModeChanged;
        AddChild(_gameModeManager);
        AddChild(new Muffler(_gameModeManager));

        string? worldName = null;
        string? configurationName = null;
        string? probeName = null;
        foreach (string arg in OS.GetCmdlineUserArgs())
        {
            if (arg == "--overlay")
            {
                _overlay.Visible = true;
            }
            else if (arg == "--shapes")
            {
                _shapes.Visible = true;
            }
            else if (arg.StartsWith("--configuration="))
            {
                configurationName = arg["--configuration=".Length..];
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

        ReadFiles();
        SwitchConfiguration(Math.Max(IndexOf(_configurations, configurationName), 0));

        if (probeName is not null)
        {
            _probe = Probe.Start(probeName, world => LoadWorld(world));
            // A probe's input comes from this node's physics tick, which stops while the game is paused, so nothing
            // pauses for its player. Unpaused, the player aims a phase while the world runs.
            _gameModeManager.World = null;
            return;
        }

        LoadWorld(Math.Max(IndexOf(_worlds.Select(world => world.Name), worldName), 0));
        _world.PlayerInput = () => new PlayerInput(
            Input.GetAxis("move_left", "move_right"),
            Input.GetAxis("move_down", "move_up"),
            Input.IsActionPressed("jump"),
            Input.IsActionPressed("phase"),
            Input.IsActionPressed("boost"));
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
        else if (Input.IsActionJustPressed("next_configuration"))
        {
            SwitchConfiguration(_configurationIndex + 1);
        }
        else if (Input.IsActionJustPressed("previous_configuration"))
        {
            SwitchConfiguration(_configurationIndex - 1);
        }
        else if (Input.IsActionJustPressed("reload"))
        {
            Reload();
        }
    }

    /// <summary>
    /// Reads the LDtk project and the configurations again, puts the current configuration in effect again, and restarts the
    /// current world. Both are kept by name, or are the first if they are gone.
    /// </summary>
    private void Reload()
    {
        string worldName = _worlds[_worldIndex].Name;
        string configurationName = _configurations[_configurationIndex];
        ReadFiles();
        // Kept even if it has an error now, so that the title names it, since its features stay in effect.
        _configurationIndex = Math.Max(IndexOf(_configurations, configurationName), 0);
        SwitchConfiguration(_configurationIndex);
        LoadWorld(Math.Max(IndexOf(_worlds.Select(world => world.Name), worldName), 0));
    }

    /// <summary>
    /// Reads the LDtk project's worlds, and lists the configurations. If the project has an error, its worlds are left
    /// out, and the error is shown.
    /// </summary>
    private void ReadFiles()
    {
        IReadOnlyList<World> levels = [];
        _levelsError = null;
        try
        {
            levels = LdtkReader.ReadWorlds(LevelsPath, ReadFile);
        }
        catch (Exception e) when (e is System.IO.IOException or System.IO.InvalidDataException)
        {
            GD.PushError(e.Message);
            _levelsError = e.Message;
        }
        _worlds = [.. levels, .. RoomLibrary.All.Select(room => new World(room))];

        _configurations = [
            DefaultConfiguration,
            .. DirAccess.GetFilesAt(ConfigurationsDirectory)
                .Where(file => file.EndsWith(ConfigurationExtension, StringComparison.OrdinalIgnoreCase))
                .Select(file => file[..^ConfigurationExtension.Length])
                .Order(StringComparer.OrdinalIgnoreCase),
        ];
        ShowErrors();
    }

    /// <summary>
    /// Reads the configuration at this index, wrapping around, and puts it in effect. If it has an error, the features
    /// stay as they were, and the error is shown.
    /// </summary>
    private void SwitchConfiguration(int index)
    {
        index = Mathf.PosMod(index, _configurations.Count);
        string path = $"{ConfigurationsDirectory}/{_configurations[index]}{ConfigurationExtension}";
        try
        {
            Features.Set(index == 0 ? new Features() : Features.Read(path, ReadFile(path)));
            _configurationIndex = index;
            _configurationError = null;
        }
        catch (Exception e) when (e is System.IO.IOException or System.IO.InvalidDataException)
        {
            GD.PushError(e.Message);
            _configurationError = e.Message;
        }
        ShowErrors();
        if (_world is not null)
        {
            ShowTitle();
        }
    }

    private void ShowErrors() =>
        _errorOverlay.ShowErrors([.. new[] { _levelsError, _configurationError }.OfType<string>()]);

    /// <summary>The index of the name, ignoring case, or -1 if it isn't there or is null.</summary>
    private static int IndexOf(IEnumerable<string> names, string? name) =>
        names.ToList().FindIndex(other => other.Equals(name, StringComparison.OrdinalIgnoreCase));

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
        _world = new WorldNode(world, _assets);
        if (world.Rooms.Contains(RoomLibrary.Look))
        {
            _world.AddChild(new LookProps(_world.World.Size));
        }
        _worldView.World.AddChild(_world);
        _cameraRig.Follow(_world);
        _gameModeManager.World = _world;
        _phaseAimMarker.World = _world;
        ShowTitle();
        return _world;
    }

    private static string ReadFile(string path)
    {
        using FileAccess file = FileAccess.Open(path, FileAccess.ModeFlags.Read)
            ?? throw new System.IO.IOException($"Can't open {path}: {FileAccess.GetOpenError()}.");
        return file.GetAsText();
    }

    private void ShowTitle() => GetWindow().Title =
        $"Body Check: {_world.World.Name}, with {_configurations[_configurationIndex]} ({_look.Summary})";

    /// <summary>Adds the lights, the colour pass, and the switches that turn parts of the pipeline on and off.</summary>
    private void AddLook()
    {
        var light = new DirectionalLight3D
        {
            RotationDegrees = new Vector3(-35, 10, 0),
            LightColor = new Color(1, 1, 0.9f),
            LightEnergy = 0.5f,
            ShadowEnabled = true,
        };
        _worldView.World.AddChild(light);

        var environment = new Godot.Environment
        {
            BackgroundMode = Godot.Environment.BGMode.Color,
            BackgroundColor = new Color(0.02f, 0.02f, 0.04f),
            AmbientLightSource = Godot.Environment.AmbientSource.Color,
            AmbientLightColor = new Color(0.5f, 0.5f, 1),
            AmbientLightEnergy = 0.1f,
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
            new InputEventKey { PhysicalKeycode = Key.J },
            new InputEventKey { PhysicalKeycode = Key.C },
            new InputEventJoypadButton { ButtonIndex = JoyButton.A });
        AddAction("phase",
            new InputEventKey { PhysicalKeycode = Key.K },
            new InputEventKey { PhysicalKeycode = Key.X },
            new InputEventJoypadButton { ButtonIndex = JoyButton.X });
        AddAction("boost",
            new InputEventKey { PhysicalKeycode = Key.L },
            new InputEventKey { PhysicalKeycode = Key.Z },
            new InputEventJoypadMotion { Axis = JoyAxis.TriggerRight });
        AddAction("next_world",
            new InputEventKey { PhysicalKeycode = Key.Period },
            new InputEventJoypadButton { ButtonIndex = JoyButton.RightShoulder });
        AddAction("previous_world",
            new InputEventKey { PhysicalKeycode = Key.Comma },
            new InputEventJoypadButton { ButtonIndex = JoyButton.LeftShoulder });
        AddAction("reload", new InputEventKey { PhysicalKeycode = Key.F5 });
        AddAction("previous_configuration", new InputEventKey { PhysicalKeycode = Key.F6 });
        AddAction("next_configuration", new InputEventKey { PhysicalKeycode = Key.F7 });
        AddAction("menu",
            new InputEventKey { PhysicalKeycode = Key.Escape },
            new InputEventJoypadButton { ButtonIndex = JoyButton.Start });
        AddAction("map",
            new InputEventKey { PhysicalKeycode = Key.Tab },
            new InputEventJoypadButton { ButtonIndex = JoyButton.Back });
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
