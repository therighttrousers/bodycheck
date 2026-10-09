using BodyCheck.Core;
using Godot;

namespace BodyCheck;

/// <summary>
/// Prototype 1: a graybox room built in code, drawing the Core simulation.
/// </summary>
public partial class Main : Node3D
{
    // '#' is a solid tile and 'P' is where the player starts.
    private static readonly string[] Room =
    [
        "################################",
        "#..............................#",
        "#..............................#",
        "#..............................#",
        "#.......................####...#",
        "#..............................#",
        "#..............................#",
        "#...............####...........#",
        "#..............................#",
        "#.............##...............#",
        "#..............................#",
        "#.......###........#...........#",
        "#..................#.....###...#",
        "#..............#####...........#",
        "#..P...........#...............#",
        "#.........#....#.......##......#",
        "#.........#............##......#",
        "################################",
    ];

    private static readonly System.Numerics.Vector2 PlayerHalfSize = new(0.4f, 0.8f);

    private World _world = null!;
    private Body _playerBody = null!;
    private PlayerController _player = null!;
    private Node3D _playerNode = null!;

    public override void _Ready()
    {
        Engine.MaxFps = 60;
        Engine.PhysicsTicksPerSecond = 60;
        Engine.PhysicsJitterFix = 0;

        AddInputActions();

        _world = new World(TileGrid.FromText(Room));
        _playerBody = new Body(FindPlayerStart(), PlayerHalfSize, mass: 1) { Drag = 0.5f };
        _world.Bodies.Add(_playerBody);
        _player = new PlayerController(_playerBody, new PlayerTuning());

        AddTiles();
        AddPlayer();
        AddCameraAndLights();
    }

    public override void _PhysicsProcess(double delta)
    {
        var frame = Engine.GetPhysicsFrames();
        var input = new PlayerInput(Input.GetAxis("move_left", "move_right"), Input.IsActionPressed("jump"));
        _player.Apply(input, _world, (float)delta, frame);
        _world.Step((float)delta);
        _playerNode.Position = ToGodot(_playerBody.Position);
    }

    private static Vector3 ToGodot(System.Numerics.Vector2 v) => new(v.X, v.Y, 0);

    private static System.Numerics.Vector2 FindPlayerStart()
    {
        for (int row = 0; row < Room.Length; row++)
        {
            int x = Room[row].IndexOf('P');
            if (x >= 0)
            {
                // Standing on the floor of its tile.
                return new System.Numerics.Vector2(x + 0.5f, Room.Length - 1 - row + PlayerHalfSize.Y);
            }
        }
        throw new System.InvalidOperationException("The room has no player start.");
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
    }

    private static void AddAction(string name, params InputEvent[] events)
    {
        InputMap.AddAction(name, deadzone: 0.2f);
        foreach (InputEvent e in events)
        {
            InputMap.ActionAddEvent(name, e);
        }
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
                    AddChild(new MeshInstance3D { Mesh = mesh, Position = new Vector3(x + 0.5f, y + 0.5f, 0) });
                }
            }
        }

        // A back wall, so that lights have something to fall on.
        AddChild(new MeshInstance3D
        {
            Mesh = new BoxMesh
            {
                Size = new Vector3(tiles.Width, tiles.Height, 0.2f),
                Material = new StandardMaterial3D { AlbedoColor = new Color(0.12f, 0.13f, 0.16f) },
            },
            Position = new Vector3(tiles.Width / 2f, tiles.Height / 2f, -0.6f),
        });
    }

    private void AddPlayer()
    {
        var glow = new Color(0.2f, 0.9f, 1.0f);
        _playerNode = new MeshInstance3D
        {
            Mesh = new BoxMesh
            {
                Size = new Vector3(2 * PlayerHalfSize.X, 2 * PlayerHalfSize.Y, 0.8f),
                Material = new StandardMaterial3D
                {
                    AlbedoColor = glow,
                    EmissionEnabled = true,
                    Emission = glow,
                    EmissionEnergyMultiplier = 1.5f,
                },
            },
            Position = ToGodot(_playerBody.Position),
        };
        _playerNode.AddChild(new OmniLight3D { LightColor = glow, LightEnergy = 2, OmniRange = 6 });
        AddChild(_playerNode);
    }

    private void AddCameraAndLights()
    {
        TileGrid tiles = _world.Tiles;
        const float fov = 50;
        float distance = tiles.Height / 2f / Mathf.Tan(Mathf.DegToRad(fov / 2));
        AddChild(new Camera3D
        {
            Fov = fov,
            Position = new Vector3(tiles.Width / 2f, tiles.Height / 2f, distance + 0.5f),
        });

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
}
