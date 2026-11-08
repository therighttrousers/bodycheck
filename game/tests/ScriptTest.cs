using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using BodyCheck.Core;
using BodyCheck.Core.Entities;
using BodyCheck.Core.Scripts;
using BodyCheck.Core.Simulation;
using BodyCheck.Core.Tasks;
using BodyCheck.Core.WorldData;

using GdUnit4;

using Godot;

using static GdUnit4.Assertions;

namespace BodyCheck.Tests;

/// <summary>Scripts, run by the world from a <c>Script</c> entity.</summary>
[TestSuite]
[RequireGodotRuntime]
public class ScriptTest
{
    private const int RunTicks = 30;

    private RoomRun? _run;
    private GameModeManager? _manager;

    [AfterTest]
    public void TearDown()
    {
        if (_manager is not null)
        {
            RoomRun.FreeAndFinalize(_manager);
            _manager = null;
        }
        _run?.Free();
        _run = null;
    }

    /// <summary>Holds a cutscene, and runs the player right for <see cref="RunTicks"/> ticks.</summary>
    private sealed class RunRight : IScript
    {
        public async TickTask Run(IScriptWorld world)
        {
            using IDisposable cutscene = world.HoldCutscene();
            world.PlayerController.ScriptedInput = RoomRun.Right;
            for (int i = 0; i < RunTicks; i++)
            {
                await world.Tick.NextTick();
            }
        }
    }

    /// <summary>A script that takes the player, and finishes at once.</summary>
    private sealed class TakesThePlayer(PlayerEntity player) : IScript
    {
        public PlayerEntity Player { get; } = player;

        public TickTask Run(IScriptWorld world) => TickTask.Finished;
    }

    /// <summary>
    /// Knows <see cref="RunRight"/>, which takes no entities, and <see cref="TakesThePlayer"/>, by their names, and keeps
    /// the scripts it makes.
    /// </summary>
    private sealed class Library : IScriptLibrary
    {
        public List<IScript> Made { get; } = [];

        public bool Holds(string className, IReadOnlyList<Type> entityTypes) => className switch
        {
            nameof(RunRight) => entityTypes.Count == 0,
            nameof(TakesThePlayer) => entityTypes is [Type type] && type == typeof(PlayerEntity),
            _ => false,
        };

        public IScript Create(string className, IReadOnlyList<Entity> entities)
        {
            IScript script = className == nameof(RunRight) ? new RunRight() : new TakesThePlayer((PlayerEntity)entities[0]);
            Made.Add(script);
            return script;
        }
    }

    /// <summary>
    /// A room with a trigger around the player, which runs the script <paramref name="className"/>, given
    /// <paramref name="entities"/>, when it is set off by <paramref name="setOffBy"/>.
    /// </summary>
    private static Room RoomWithScript(
        string className,
        TriggerSetOffBy setOffBy = TriggerSetOffBy.Touch,
        IReadOnlyList<string>? entities = null,
        bool startsOn = true)
    {
        string[] rows =
        [
            "##############################",
            "#............................#",
            "#............................#",
            "#P...........................#",
            "##############################",
        ];
        Room room = RoomRun.RoomOf(rows);
        EntityConfig player = room.EntityConfigs.Single(config => config.Kind == EntityKind.Player);
        var script = new EntityConfig.Script
        {
            Id = "Script",
            MapOrigin = new Vector2I(1, 1),
            MapSize = Vector2I.One,
            MapColor = Colors.White,
            ClassName = className,
            Entities = entities ?? [],
        };
        var trigger = new EntityConfig.Trigger
        {
            Id = "Trigger",
            MapOrigin = player.MapOrigin,
            MapSize = player.MapSize,
            MapColor = Colors.White,
            Step = new Step(script.Id, TriggerAction.Default),
            StartsOn = startsOn,
            SetOffBy = setOffBy,
        };
        return new Room(room.Name, room.Origin, TileGrid.FromText(rows), room.EntityConfigs.Append(trigger).Append(script));
    }

    [TestCase]
    public async Task The_player_runs_on_a_scripts_input_while_the_script_holds_a_cutscene()
    {
        _run = new RoomRun(RoomWithScript(nameof(RunRight)), scripts: new Library());
        WorldNode world = _run.Node;
        world.GameModeSource = () => world.CutsceneRequest is null ? GameMode.Playing : GameMode.Cutscene;
        float start = world.Player.Position.X;

        await _run.Tick(RoomRun.Idle, RunTicks / 2);
        AssertThat(world.RunningScripts.Count).IsEqual(1);
        AssertThat(world.CutsceneRequest).IsNotNull();
        AssertThat(world.Player.Position.X - start).IsGreater(0.5f);

        await _run.Tick(RoomRun.Idle, RunTicks);
        AssertThat(world.RunningScripts.Count).IsEqual(0);
        AssertThat(world.CutsceneRequest).IsNull();
        AssertThat(world.PlayerController.ScriptedInput).IsEqual(default(PlayerInput));
    }

    [TestCase]
    public async Task A_start_trigger_runs_its_script_once_given_its_entities()
    {
        var library = new Library();
        // A text room's ids are its name and the cell: the player's 'P' is at 1,3.
        _run = new RoomRun(RoomWithScript(nameof(TakesThePlayer), TriggerSetOffBy.Start, ["Test 1,3"]), scripts: library);

        await _run.Tick(RoomRun.Idle, 3);
        AssertThat(library.Made).HasSize(1);
        AssertThat(((TakesThePlayer)library.Made[0]).Player).IsSame(_run.Node.Player);
    }

