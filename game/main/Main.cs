using System;
using System.Collections.Generic;
using System.Runtime.ExceptionServices;
using BodyCheck.Core;
using Godot;

namespace BodyCheck;

/// <summary>
/// Draws the Core simulation of one room from the <see cref="Rooms"/> library, as gray boxes.
/// </summary>
public partial class Main : Node3D
{
    private static readonly Color PlayerColor = new(0.2f, 0.9f, 1.0f);
    private static readonly Color RobotColor = new(1.0f, 0.45f, 0.15f);

    private readonly List<(Body Body, Node3D Node)> _bodyNodes = [];

    private int _roomIndex;
    private World _world = null!;
    private PlayerController _player = null!;
    private List<ICharacterController> _characters = [];

    // Holds everything that belongs to the current room, so that changing rooms can free it all at once.
    private Node3D _roomNode = null!;

    public override void _Ready()
    {
        AddInputActions();
        AddLights();
        LoadRoom(StartingRoomIndex());
    }

    /// <summary>
    /// The room named by the first argument after "--" on the command line, or the first room.
    /// </summary>
    private static int StartingRoomIndex()
    {
        string[] args = OS.GetCmdlineUserArgs();
        for (int i = 0; args.Length > 0 && i < Rooms.All.Count; i++)
        {
            if (Rooms.All[i].Name.Equals(args[0], System.StringComparison.OrdinalIgnoreCase))
            {
                return i;
            }
        }
        return 0;
    }

    public override void _PhysicsProcess(double delta)
    {
        if (Input.IsActionJustPressed("next_room"))
        {
            LoadRoom(_roomIndex + 1);
        }
        else if (Input.IsActionJustPressed("previous_room"))
        {
            LoadRoom(_roomIndex - 1);
        }

        var input = new PlayerInput(Input.GetAxis("move_left", "move_right"), Input.IsActionPressed("jump"));
        _player.Apply(input, _world, (float)delta);
        foreach (var character in _characters)
        {
            character.Apply(_world, (float)delta);
        }
        _world.Step((float)delta);

        foreach ((Body body, Node3D node) in _bodyNodes)
        {
            node.Position = ToGodot(body.Position);
        }
    }

    private static Vector3 ToGodot(System.Numerics.Vector2 v) => new(v.X, v.Y, 0);

    private void LoadRoom(int index)
    {
        GD.Print($"LoadRoom {index}");

        _roomIndex = Mathf.PosMod(index, Rooms.All.Count);
        Room room = Rooms.All[_roomIndex];
        GetWindow().Title = $"Body Check: {room.Name}";

        _roomNode?.QueueFree();
        _roomNode = new Node3D();
        AddChild(_roomNode);
        _bodyNodes.Clear();

        _world = new World(room.CreateTiles());

        var playerTuning = new PlayerTuning();
        var playerFeetToCenter = new System.Numerics.Vector2(0, playerTuning.HalfSize.Y);
        _player = new PlayerController(room.PlayerStart + playerFeetToCenter, playerTuning);
        AddBody(_player.Body, PlayerColor, glows: true);

        _characters = [];

        var robotTuning = new RobotTuning();
        var robotFeetToCenter = new System.Numerics.Vector2(0, robotTuning.HalfSize.Y);
        foreach (System.Numerics.Vector2 start in room.RobotStarts)
        {
            var robot = new RobotController(start + robotFeetToCenter, robotTuning);
            _characters.Add(robot);
            AddBody(robot.Body, RobotColor, glows: false);
        }

        AddTiles();
        AddCamera();
    }

    private void AddBody(Body body, Color color, bool glows)
    {
        _world.Bodies.Add(body);

        System.Numerics.Vector2 size = 2 * body.HalfSize;
        var node = new MeshInstance3D
        {
            Mesh = new BoxMesh
            {
                Size = new Vector3(size.X, size.Y, size.X),
                Material = new StandardMaterial3D
                {
                    AlbedoColor = color,
                    EmissionEnabled = glows,
                    Emission = color,
                    EmissionEnergyMultiplier = 1.5f,
                },
            },
            Position = ToGodot(body.Position),
        };
        if (glows)
        {
            node.AddChild(new OmniLight3D { LightColor = color, LightEnergy = 2, OmniRange = 6 });
        }
        _roomNode.AddChild(node);
        _bodyNodes.Add((body, node));
    }

    private void AddTiles()
    {
        var mesh = new BoxMesh
        {
            Size = Vector3.One,
            Material = new StandardMaterial3D { AlbedoColor = new Color(0.35f, 0.38f, 0.45f) },
        };
        TileGrid tiles = _world.Tiles;
        for (int x = 0; x < tiles.Width; x++)
        {
            for (int y = 0; y < tiles.Height; y++)
            {
                if (tiles.IsSolid(x, y))
                {
                    _roomNode.AddChild(new MeshInstance3D { Mesh = mesh, Position = new Vector3(x + 0.5f, y + 0.5f, 0) });
                }
            }
        }

        // A back wall, so that lights have something to fall on.
        _roomNode.AddChild(new MeshInstance3D
        {
            Mesh = new BoxMesh
            {
                Size = new Vector3(tiles.Width, tiles.Height, 0.2f),
                Material = new StandardMaterial3D { AlbedoColor = new Color(0.12f, 0.13f, 0.16f) },
            },
            Position = new Vector3(tiles.Width / 2f, tiles.Height / 2f, -0.6f),
        });
    }

    private void AddCamera()
    {
        // Far enough back to fit the whole room, whichever of its width and height is the tighter fit.
        TileGrid tiles = _world.Tiles;
        const float fov = 50;
        Vector2 view = GetViewport().GetVisibleRect().Size;
        float halfHeight = Mathf.Max(tiles.Height / 2f, tiles.Width / 2f * view.Y / view.X);
        float distance = halfHeight / Mathf.Tan(Mathf.DegToRad(fov / 2));
        _roomNode.AddChild(new Camera3D
        {
            Fov = fov,
            Position = new Vector3(tiles.Width / 2f, tiles.Height / 2f, distance + 0.5f),
            Current = true,
        });
    }

    private void AddLights()
    {
        AddChild(new DirectionalLight3D
        {
            RotationDegrees = new Vector3(-35, -25, 0),
            LightEnergy = 0.6f,
            ShadowEnabled = true,
        });

        AddChild(new WorldEnvironment
        {
            Environment = new Godot.Environment
            {
                BackgroundMode = Godot.Environment.BGMode.Color,
                BackgroundColor = new Color(0.02f, 0.02f, 0.04f),
                AmbientLightSource = Godot.Environment.AmbientSource.Color,
                AmbientLightColor = new Color(0.5f, 0.55f, 0.7f),
                AmbientLightEnergy = 0.3f,
            },
        });
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
