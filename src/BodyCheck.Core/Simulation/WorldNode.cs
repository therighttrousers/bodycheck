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
    /// <summary>
    /// The world's physics priority. What must step before the world in a tick or after it, such as whatever pauses it,
    /// sets its own priority from this one.
    /// </summary>
    public const int PhysicsPriority = 0;

    private readonly List<Entity> _entities = [];

    // The world's colliders: one static body for every tile.
    private readonly StaticBody3D _tiles;

    // Whether the features have changed since they were last put in the engine. See UpdateFromFeatures.
    private bool _featuresChanged = true;

    private readonly Defer _defer = new();

    /// <param name="assets">What to draw for each entity and for tiles.</param>
    /// <param name="respawnId">
    /// The respawn the player starts at, which is then the current one. The player starts where the world says if this is
    /// null, or names none of the world's respawns.
    /// </param>
    public WorldNode(
        World world,
        IAssetLibrary assets,
        BodyTuning? playerBody = null,
        string? respawnId = null)
    {
        World = world;
        CurrentRespawn = world.Respawns.FirstOrDefault(respawn => respawn.Id == respawnId);
        ProcessPhysicsPriority = PhysicsPriority;

        _tiles = new StaticBody3D
        {
            PhysicsMaterialOverride = new PhysicsMaterial { Friction = Features.Current.TileDefaultFriction, Bounce = 0 },
            CollisionLayer = (uint)PhysicsLayer.Static,
        };
        foreach (Rect2I box in TileColliders(World))
        {
            var size = new Vector3(box.Size.X, box.Size.Y, 1);
            var position = new Vector3(box.Position.X + box.Size.X / 2f, box.Position.Y + box.Size.Y / 2f, 0);
            _tiles.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = size }, Position = position });
        }
        AddChild(_tiles);

        foreach (var room in world.Rooms)
        {
            AddChild(new RoomNode(room, assets));
        }

        PlayerInput Input() => PlayerInput?.Invoke() ?? default;

        // Out of the world until the player spawns it: a disabled body is out of the engine's space, and isn't stepped.
        PlayerBlock = new Entity(
            BodyTuning.PlayerBlock,
            assets.GetPlayerBlockVisual(BodyTuning.PlayerBlock.HalfSize),
            axisLockAngularZ: false)
        {
            ProcessMode = ProcessModeEnum.Disabled,
            Visible = false,
        };

        Node3D GetVisual(EntityConfig config) =>
            assets.GetEntityVisual(config) ?? assets.GetDebugBoxVisual(config.MapHalfSize, config.MapColor);

        foreach (EntityConfig config in world.EntityConfigs)
        {
            Entity? entity = config.Kind switch
            {
                EntityKind.Player => Player = CreatePlayer(
                    (EntityConfig.Player)config,
                    GetVisual(config),
                    playerBody ?? BodyTuning.Player,
                    PlayerController = new PlayerController(Input, PlayerBlock)),

                EntityKind.Robot => CreateRobot((EntityConfig.Robot)config, GetVisual(config)),
                EntityKind.Platform => CreatePlatform((EntityConfig.Platform)config, GetVisual(config)),
                EntityKind.Block => CreateBlock((EntityConfig.Block)config, GetVisual(config)),

                EntityKind.Respawn => null,
            };

            if (entity is not null)
            {
                Vector2 start = config.Kind == EntityKind.Player && CurrentRespawn is not null ? CurrentRespawn.Start : config.Start;
                entity.Position = new Vector3(start.X, start.Y + entity.HalfSize.Y, 0);
                AddChild(entity);
                _entities.Add(entity);
            }

            var marker = assets.GetDebugBoxVisual(config.MapHalfSize, config.MapColor);
            marker.Position = new Vector3(config.Start.X, config.Start.Y + config.MapHalfSize.Y, 0);
            MapMarkers.AddChild(marker);
        }
        AddChild(MapMarkers);

        // Last, since no configuration builds it.
        AddChild(PlayerBlock);
        _entities.Add(PlayerBlock);

        // Redundant but harmless: the `World` constructor also ensures this.
        Debug.Assert(Player is not null);
    }

    private static PlayerEntity CreatePlayer(EntityConfig.Player config, Node3D visual, BodyTuning body, PlayerController controller)
    {
        var player = new PlayerEntity(body, visual);
        player.AddChild(new OmniLight3D { OmniRange = 15, ShadowEnabled = true, Position = new Vector3(0, 0, 2) });
        player.Controller = controller;
        return player;
    }

    private RobotEntity CreateRobot(EntityConfig.Robot config, Node3D visual)
    {
        // The player may be built after the robot, so the controller asks for it when it aims.
        return new RobotEntity(BodyTuning.Robot, visual) { Controller = new RobotController(() => Player) };
    }

    private static SimpleControlledEntity CreatePlatform(EntityConfig.Platform config, Node3D visual)
    {
        var platform = new SimpleControlledEntity(BodyTuning.Platform, visual);
        // The range is where the middle of the platform's box goes. Its body starts at the bottom of its box, which is
        // taller, and keeps that place in the box wherever it goes.
        var offset = new Vector2(0, platform.HalfSize.Y - config.MapHalfSize.Y);
        platform.Controller = new PlatformController(config.RangeA + offset, config.RangeB + offset);
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

    /// <summary>
    /// A box where each entity starts, of the size and colour of its box in LDtk, for comparing the world with its map
    /// and for seeing entities that have no body, such as respawns. Hidden until shown.
    /// </summary>
    public Node3D MapMarkers { get; } = new() { Name = "MapMarkers", Visible = false };

    public PlayerEntity Player { get; } = null!;

    /// <summary>The block the player spawns, the last of <see cref="Entities"/>.</summary>
    public Entity PlayerBlock { get; }
    public PlayerController PlayerController { get; } = null!;

    /// <summary>
    /// Every entity in the world, in the order they are stepped: those the world's <see cref="World.EntityConfigs"/>
    /// build, in their order, so that <c>Entities[i]</c> was built from <c>World.EntityConfigs[i]</c>, then
    /// <see cref="PlayerBlock"/>, which no configuration builds.
    /// </summary>
    public IReadOnlyList<Entity> Entities => _entities;

    /// <summary>The entities of one kind that the world's configurations build, in the order they are stepped.</summary>
    public IEnumerable<Entity> EntitiesOf(EntityKind kind) =>
        _entities.Where((entity, i) => i < World.EntityConfigs.Count && World.EntityConfigs[i].Kind == kind);

    /// <summary>
    /// The respawn the player came within reach of last, or started at: where the player would start from if the world
    /// restarted. Null until the player has been near one.
    /// </summary>
    public EntityConfig.Respawn? CurrentRespawn { get; private set; }

    /// <summary>Raised on the tick of play when the player comes within reach of a respawn that isn't the current one.</summary>
    public event Action<EntityConfig.Respawn>? CurrentRespawnChanged;

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
        // Run the last tick's post-physics-step deferred actions.
        _defer.DrainAfterPhysicsStepActions();
        UpdateFromFeatures();

        if (!ControllersEnabled)
        {
            return;
        }

        // Start with a clean slate, in case some post-entity-steps deferred actions sneaked in somehow.
        _defer.Clear();
        try
        {
            foreach (Entity entity in _entities)
            {
                entity.StepPaused(RealDt, _defer);
            }
        }
        catch
        {
            // We can't count on the scene being in the expected state for the deferred actions, so don't do them.
            _defer.Clear();
            throw;
        }
        // Run this tick's post-entity-steps deferred actions.
        _defer.DrainAfterEntityStepsActions();
    }

    public override void _PhysicsProcess(double delta)
    {
        // Run the last tick's post-physics-step deferred actions.
        _defer.DrainAfterPhysicsStepActions();
        UpdateFromFeatures();

        // Start with a clean slate, in case some post-entity-steps deferred actions sneaked in somehow.
        _defer.Clear();
        try
        {
            foreach (Entity entity in _entities)
            {
                if (entity.CanProcess())
                {
                    entity.Step((float)delta, RealDt, _defer, ControllersEnabled);
                }
            }
        }
        catch
        {
            // We can't count on the scene being in the expected state for the deferred actions, so don't do them.
            _defer.Clear();
            throw;
        }
        // Run this tick's post-entity-steps deferred actions.
        _defer.DrainAfterEntityStepsActions();

        UpdateCurrentRespawn();
    }

    // Listens for changes to the features only while in the tree, so that the event doesn't keep the world alive: Godot
    // takes a node out of the tree before freeing it.
    public override void _EnterTree()
    {
        // They may have changed while the world was out of the tree.
        _featuresChanged = true;
        Features.Changed += OnFeaturesChanged;
    }

    public override void _ExitTree() => Features.Changed -= OnFeaturesChanged;

    private void OnFeaturesChanged() => _featuresChanged = true;

    // Puts the features that the engine holds in it, if they have changed, before any entity steps, so that a change
    // takes effect at the next tick and every controller sees it (029 §2).
    private void UpdateFromFeatures()
    {
        if (!_featuresChanged)
        {
            return;
        }
        _featuresChanged = false;

        // Godot reloads the body's material on every change, even to the same value.
        PhysicsMaterial tiles = _tiles.PhysicsMaterialOverride;
        if (tiles.Friction != Features.Current.TileDefaultFriction)
        {
            tiles.Friction = Features.Current.TileDefaultFriction;
        }

        foreach (Entity entity in _entities)
        {
            entity.UpdateFromFeatures();
        }
    }

    /// <summary>
    /// Makes the respawn nearest the player, box to box, the current one, if it is within reach, as of the engine's last
    /// step.
    /// </summary>
    private void UpdateCurrentRespawn()
    {
        var center = new Vector2(Player.Position.X, Player.Position.Y);
        EntityConfig.Respawn? nearest = null;
        float nearestGap = 0;
        foreach (EntityConfig.Respawn respawn in World.Respawns)
        {
            // How far apart the boxes are along each axis, or 0 where they overlap along it.
            Vector2 apart = (center - respawn.Start - new Vector2(0, respawn.MapHalfSize.Y)).Abs()
                - Player.HalfSize - respawn.MapHalfSize;
            float gap = new Vector2(MathF.Max(apart.X, 0), MathF.Max(apart.Y, 0)).Length();
            if (gap <= Features.Current.PlayerRespawnReach && (nearest is null || gap < nearestGap))
            {
                nearest = respawn;
                nearestGap = gap;
            }
        }
        if (nearest is not null && nearest != CurrentRespawn)
        {
            CurrentRespawn = nearest;
            CurrentRespawnChanged?.Invoke(nearest);
        }
    }

    /// <summary>
    /// A physics tick's length in real time. The engine's time scale shortens the delta it gives each tick, but not
    /// how often ticks come.
    /// </summary>
    private static float RealDt => 1f / Engine.PhysicsTicksPerSecond;

    private sealed class ActionQueue
    {
        private readonly List<Action> _actions = [];

        public void Clear() => _actions.Clear();
        public void Add(Action action) => _actions.Add(action);

        /// <summary>
        /// Runs the deferred actions, in the order they were deferred, and forgets them. An action can't defer another.
        /// </summary>
        public void Drain()
        {
            try
            {
                foreach (Action action in _actions)
                {
                    action();
                }
            }
            finally
            {
                _actions.Clear();
            }
        }
    }

    private sealed class Defer : IDefer
    {
        private readonly ActionQueue _afterEntityStepsActions = new();
        private readonly ActionQueue _afterPhysicsStepActions = new();

        public void Clear()
        {
            _afterEntityStepsActions.Clear();
            _afterPhysicsStepActions.Clear();
        }

        public void RunAfterEntitySteps(Action action) => _afterEntityStepsActions.Add(action);

        public void RunAfterPhysicsStep(Action action) => _afterPhysicsStepActions.Add(action);

        public void DrainAfterEntityStepsActions() => _afterEntityStepsActions.Drain();

        public void DrainAfterPhysicsStepActions() => _afterPhysicsStepActions.Drain();
    }
}
