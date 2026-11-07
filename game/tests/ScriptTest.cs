using System;
using System.Linq;
using System.Threading.Tasks;

using BodyCheck.Core;
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
        public async TickTask Run(IScriptWorld world, ITick tick)
        {
            using IDisposable cutscene = world.HoldCutscene();
            world.PlayerController.ScriptedInput = RoomRun.Right;
            for (int i = 0; i < RunTicks; i++)
            {
                await tick.NextTick();
            }
        }
    }

    /// <summary>Knows only <see cref="RunRight"/>, as <c>RunRight</c>.</summary>
    private sealed class Library : IScriptLibrary
    {
        public bool Holds(string className) => className == nameof(RunRight);

        public IScript Create(string className) => new RunRight();
    }

    /// <summary>A room with a trigger around the player, which runs the script <paramref name="className"/>.</summary>
    private static Room RoomWithScript(string className)
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
        };
        var trigger = new EntityConfig.Trigger
        {
            Id = "Trigger",
            MapOrigin = player.MapOrigin,
            MapSize = player.MapSize,
            MapColor = Colors.White,
            Step = new Step(script.Id, TriggerAction.Default),
            StartsOn = true,
            SetOffBy = TriggerSetOffBy.Touch,
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
