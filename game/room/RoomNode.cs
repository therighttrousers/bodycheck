using System;
using System.Collections.Generic;

using BodyCheck.Core;

using Godot;

namespace BodyCheck;

/// <summary>
/// One room, simulated: its tiles, the player, the robots and the platforms, with their controllers.
/// Each physics tick it runs the controllers, steps the <see cref="World"/>, and hands the result to the
/// engine. It has no camera, lights or input of its own, so that tests can use it as it is.
/// </summary>
public partial class RoomNode : Node3D
{
    private static readonly Color PlayerColor = new(0.2f, 0.9f, 1.0f);
    private static readonly Color RobotColor = new(1.0f, 0.45f, 0.15f);
    private static readonly Color PlatformColor = new(0.9f, 0.1f, 0.7f);
    private static readonly Color BlockColor = new(0.6f, 0.65f, 0.3f);

    private readonly List<BodyNode> _bodyNodes = [];
    private readonly List<ICharacterController> _characters = [];
    private readonly List<BodyNode> _blocks = [];

    /// <param name="mergeTiles">Whether to merge tiles into larger colliders, or give each tile its own.</param>
    public RoomNode(Room room, bool mergeTiles = true, PlayerController? playerController = null)
    {
        Room = room;
        Tiles = room.CreateTiles();
        AddChild(TileColliders.Build(Tiles, mergeTiles));

        // A back wall, so that lights have something to fall on.
        AddChild(new MeshInstance3D
        {
            Mesh = new BoxMesh
            {
                Size = new Vector3(Tiles.Width, Tiles.Height, 0.2f),
                Material = new StandardMaterial3D { AlbedoColor = new Color(0.12f, 0.13f, 0.16f) },
            },
            Position = new Vector3(Tiles.Width / 2f, Tiles.Height / 2f, -0.6f),
        });

        PlayerController = playerController ?? new PlayerController(new PlayerTuning());
        Player = AddBody(PlayerController.Body, room.PlayerStart, PlayerColor, glows: true);

        var robotTuning = new RobotTuning();
        foreach (Vector2 start in room.RobotStarts)
        {
            var robot = new RobotController(robotTuning);
            _characters.Add(robot);
            AddBody(robot.Body, start, RobotColor, glows: false);
        }

        var platformTuning = new PlatformTuning();
        foreach (Vector2 start in room.PlatformStarts)
        {
            var platform = new PlatformController(start.Y + platformTuning.HalfSize.Y, platformTuning);
            _characters.Add(platform);
            AddBody(platform.Body, start, PlatformColor, glows: false);
        }

        var blockTuning = new BlockTuning();
        foreach (Vector2 start in room.BlockStarts)
        {
            _blocks.Add(AddBody(blockTuning.CreateBody(), start, BlockColor, glows: false));
        }
    }

    public Room Room { get; }
    public TileGrid Tiles { get; }
    public World World { get; } = new() { TractionInEngine = Spike.MotorTraction, DragInEngine = Spike.EngineDamping };

    public BodyNode Player { get; }
    public PlayerController PlayerController { get; }

    /// <summary>The robots' and platforms' controllers, robots first.</summary>
    public IReadOnlyList<ICharacterController> Characters => _characters;

    /// <summary>The blocks' nodes. A block has no controller: it is only pushed, carried and stood on.</summary>
    public IReadOnlyList<BodyNode> Blocks => _blocks;

    /// <summary>The node of every body in the room, in the order of <see cref="World.Bodies"/>.</summary>
    public IReadOnlyList<BodyNode> BodyNodes => _bodyNodes;

    /// <summary>Supplies the player's input, once per tick. The player stands idle if this is null.</summary>
    public Func<PlayerInput>? PlayerInput { get; set; }

    /// <summary>Whether the controllers run. With this off, bodies only coast, under drag and gravity.</summary>
    public bool ControllersEnabled { get; set; } = true;

    public override void _Ready()
    {
        // The rules' gravity, on the default space. This lasts only as long as the run.
        PhysicsServer3D.AreaSetParam(GetWorld3D().Space, PhysicsServer3D.AreaParameter.Gravity, World.Gravity);
    }

    public override void _PhysicsProcess(double delta)
    {
        float dt = (float)delta;
        foreach (BodyNode node in _bodyNodes)
        {
            node.PullMotion();
        }

        if (ControllersEnabled)
        {
            PlayerController.Apply(PlayerInput?.Invoke() ?? default, World, dt);
            foreach (ICharacterController character in _characters)
            {
                character.Apply(World, dt);
            }
        }

        World.Step(dt);
        foreach (BodyNode node in _bodyNodes)
        {
            node.PushImpulse();
            node.UpdateMotor();
        }
    }

    /// <summary>Adds a node for a body, with the middle of the body's base at <paramref name="start"/>.</summary>
    private BodyNode AddBody(Body body, Vector2 start, Color color, bool glows)
    {
        var node = new BodyNode(body, color, glows)
        {
            Position = new Vector3(start.X, start.Y + body.HalfSize.Y, 0),
        };
        AddChild(node);
        node.PullMotion();
        _bodyNodes.Add(node);
        World.Bodies.Add(body);
        return node;
    }
}
