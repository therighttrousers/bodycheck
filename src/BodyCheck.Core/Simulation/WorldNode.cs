using System.Diagnostics;

using BodyCheck.Core.Controllers;
using BodyCheck.Core.Entities;
using BodyCheck.Core.WorldData;

using Godot;

namespace BodyCheck.Core.Simulation;

/// <summary>
/// One world, simulated: its tiles, drawn room by room, and the entities in it with their controllers. Each physics tick it
/// steps every entity that can process, before the engine's own step, so that an entity's controller stops with
/// whatever pauses or disables its body. It has no camera, lights or input of its own, so that tests can use it as it
/// is.
/// <para>
/// It is given what to draw for each kind of entity and for tiles, so that it doesn't know the game's assets. Its
/// colliders are one static body for the whole world, not one for each room, since the engine removes ghost collisions
/// at the seams between shapes of one body, but not between bodies.
/// </para>
/// </summary>
public partial class WorldNode : Node3D
{
    private class EndOfTick : IEndOfTick
    {
        public void Defer(Action action) => _actions.Add(action);

        private readonly List<Action> _actions = [];

        /// <summary>
        /// Runs the deferred actions, in the order they were deferred, and forgets them. For whatever steps the
        /// controllers, once they all have. An action can't defer another.
        /// </summary>
        public void Run()
        {
            foreach (Action action in _actions)
            {
                action();
            }
            _actions.Clear();
        }
    }

    /// <summary>
    /// The world's physics priority. What must step before the world in a tick or after it, such as whatever pauses it,
    /// sets its own priority from this one.
    /// </summary>
    public const int PhysicsPriority = 0;

    private readonly List<Entity> _entities = [];

    /// <param name="assets">What to draw for each entity and for tiles.</param>
    public WorldNode(
        World world,
        IAssetLibrary assets,
        BodyTuning? playerBody = null)
    {
        World = world;
        ProcessPhysicsPriority = PhysicsPriority;

        var body = new StaticBody3D
        {
            PhysicsMaterialOverride = new PhysicsMaterial { Friction = Features.Current.TileDefaultFriction, Bounce = 0 },
            CollisionLayer = (uint)PhysicsLayer.Static,
        };
        foreach (Rect2I box in TileColliders(World))
        {
            var size = new Vector3(box.Size.X, box.Size.Y, 1);
            var position = new Vector3(box.Position.X + box.Size.X / 2f, box.Position.Y + box.Size.Y / 2f, 0);
            body.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = size }, Position = position });
        }
        AddChild(body);

        foreach (var room in world.Rooms)
        {
            AddChild(new RoomNode(room, assets));
        }

        PlayerInput Input() => PlayerInput?.Invoke() ?? default;

        foreach (EntityConfig config in world.EntityConfigs)
        {
            Entity entity = config.Kind switch
            {
                EntityKind.Player => Player = CreatePlayer(
                    (EntityConfig.Player)config,
                    assets.GetEntityVisual(config.Kind, BodyTuning.Player.HalfSize),
                    playerBody ?? BodyTuning.Player,
                    PlayerController = new PlayerController(Input)),

                EntityKind.Robot => CreateRobot(
                    (EntityConfig.Robot)config,
                    assets.GetEntityVisual(config.Kind, BodyTuning.Robot.HalfSize)),

                EntityKind.Platform => CreatePlatform(
                    (EntityConfig.Platform)config,
                    assets.GetEntityVisual(config.Kind, BodyTuning.Platform.HalfSize)),

                EntityKind.Block => CreateBlock(
                    (EntityConfig.Block)config,
                    assets.GetEntityVisual(config.Kind, BodyTuning.Block.HalfSize)),
            };

            entity.Position = new Vector3(config.Start.X, config.Start.Y + entity.HalfSize.Y, 0);
            AddChild(entity);
            _entities.Add(entity);
        }

        // Redundant but harmless: the `World` constructor also ensures this.
        Debug.Assert(Player is not null);
    }

    private static WalkerEntity CreatePlayer(EntityConfig.Player config, Node3D visual, BodyTuning body, PlayerController controller)
    {
        var player = new WalkerEntity(body, visual);
        player.AddChild(new OmniLight3D { OmniRange = 15, ShadowEnabled = true, Position = new Vector3(0, 0, 2) });
        player.Controller = controller;
        return player;
    }

    private static WalkerEntity CreateRobot(EntityConfig.Robot config, Node3D visual)
    {
        return new WalkerEntity(BodyTuning.Robot, visual) { Controller = new RobotController() };
    }

    private static SimpleControlledEntity CreatePlatform(EntityConfig.Platform config, Node3D visual)
    {
        var platform = new SimpleControlledEntity(BodyTuning.Platform, visual);
        platform.Controller = new PlatformController(config.Start.Y + platform.HalfSize.Y);
        return platform;
    }

    private static Entity CreateBlock(EntityConfig.Block config, Node3D visual)
    {
        return new Entity(BodyTuning.Block, visual, axisLockAngularZ: false);
    }

    /// <summary>
    /// The solid tiles as boxes, in tile coordinates. Merging joins each horizontal run of tiles into one
    /// box, then joins runs that sit directly above one another and span the same columns.
    /// </summary>
    private static List<Rect2I> TileColliders(World world)
    {
        var boxes = new List<Rect2I>();

        // The runs of the row below, by their first and last column, with the index of the box each belongs to.
        var below = new Dictionary<(int, int), int>();
        for (int y = 0; y < world.Size.Y; y++)
        {
            var current = new Dictionary<(int, int), int>();
            for (int x = 0; x < world.Size.X; x++)
            {
                if (!world.IsSolid(x, y))
                {
                    continue;
                }

                int start = x;
                while (x + 1 < world.Size.X && world.IsSolid(x + 1, y))
                {
                    x++;
                }

                if (below.TryGetValue((start, x), out int index))
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

    public World World { get; }

    public WalkerEntity Player { get; } = null!;
    public PlayerController PlayerController { get; } = null!;

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

    /// <summary>
    /// Whether any entity's controller asks for the game to be paused. Whatever pauses the game for it calls
    /// <see cref="StepPaused"/> each physics tick until nothing asks.
    /// </summary>
    public bool PauseRequested => _entities.Any(entity => entity.RequestsPause);

    /// <summary>
    /// Steps every entity's controller while the game is paused because a controller asked: call once per physics
    /// tick then, in place of the world's own step. Nothing moves, and no game time passes.
    /// </summary>
    public void StepPaused()
    {
        if (!ControllersEnabled)
        {
            return;
        }
        var endOfTick = new EndOfTick();
        foreach (Entity entity in _entities)
        {
            entity.StepPaused(RealDt, endOfTick);
        }
        endOfTick.Run();
    }

    public override void _PhysicsProcess(double delta)
    {
        var endOfTick = new EndOfTick();
        foreach (Entity entity in _entities)
        {
            if (entity.CanProcess())
            {
                entity.Step((float)delta, RealDt, endOfTick, ControllersEnabled);
            }
        }
        endOfTick.Run();
    }

    /// <summary>
    /// A physics tick's length in real time. The engine's time scale shortens the delta it gives each tick, but not
    /// how often ticks come.
    /// </summary>
    private static float RealDt => 1f / Engine.PhysicsTicksPerSecond;
}
