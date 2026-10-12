using System.Collections.Generic;
using System.Linq;

using BodyCheck.Core;

using Godot;

namespace BodyCheck.Spike;

/// <summary>
/// Spike for decision record 001: one room from the <see cref="Rooms"/> library, simulated by Godot's 3D
/// physics engine instead of Core's <see cref="World"/>.
/// <para>
/// User arguments, after "--" on the command line: a room name; "--tiles=single" for one collider per
/// tile instead of merged boxes; "--probe=" with "seams", "player", "corner", "corner-hold", "drop", "ride-robot" or "ride-platform" to run a probe and quit.
/// </para>
/// </summary>
public partial class JoltSpike : Node3D
{
    private const float Gravity = 50;

    /// <summary>The player's grip on the ground when braking, as a multiple of its weight.</summary>
    private const float Friction = 4f;

    private static readonly Color PlayerColor = new(0.2f, 0.9f, 1.0f);
    private static readonly Color RobotColor = new(1.0f, 0.45f, 0.15f);
    private static readonly Color PlatformColor = new(0.9f, 0.1f, 0.7f);

    /// <summary>A long flat floor with a high ceiling, for sliding a body over many tile seams.</summary>
    private static readonly Room SeamsRoom = new("Seams", string.Join('\n',
    [
        new string('#', 64),
        .. Enumerable.Repeat("#" + new string('.', 62) + "#", 7),
        "#.P" + new string('.', 60) + "#",
        new string('#', 64),
    ]));

    /// <summary>A two-tile wall on a floor, for jumping beside.</summary>
    private static readonly Room CornerRoom = new("Corner", """
        ............
        ............
        ............
        ............
        ............
        ............
        .........#..
        ..P......#..
        ############
        """);

    /// <summary>A long floor with the player above a robot, which has room to patrol one way for a long time.</summary>
    private static readonly Room RobotRidingRoom = new("Robot riding", string.Join('\n',
    [
        .. Enumerable.Repeat(new string('.', 400), 8),
        new string('.', 20) + "P" + new string('.', 379),
        new string('.', 400),
        new string('.', 20) + "R" + new string('.', 379),
        new string('#', 400),
    ]));

    /// <summary>A hovering platform between two walls, with the player about 3 tiles above it.</summary>
    private static readonly Room PlatformRidingRoom = new("Platform riding", string.Join('\n',
    [
        .. Enumerable.Repeat("#" + new string('.', 38) + "#", 3),
        "#" + new string('.', 19) + "P" + new string('.', 18) + "#",
        .. Enumerable.Repeat("#" + new string('.', 38) + "#", 3),
        "#" + new string('.', 19) + "H" + new string('.', 18) + "#",
        .. Enumerable.Repeat("#" + new string('.', 38) + "#", 6),
        new string('#', 40),
    ]));

    private Room _room = null!;
    private TileGrid _tiles = null!;
    private bool _mergeTiles = true;
    private readonly JoltWorld _world = new();
    private JoltPlayerController _playerController = null!;
    private string? _probe;

    private SeamProbe? _seamProbe;
    private PlayerProbe? _playerProbe;
    private CornerProbe? _cornerProbe;
    private DropProbe? _dropProbe;
    private RidingProbe? _ridingProbe;

    public JoltBody Player { get; private set; } = null!;

    private readonly List<IJoltCharacterController> _controllers = [];

