using BodyCheck.Core.Controllers;
using BodyCheck.Core.Entities;
using BodyCheck.Core.Rooms;

using Godot;

namespace BodyCheck.Core.Simulation;

/// <summary>
/// One room, simulated: its tiles, and the entities in it with their controllers. Each physics tick it
/// steps every entity, before the engine's own step. It has no camera, lights or input of its own, so
/// that tests can use it as it is.
/// <para>
/// It is given what to draw for each kind of entity, so that it doesn't know the game's assets.
/// </para>
/// </summary>
public partial class RoomNode : Node3D
{
    private readonly List<Entity> _entities = [];
    private readonly List<Entity> _characters = [];
    private readonly List<Entity> _blocks = [];

    /// <param name="looks">
    /// Makes what to draw for an entity of a kind, with the given body, with its origin at the middle of its base.
    /// It is called once for each entity.
    /// </param>
    /// <param name="mergeTiles">Whether to merge tiles into larger colliders, or give each tile its own.</param>
    public RoomNode(
        Room room,
        Func<EntityKind, BodyTuning, Node3D> looks,
        bool mergeTiles = true,
        PlayerTuning? playerTuning = null,
        BodyTuning? playerBody = null)
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
        Player = Add(new WalkerEntity(body, looks(EntityKind.Player, body)), room.PlayerStart);
        Player.AddChild(new OmniLight3D { OmniRange = 15, ShadowEnabled = true, Position = new Vector3(0, 0, 2) });
        Player.Controller = new PlayerController(PlayerTuning, () => PlayerInput?.Invoke() ?? default);

        var robotTuning = new RobotTuning();
        foreach (Vector2 start in room.RobotStarts)
        {
            WalkerEntity robot = Add(new WalkerEntity(BodyTuning.Robot, looks(EntityKind.Robot, BodyTuning.Robot)), start);
            robot.Controller = new RobotController(robotTuning);
            _characters.Add(robot);
        }

        var platformTuning = new PlatformTuning();
        foreach (Vector2 start in room.PlatformStarts)
        {
            SimpleControlledEntity platform = Add(
                new SimpleControlledEntity(BodyTuning.Platform, looks(EntityKind.Platform, BodyTuning.Platform)), start);
            platform.Controller = new PlatformController(platform.Position.Y, platformTuning);
            _characters.Add(platform);
        }

        foreach (Vector2 start in room.BlockStarts)
        {
            _blocks.Add(Add(new Entity(BodyTuning.Block, looks(EntityKind.Block, BodyTuning.Block), axisLockAngularZ: false), start));
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
}
