using System;
using System.Linq;

using BodyCheck.Core;
using BodyCheck.Look;
using BodyCheck.Probes;

using Godot;

namespace BodyCheck;

/// <summary>
/// Plays one room from the <see cref="Rooms"/> library, drawn as gray boxes: it adds the camera, the
/// lights and the player's input to a <see cref="RoomNode"/>, and the rendering pipeline's switches
/// (see <see cref="LookSwitches"/>).
/// <para>
/// User arguments, after "--" on the command line: a room name; "--tiles=single" for one collider per
/// tile instead of merged boxes; "--probe=" and a probe's name to run that probe and quit.
/// </para>
/// </summary>
public partial class Main : Node3D
{
    private int _roomIndex;
    private bool _mergeTiles = true;
    private RoomNode _room = null!;
    private Camera3D _camera = null!;
    private LookSwitches _look = null!;
    private IProbe? _probe;

    public override void _Ready()
    {
        AddInputActions();
        AddLook();

        string? roomName = null;
        string? probeName = null;
        foreach (string arg in OS.GetCmdlineUserArgs())
        {
            if (arg == "--tiles=single")
            {
                _mergeTiles = false;
            }
            else if (arg.StartsWith("--probe="))
            {
                probeName = arg["--probe=".Length..];
            }
            else
            {
                roomName = arg;
            }
        }

        if (probeName is not null)
        {
            _probe = Probe.Start(probeName, room => LoadRoom(room), _mergeTiles);
            return;
        }

        int index = Rooms.All.ToList().FindIndex(
            room => room.Name.Equals(roomName, StringComparison.OrdinalIgnoreCase));
        LoadRoom(Math.Max(index, 0));
        _room.PlayerInput = () => new PlayerInput(
            Input.GetAxis("move_left", "move_right"), Input.IsActionPressed("jump"));
    }

    public override void _PhysicsProcess(double delta)
    {
        // This runs before the room's own tick, because a parent is processed before its children.
        if (_probe is not null)
        {
            _probe.Tick((float)delta);
            if (_probe.Done)
            {
                GetTree().Quit();
            }
            return;
        }

        if (Input.IsActionJustPressed("next_room"))
        {
            LoadRoom(_roomIndex + 1);
        }
        else if (Input.IsActionJustPressed("previous_room"))
        {
            LoadRoom(_roomIndex - 1);
        }
    }

    private void LoadRoom(int index)
    {
        Func<PlayerInput>? input = _room?.PlayerInput;
        _roomIndex = Mathf.PosMod(index, Rooms.All.Count);
        LoadRoom(Rooms.All[_roomIndex]).PlayerInput = input;
    }

    /// <summary>Replaces the current room, if there is one, and points the camera at the new one.</summary>
    private RoomNode LoadRoom(Room room)
    {
        _room?.QueueFree();
        _room = new RoomNode(room, _mergeTiles);
        if (room == Rooms.Look)
        {
            _room.AddChild(new LookProps(_room.Tiles));
        }
        AddChild(_room);
        AddCamera();
        ShowTitle();
        return _room;
    }

    private void ShowTitle() => GetWindow().Title = $"Body Check: {_room.Room.Name} ({_look.Summary})";

    private void AddCamera()
    {
        _camera?.QueueFree();

        // Far enough back to fit the whole room, whichever of its width and height is the tighter fit.
        TileGrid tiles = _room.Tiles;
        const float fov = 50;
        Vector2 view = GetViewport().GetVisibleRect().Size;
        float halfHeight = Mathf.Max(tiles.Height / 2f, tiles.Width / 2f * view.Y / view.X);
        float distance = halfHeight / Mathf.Tan(Mathf.DegToRad(fov / 2));
        _camera = new Camera3D
        {
            Fov = fov,
            Position = new Vector3(tiles.Width / 2f, tiles.Height / 2f, distance + 0.5f),
            Current = true,
        };
        AddChild(_camera);
    }

    /// <summary>Adds the lights, the colour pass, and the switches that turn parts of the pipeline on and off.</summary>
    private void AddLook()
    {
        var light = new DirectionalLight3D
        {
            RotationDegrees = new Vector3(-35, -25, 0),
            LightEnergy = 0.6f,
            ShadowEnabled = true,
        };
        AddChild(light);

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
        AddChild(new WorldEnvironment { Environment = environment });

        var colorPass = new ColorPass();
        AddChild(colorPass);

        _look = new LookSwitches(light, environment, colorPass);
        AddChild(_look);

        // A change of size changes the shape of the view, so the camera has to fit the room again.
        _look.Changed += () =>
        {
            AddCamera();
            ShowTitle();
        };
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
        AddAction("jump",
            new InputEventKey { PhysicalKeycode = Key.Space },
            new InputEventJoypadButton { ButtonIndex = JoyButton.A });
        AddAction("next_room",
            new InputEventKey { PhysicalKeycode = Key.Period },
            new InputEventJoypadButton { ButtonIndex = JoyButton.RightShoulder });
        AddAction("previous_room",
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