    public override void _Ready()
    {
        ReadArguments();
        if (_probe is "seams" or "player")
        {
            _room = SeamsRoom;
        }
        else if (_probe is "corner" or "corner-hold" or "drop")
        {
            _room = CornerRoom;
        }
        else if (_probe == "ride-robot")
        {
            _room = RobotRidingRoom;
        }
        else if (_probe == "ride-platform")
        {
            _room = PlatformRidingRoom;
        }
        _tiles = _room.CreateTiles();
        GetWindow().Title = $"Body Check, Jolt spike: {_room.Name}";

        // Core's gravity, on the default space. This lasts only as long as the run.
        PhysicsServer3D.AreaSetParam(GetWorld3D().Space, PhysicsServer3D.AreaParameter.Gravity, Gravity);

        AddInputActions();
        AddLights();
        AddCamera();
        AddTiles();

        var tuning = new JoltPlayerTuning();
        Player = new JoltBody(tuning.HalfSize, tuning.Mass, tuning.Drag, PlayerColor, glows: true)
        {
            Position = new Vector3(_room.PlayerStart.X, _room.PlayerStart.Y + tuning.HalfSize.Y, 0),
        };
        AddChild(Player);
        _world.Bodies.Add(Player);
        _playerController = new JoltPlayerController(Player, tuning, Gravity, Friction);

        var robotTuning = new JoltRobotTuning();
        foreach (System.Numerics.Vector2 start in _room.RobotStarts)
        {
            var body = new JoltBody(robotTuning.HalfSize, robotTuning.Mass, robotTuning.Drag, RobotColor, glows: false)
            {
                Position = new Vector3(start.X, start.Y + robotTuning.HalfSize.Y, 0),
            };
            AddChild(body);
            _world.Bodies.Add(body);
            _controllers.Add(new JoltRobotController(body, robotTuning, Gravity));
        }

        var platformTuning = new JoltPlatformTuning();
        foreach (System.Numerics.Vector2 start in _room.PlatformStarts)
        {
            var body = new JoltBody(platformTuning.HalfSize, platformTuning.Mass, platformTuning.Drag, PlatformColor, glows: false)
            {
                Position = new Vector3(start.X, start.Y + platformTuning.HalfSize.Y, 0)
            };
            AddChild(body);
            _world.Bodies.Add(body);
            _controllers.Add(new JoltPlatformController(body, platformTuning, Gravity));
        }

        if (_probe == "player")
        {
            _playerProbe = new PlayerProbe(Player);
            GD.Print("Player probe:");
        }

        if (_probe is "corner" or "corner-hold")
        {
            _cornerProbe = new CornerProbe(Player, holdRight: _probe == "corner-hold");
            GD.Print($"Corner probe, {(_probe == "corner" ? "not pushing" : "pushing")} during the jump:");
        }

        if (_probe is "ride-robot" or "ride-platform")
        {
            _ridingProbe = new RidingProbe(Player, _controllers[0].Body);
            GD.Print($"Riding probe, {_room.Name}:");
        }

        if (_probe == "drop")
        {
            GD.Print("Drop probe: Jolt collision margin fraction "
                + $"{ProjectSettings.GetSetting("physics/jolt_physics_3d/collisions/collision_margin_fraction")}, "
                + $"penetration slop {ProjectSettings.GetSetting("physics/jolt_physics_3d/simulation/penetration_slop")}, "
                + $"speculative contact distance {ProjectSettings.GetSetting("physics/jolt_physics_3d/simulation/speculative_contact_distance")}");
            _dropProbe = new DropProbe(Player);
        }

        if (_probe == "seams")
        {
            _seamProbe = new SeamProbe(Player, speed: tuning.TopSpeed);
            GD.Print($"Seam probe: tiles {(_mergeTiles ? "merged" : "single")}, "
                + $"engine {ProjectSettings.GetSetting("physics/3d/physics_engine")}, internal edge removal "
                + ProjectSettings.GetSetting("physics/jolt_physics_3d/simulation/use_enhanced_internal_edge_removal"));
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        if (_seamProbe is not null)
        {
            // The seam probe slides the body with no controller, so that nothing brakes it.
            if (_seamProbe.Tick((float)delta))
            {
                GetTree().Quit();
            }
            _world.Step((float)delta);
            return;
        }

        _dropProbe?.Tick();
        if (_playerProbe is { Done: true } || _cornerProbe is { Done: true } || _dropProbe is { Done: true }
            || _ridingProbe is { Done: true })
        {
            GetTree().Quit();
        }

        JoltPlayerInput input = _playerProbe?.Tick() ?? _cornerProbe?.Tick() ?? _ridingProbe?.Tick()
            ?? new JoltPlayerInput(Input.GetAxis("move_left", "move_right"), Input.IsActionPressed("jump"));
        _playerController.Apply(input, (float)delta);

        foreach (var controller in _controllers)
        {
            controller.Apply(_world, (float)delta);
        }

        _world.Step((float)delta);
    }

    /// <summary>Adds a body whose base is centered on <paramref name="start"/>, as deep as it is wide.</summary>
    private JoltBody AddBody(
        System.Numerics.Vector2 start, System.Numerics.Vector2 halfSize, float mass, float drag, Color color)
    {
        var body = new JoltBody(new Vector3(halfSize.X, halfSize.Y, halfSize.X), mass, drag, color, glows: false)
        {
            Position = new Vector3(start.X, start.Y + halfSize.Y, 0),
        };
        AddChild(body);
        _world.Bodies.Add(body);
        return body;
    }

    private void ReadArguments()
    {
        _room = Rooms.All[0];
        foreach (string arg in OS.GetCmdlineUserArgs())
        {
            if (arg == "--tiles=single")
            {
                _mergeTiles = false;
            }
            else if (arg.StartsWith("--probe="))
            {
                _probe = arg["--probe=".Length..];
            }
            else
            {
                _room = Rooms.All.FirstOrDefault(
                    room => room.Name.Equals(arg, System.StringComparison.OrdinalIgnoreCase), _room);
            }
        }
    }

    /// <summary>
    /// Adds every solid tile to one static body, because Jolt removes ghost collisions at the seams between
    /// shapes of one body but not between bodies. Each collider is drawn, so the layout is visible.
    /// </summary>
    private void AddTiles()
    {
        var body = new StaticBody3D { PhysicsMaterialOverride = new PhysicsMaterial { Friction = 0, Bounce = 0 } };
        var material = new StandardMaterial3D { AlbedoColor = new Color(0.35f, 0.38f, 0.45f) };
        foreach (Rect2I box in TileBoxes(_tiles, _mergeTiles))
        {
            var size = new Vector3(box.Size.X, box.Size.Y, 1);
            var position = new Vector3(box.Position.X + box.Size.X / 2f, box.Position.Y + box.Size.Y / 2f, 0);
            body.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = size }, Position = position });
            body.AddChild(new MeshInstance3D
            {
                Mesh = new BoxMesh { Size = size, Material = material },
                Position = position,
            });
        }
        AddChild(body);

        // A back wall, so that lights have something to fall on.
        AddChild(new MeshInstance3D
        {
            Mesh = new BoxMesh
            {
                Size = new Vector3(_tiles.Width, _tiles.Height, 0.2f),
                Material = new StandardMaterial3D { AlbedoColor = new Color(0.12f, 0.13f, 0.16f) },
            },
            Position = new Vector3(_tiles.Width / 2f, _tiles.Height / 2f, -0.6f),
        });
    }

    /// <summary>
    /// The solid tiles as boxes, in tile coordinates. Merging joins each horizontal run of tiles into one
    /// box, then joins runs that sit directly above one another and span the same columns.
    /// </summary>
    private static List<Rect2I> TileBoxes(TileGrid tiles, bool merge)
    {
        var boxes = new List<Rect2I>();

        // The runs of the row below, by their first and last column, with the index of the box each belongs to.
        var below = new Dictionary<(int, int), int>();
        for (int y = 0; y < tiles.Height; y++)
        {
            var current = new Dictionary<(int, int), int>();
            for (int x = 0; x < tiles.Width; x++)
            {
                if (!tiles.IsSolid(x, y))
                {
                    continue;
                }

                int start = x;
                while (merge && x + 1 < tiles.Width && tiles.IsSolid(x + 1, y))
                {
                    x++;
                }

                if (merge && below.TryGetValue((start, x), out int index))
                {
                    boxes[index] = new Rect2I(boxes[index].Position, boxes[index].Size + new Vector2I(0, 1));
                }
                else
                {
                    index = boxes.Count;
                    boxes.Add(new Rect2I(start, y, x - start + 1, 1));
                }
                current[(start, x)] = index;
            }
            below = current;
        }
        return boxes;
    }

    private void AddCamera()
    {
        // Far enough back to fit the whole room, whichever of its width and height is the tighter fit.
        const float fov = 50;
        Vector2 view = GetViewport().GetVisibleRect().Size;
        float halfHeight = Mathf.Max(_tiles.Height / 2f, _tiles.Width / 2f * view.Y / view.X);
        float distance = halfHeight / Mathf.Tan(Mathf.DegToRad(fov / 2));
        AddChild(new Camera3D
        {
            Fov = fov,
            Position = new Vector3(_tiles.Width / 2f, _tiles.Height / 2f, distance + 0.5f),
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
