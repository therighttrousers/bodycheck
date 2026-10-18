using System.Diagnostics;

using BodyCheck.Core.Controllers;
using BodyCheck.Core.Entities;
using BodyCheck.Core.WorldData;

using Godot;

namespace BodyCheck.Core.Simulation;

/// <summary>
/// One world, simulated: its tiles, and the entities in it with their controllers. Each physics tick it
/// steps every entity that can process, before the engine's own step, so that an entity's controller stops with
/// whatever pauses or disables its body. It has no camera, lights or input of its own, so that tests can use it as it
/// is.
/// <para>
/// It is given what to draw for each kind of entity, so that it doesn't know the game's assets.
/// </para>
/// </summary>
public partial class WorldNode : Node3D
{
    private readonly List<Entity> _entities = [];

    /// <param name="visual">
    /// Makes what to draw for an entity of a kind, with the given body, with its origin at the middle of its base.
    /// It is called once for each entity.
    /// </param>
    /// <param name="mergeTiles">Whether to merge tiles into larger colliders, or give each tile its own.</param>
    public WorldNode(
        World world,
        Func<EntityKind, BodyTuning, Node3D> visual,
        bool mergeTiles = true,
        PlayerTuning? playerTuning = null,
        BodyTuning? playerBody = null)
    {
        World = world;
        Tiles = world.CreateTiles();
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
        PlayerInput Input() => PlayerInput?.Invoke() ?? default;

        var robotTuning = new RobotTuning();
        var platformTuning = new PlatformTuning();

        foreach (EntityConfig config in world.EntityConfigs)
        {
            Entity entity = config.Kind switch
            {
                EntityKind.Player => Player = CreatePlayer((EntityConfig.Player)config, visual(config.Kind, BodyTuning.Player), playerBody ?? BodyTuning.Player, PlayerTuning, Input),
                EntityKind.Robot => CreateRobot((EntityConfig.Robot)config, visual(config.Kind, BodyTuning.Robot), robotTuning),
                EntityKind.Platform => CreatePlatform((EntityConfig.Platform)config, visual(config.Kind, BodyTuning.Platform), platformTuning),
                EntityKind.Block => CreateBlock((EntityConfig.Block)config, visual(config.Kind, BodyTuning.Block)),
            };

            entity.Position = new Vector3(config.Start.X, config.Start.Y + entity.HalfSize.Y, 0);
            AddChild(entity);
            _entities.Add(entity);
        }

        // Redundant but harmless: the `World` constructor also ensures this.
        Debug.Assert(Player is not null);
    }

    private static WalkerEntity CreatePlayer(EntityConfig.Player config, Node3D visual, BodyTuning body, PlayerTuning tuning, Func<PlayerInput> input)
    {
        var player = new WalkerEntity(body, visual);
        player.AddChild(new OmniLight3D { OmniRange = 15, ShadowEnabled = true, Position = new Vector3(0, 0, 2) });
        player.Controller = new PlayerController(tuning, input);
        return player;
    }

    private static WalkerEntity CreateRobot(EntityConfig.Robot config, Node3D visual, RobotTuning tuning)
    {
        return new WalkerEntity(BodyTuning.Robot, visual) { Controller = new RobotController(tuning) };
    }

    private static SimpleControlledEntity CreatePlatform(EntityConfig.Platform config, Node3D visual, PlatformTuning tuning)
    {
        var platform = new SimpleControlledEntity(BodyTuning.Platform, visual);
        platform.Controller = new PlatformController(config.Start.Y + platform.HalfSize.Y, tuning);
        return platform;
    }

    private static Entity CreateBlock(EntityConfig.Block config, Node3D visual)
    {
        return new Entity(BodyTuning.Block, visual, axisLockAngularZ: false);
    }

    public World World { get; }
    public TileGrid Tiles { get; }

    public WalkerEntity Player { get; } = null!;
    public PlayerTuning PlayerTuning { get; }

    /// <summary>
    /// Every entity in the world, in the order they are stepped, which is the order of the world's
    /// <see cref="World.EntityConfigs"/>: <c>Entities[i]</c> was built from <c>World.EntityConfigs[i]</c>.
    /// </summary>
    public IReadOnlyList<Entity> Entities => _entities;

    /// <summary>The entities of one kind, in the order they are stepped.</summary>
    public IEnumerable<Entity> EntitiesOf(EntityKind kind) =>
        _entities.Where((entity, i) => World.EntityConfigs[i].Kind == kind);

    /// <summary>Supplies the player's input, once per tick. The player stands idle if this is null.</summary>
    public Func<PlayerInput>? PlayerInput { get; set; }

    /// <summary>Whether the controllers run. With this off, entities only coast, under drag and gravity.</summary>
    public bool ControllersEnabled { get; set; } = true;

    public override void _PhysicsProcess(double delta)
    {
        foreach (Entity entity in _entities)
        {
            if (entity.CanProcess())
            {
                entity.Step((float)delta, ControllersEnabled);
            }
        }
    }
}
