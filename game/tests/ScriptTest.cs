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

    [AfterTest]
    public void TearDown()
    {
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
        };
        return new Room(room.Name, room.Origin, TileGrid.FromText(rows), room.EntityConfigs.Append(trigger).Append(script));
    }

    [TestCase]
    public async Task The_player_runs_on_a_scripts_input_while_the_script_holds_a_cutscene()
    {
        _run = new RoomRun(RoomWithScript(nameof(RunRight)), scripts: new Library());
        WorldNode world = _run.Node;
        world.GameModeSource = () => world.CutsceneRequested ? GameMode.Cutscene : GameMode.Playing;
        float start = world.Player.Position.X;

        await _run.Tick(RoomRun.Idle, RunTicks / 2);
        AssertThat(world.RunningScripts.Count).IsEqual(1);
        AssertThat(world.CutsceneRequested).IsTrue();
        AssertThat(world.Player.Position.X - start).IsGreater(0.5f);

        await _run.Tick(RoomRun.Idle, RunTicks);
        AssertThat(world.RunningScripts.Count).IsEqual(0);
        AssertThat(world.CutsceneRequested).IsFalse();
        AssertThat(world.PlayerController.ScriptedInput).IsEqual(default(PlayerInput));
    }

    [TestCase(true)]
    [TestCase(false)]
    public void A_world_doesnt_build_with_a_script_its_library_doesnt_hold(bool hasLibrary)
    {
        var world = new World(RoomWithScript("Missing"));
        AssertThrown(() => new WorldNode(world, new AssetLibrary(), scripts: hasLibrary ? new Library() : null))
            .IsInstanceOf<ArgumentException>();
    }
}
