using System;
using System.Collections.Generic;

using BodyCheck.Core;

using Godot;

namespace BodyCheck;

/// <summary>
/// One room, simulated: its tiles, and the entities in it with their controllers. Each physics tick it
/// steps every entity, before the engine's own step. It has no camera, lights or input of its own, so
/// that tests can use it as it is.
/// </summary>
public partial class RoomNode : Node3D
{
    private static readonly Color PlayerColor = new(0.2f, 0.9f, 1.0f);
    private static readonly Color RobotColor = new(1.0f, 0.45f, 0.15f);
    private static readonly Color PlatformColor = new(0.9f, 0.1f, 0.7f);
    private static readonly Color BlockColor = new(0.6f, 0.65f, 0.3f);

    private readonly List<Entity> _entities = [];
    private readonly List<Entity> _characters = [];
    private readonly List<Entity> _blocks = [];

    /// <param name="mergeTiles">Whether to merge tiles into larger colliders, or give each tile its own.</param>
    public RoomNode(Room room, bool mergeTiles = true, PlayerTuning? playerTuning = null, BodyTuning? playerBody = null)
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

        PlayerTuning = playerTuning ?? new PlayerTuning();
        BodyTuning body = playerBody ?? BodyTuning.Player;
        Player = Add(new WalkerEntity(body, BobTheRobotVisual()), room.PlayerStart);
        Player.AddChild(new OmniLight3D { OmniRange = 15, ShadowEnabled = true, Position = new Vector3(0, 0, 2) });
        Player.Controller = new PlayerController(PlayerTuning, () => PlayerInput?.Invoke() ?? default);

        var robotTuning = new RobotTuning();
        foreach (Vector2 start in room.RobotStarts)
        {
            WalkerEntity robot = Add(new WalkerEntity(BodyTuning.Robot, DalekVisual()), start);
            robot.Controller = new RobotController(robotTuning);
            _characters.Add(robot);
        }

        var platformTuning = new PlatformTuning();
        foreach (Vector2 start in room.PlatformStarts)
        {
            SimpleControlledEntity platform = Add(
                new SimpleControlledEntity(BodyTuning.Platform, BoxVisual(BodyTuning.Platform, PlatformColor)), start);
            platform.Controller = new PlatformController(platform.Position.Y, platformTuning);
            _characters.Add(platform);
        }

        foreach (Vector2 start in room.BlockStarts)
        {
            _blocks.Add(Add(new Entity(BodyTuning.Block, PortalCubeVisual()), start));
        }
    }

    public Room Room { get; }
    public TileGrid Tiles { get; }

    public WalkerEntity Player { get; }
    public PlayerTuning PlayerTuning { get; }

    /// <summary>The robots and the platforms, robots first.</summary>
    public IReadOnlyList<Entity> Characters => _characters;

    /// <summary>The blocks. A block has no controller: it is only pushed, carried and stood on.</summary>
    public IReadOnlyList<Entity> Blocks => _blocks;

    /// <summary>Every entity in the room, in the order they are stepped: the player, the robots, the platforms, the blocks.</summary>
    public IReadOnlyList<Entity> Entities => _entities;

    /// <summary>Supplies the player's input, once per tick. The player stands idle if this is null.</summary>
    public Func<PlayerInput>? PlayerInput { get; set; }

    /// <summary>Whether the controllers run. With this off, entities only coast, under drag and gravity.</summary>
    public bool ControllersEnabled { get; set; } = true;

    public override void _PhysicsProcess(double delta)
    {
        foreach (Entity entity in _entities)
        {
            entity.Step((float)delta, ControllersEnabled);
        }
    }

    /// <summary>Adds an entity to the room, with the middle of its base at <paramref name="start"/>.</summary>
    private T Add<T>(T entity, Vector2 start)
        where T : Entity
    {
        entity.Position = new Vector3(start.X, start.Y + entity.HalfSize.Y, 0);
        AddChild(entity);
        _entities.Add(entity);
        return entity;
    }

    /// <summary>A box the size of a body, to draw an entity as.</summary>
    private static MeshInstance3D BoxVisual(BodyTuning body, Color color, bool glows = false)
    {
        Vector2 halfSize = body.HalfSize;
        var mesh = new MeshInstance3D
        {
            Mesh = new BoxMesh
            {
                Size = new Vector3(2 * halfSize.X, 2 * halfSize.Y, Entity.Depth),
                Material = new StandardMaterial3D
                {
                    AlbedoColor = color,
                    EmissionEnabled = glows,
                    Emission = color,
                    EmissionEnergyMultiplier = 1.5f,
                },
            },
            Position = new Vector3(0, halfSize.Y, 0),
        };
        if (glows)
        {
            mesh.AddChild(new OmniLight3D { LightColor = color, LightEnergy = 2, OmniRange = 6 });
        }
        return mesh;
    }

    private static Node3D PortalCubeVisual() => InstantiateModel("res://assets/portal_cube/scene.tscn");
    private static Node3D DalekVisual() => InstantiateModel("res://assets/dalek/scene.tscn");
    private static Node3D BobTheRobotVisual() => InstantiateModel("res://assets/bob_the_robot/scene.tscn");

    private static Node3D InstantiateModel(string path)
    {
        var scene = GD.Load<PackedScene>(path);
        return scene.Instantiate<Node3D>();
    }
}