    [TestCase]
    public async Task Removing_an_entity_ends_its_scopes_while_it_is_whole_then_takes_it_out()
    {
        _run = new RoomRun(RoomRun.RoomOf(
            "######",
            "#P..B#",
            "######"));
        WorldNode world = _run.Node;
        Entity block = world.EntitiesOf(EntityKind.Block).Single();
        bool wasInTree = false;
        ITick tick = world.Tick.Scoped(block.IsLive);

        async TickTask Hovers()
        {
            try
            {
                while (true)
                {
                    await tick.NextTick();
                }
            }
            finally
            {
                wasInTree = block.IsInsideTree();
            }
        }

        // Awaited, and its cancellation caught, as a script's route would await it, so that the world doesn't warn of it.
        bool cancelled = false;
        async TickTask Awaits()
        {
            try
            {
                await Hovers();
            }
            catch (OperationCanceledException)
            {
                cancelled = true;
            }
        }

        _ = Awaits();
        await _run.Tick(RoomRun.Idle, 2);
        world.Remove(block);
        world.Remove(block);
        AssertThat(block.IsLive()).IsFalse();

        await _run.Tick(RoomRun.Idle);
        AssertThat(wasInTree).IsTrue();
        AssertThat(cancelled).IsTrue();
        AssertThat(world.EntitiesOf(EntityKind.Block)).IsEmpty();
        // Out of the tree at once, and freed by the end of the frame.
        AssertThat(!GodotObject.IsInstanceValid(block) || !block.IsInsideTree()).IsTrue();
        AssertThrown(() => world.Remove(world.Player)).IsInstanceOf<ArgumentException>();
        AssertThrown(() => world.Remove(world.PlayerBlock)).IsInstanceOf<ArgumentException>();
    }

    [TestCase]
    public void A_room_rejects_a_start_trigger_that_starts_off() =>
        AssertThrown(() => RoomWithScript(nameof(RunRight), TriggerSetOffBy.Start, startsOn: false))
            .IsInstanceOf<ArgumentException>();

    [TestCase]
    public void A_script_takes_entities_through_its_constructor_in_order_with_a_list_for_the_rest()
    {
        Type[] none = [];
        Type[] robotAndBlocks = [typeof(RobotEntity), typeof(Entity), typeof(Entity)];

        AssertThat(ScriptArguments.Constructor(typeof(RunRight), none)).IsNotNull();
        AssertThat(ScriptArguments.Constructor(typeof(RunRight), [typeof(Entity)])).IsNull();
        AssertThat(ScriptArguments.Constructor(typeof(RobotThenRest), robotAndBlocks)).IsNotNull();
        AssertThat(ScriptArguments.Constructor(typeof(RobotThenRest), [typeof(RobotEntity)])).IsNotNull();
        AssertThat(ScriptArguments.Constructor(typeof(RobotThenRest), [typeof(Entity)])).IsNull();
        AssertThat(ScriptArguments.Constructor(typeof(RestThenRobot), robotAndBlocks)).IsNull();
        AssertThat(ScriptArguments.Constructor(typeof(TakesThePlayer), [typeof(PlayerEntity), typeof(Entity)])).IsNull();
    }

    private sealed class RobotThenRest(RobotEntity robot, IReadOnlyList<Entity> rest) : IScript
    {
        public IReadOnlyList<Entity> Entities { get; } = [robot, .. rest];

        public TickTask Run(IScriptWorld world) => TickTask.Finished;
    }

    private sealed class RestThenRobot(IReadOnlyList<Entity> rest, RobotEntity robot) : IScript
    {
        public IReadOnlyList<Entity> Entities { get; } = [robot, .. rest];

        public TickTask Run(IScriptWorld world) => TickTask.Finished;
    }

    [TestCase]
    public async Task The_game_presents_a_cutscene_as_the_holds_held_ask_as_they_come_and_go()
    {
        _run = new RoomRun(RoomRun.RoomOf(
            "#####",
            "#.P.#",
            "#####"));
        WorldNode world = _run.Node;
        // Added after the world, as the game adds it.
        _manager = new GameModeManager { World = world };
        ((SceneTree)Engine.GetMainLoop()).Root.AddChild(_manager);
        GameModeState? state = null;
        _manager.GameModeChanged += changed => state = changed;
        bool BlackBars() => ((GameModeState.Cutscene)state!).Request.BlackBars;
        await _run.Tick(RoomRun.Idle, 2);

        IDisposable plain = world.HoldCutscene();
        await _run.Tick(RoomRun.Idle);
        AssertThat(BlackBars()).IsFalse();

        // Two holds that share a request each count.
        var bars = new CutsceneRequest(BlackBars: true);
        IDisposable first = world.HoldCutscene(bars);
        IDisposable second = world.HoldCutscene(bars);
        await _run.Tick(RoomRun.Idle);
        AssertThat(BlackBars()).IsTrue();
        first.Dispose();
        await _run.Tick(RoomRun.Idle);
        AssertThat(BlackBars()).IsTrue();
        second.Dispose();
        await _run.Tick(RoomRun.Idle);
        AssertThat(BlackBars()).IsFalse();

        plain.Dispose();
        await _run.Tick(RoomRun.Idle);
        AssertThat(_manager.GameMode).IsEqual(GameMode.Playing);
    }

    [TestCase(true)]
    [TestCase(false)]
    public void A_world_doesnt_build_with_a_script_its_library_doesnt_hold(bool hasLibrary)
    {
        var world = new World(RoomWithScript("Missing"));
        AssertThrown(() => WorldNode.Build(world, new AssetLibrary(), scripts: hasLibrary ? new Library() : null))
            .IsInstanceOf<ArgumentException>();
    }
}
