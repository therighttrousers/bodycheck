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

using DialogueManagerRuntime;

using GdUnit4;

using Godot;

using static BodyCheck.Tests.Exceptions;
using static GdUnit4.Assertions;

namespace BodyCheck.Tests;

/// <summary>
/// Dialogue through Dialogue Manager, the <see cref="DialogueBox"/> and the cutscene mode of the
/// <see cref="GameModeManager"/>, with the game's debug dialogue.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class DialogueTest
{
    private static SceneTree Tree => (SceneTree)Engine.GetMainLoop();

    private RoomRun? _run;
    private DialogueBox? _box;
    private GameModeManager? _manager;

    [AfterTest]
    public void TearDown()
    {
        _manager?.Free();
        _manager = null;
        _box?.Free();
        _box = null;
        Engine.TimeScale = 1;
        Tree.Paused = false;
        _run?.Free();
        _run = null;
    }

    [TestCase]
    public void The_dialogue_font_draws_the_tofu_only_from_its_fallback()
    {
        var font = (FontVariation)GD.Load<Theme>("res://assets/fonts/dialogue_theme.tres").DefaultFont;
        int tofu = Garble.Tofu.Value;

        // A codepoint the locale's font has a glyph for would never reach the fallback.
        AssertThat(((FontFile)font.BaseFont).HasChar(tofu)).IsFalse();
        AssertThat(font.Fallbacks.Any(fallback => fallback.HasChar(tofu))).IsTrue();
    }

    [TestCase]
    public async Task A_dialogue_plays_a_cutscene_until_it_ends_and_what_waits_resumes_in_the_next_tick()
    {
        _run = new RoomRun(RoomRun.RoomOf(
            "#####",
            "#.P.#",
            "#####"));
        _box = new DialogueBox { World = _run.Node, WorldState = _run.State };
        Tree.Root.AddChild(_box);
        // Added after the world, as the game adds it.
        _manager = new GameModeManager { World = _run.Node };
        Tree.Root.AddChild(_manager);
        await _run.Tick(RoomRun.Idle, 2);

        bool resumed = false;
        async TickTask Waits(ITick tick)
        {
            await new DialogueRunner(_box, _run.Node, "debug", "start", []).Default(tick);
            resumed = true;
        }
        _ = Waits(_run.Node.Tick);

        await _run.Tick(RoomRun.Idle);
        AssertThat(_manager.GameMode).IsEqual(GameMode.Cutscene);
        AssertThat(Tree.Paused).IsFalse();

        // Dialogue Manager fetches each line a frame or more after it is asked, so go on whenever one is shown.
        for (int i = 0; i < 300 && _box.IsPresenting; i++)
        {
            _box.Advance();
            await _run.Tick(RoomRun.Idle);
        }
        AssertThat(_box.IsPresenting).IsFalse();

        // In the world's next tick, what waits for the dialogue resumes, and after the world's turn, the game goes back
        // to playing.
        await _run.Tick(RoomRun.Idle);
        AssertThat(resumed).IsTrue();
        AssertThat(_manager.GameMode).IsEqual(GameMode.Playing);
    }

    [TestCase]
    public async Task A_dialogue_names_its_speaker_and_waits_for_an_action_it_asks_of_it()
    {
        string[] rows =
        [
            "##########",
            "#........#",
            "#........#",
            "#........#",
            "#........#",
            "#........#",
            "#........#",
            "#P.......#",
            "##########",
        ];
        // A door 3 tiles high, closed on the floor, which opens up to its stop 3 tiles above it.
        Room room = RoomRun.RoomOf(rows);
        var doorConfig = new EntityConfig.VerticalDoor
        {
            Id = "door",
            MapOrigin = new Vector2I(6, 1),
            MapSize = new Vector2I(1, 3),
            MapColor = Colors.White,
            OpenStop = new Vector2I(6, 7),
            CloseStop = new Vector2I(6, 0),
        };
        _run = new RoomRun(new Room(room.Name, room.Origin, TileGrid.FromText(rows), room.EntityConfigs.Append(doorConfig)));
        _box = new DialogueBox { World = _run.Node, WorldState = _run.State };
        Tree.Root.AddChild(_box);
        Entity door = _run.Node.EntitiesOf(EntityKind.VerticalDoor).Single();
        float closed = door.Bottom();

        // Who says each line, and where the door is as it is said.
        var lines = new List<(string Character, float Bottom)>();
        void Got(DialogueLine line) => lines.Add((line.Character, door.Bottom()));
        DialogueManager.GotDialogue += Got;
        try
        {
            TickTask presented = _box.Present(_run.Node.Tick, "debug", "act", [door]);
            for (int i = 0; i < 600 && _box.IsPresenting; i++)
            {
                _box.Advance();
                await _run.Tick(RoomRun.Idle);
            }
            AssertThat(presented.IsFinished).IsTrue();
        }
        finally
        {
            DialogueManager.GotDialogue -= Got;
        }

        string name = _run.State.GetEntity("door").Name;
        AssertThat(lines.Select(line => line.Character)).ContainsExactly(name, name);
        // The second line waits until the door has opened.
        AssertFloat(lines[0].Bottom).IsEqualApprox(closed, 0.05f);
        AssertFloat(lines[1].Bottom).IsEqualApprox(closed + 3, 0.05f);
        AssertThat(_run.State.GetEntity<WorldState.DoorState>("door").LastAction).IsEqual("Open");
    }

    [TestCase]
    public void Dropping_the_world_drops_its_dialogue_so_that_the_next_world_presents_its_own_at_once()
    {
        _run = new RoomRun(RoomRun.RoomOf(
            "#####",
            "#.P.#",
            "#####"));
        _box = new DialogueBox { World = _run.Node, WorldState = _run.State };
        Tree.Root.AddChild(_box);

        TickTask dropped = _box.Present(_run.Node.Tick, "debug", "start", []);
        AssertThat(_box.IsPresenting).IsTrue();
        _run.Node.StopAndQueueFree();
        AssertThat(dropped.IsCancelled).IsTrue();
        AssertThat(_box.IsPresenting).IsFalse();

        // As the next world's tick.
        var next = new TickScheduler(Rethrow);
        _ = _box.Present(next.Tick, "debug", "start", []);
        AssertThat(_box.IsPresenting).IsTrue();
        next.Stop();
    }

    [TestCase]
    public async Task In_a_cutscene_the_player_gets_no_input()
    {
        _run = new RoomRun(RoomRun.RoomOf(
            "#######",
            "#..P..#",
            "#######"));
        _run.Node.GameModeSource = () => GameMode.Cutscene;
        float start = _run.Node.Player.Position.X;

        await _run.Tick(RoomRun.Right, 30);

        AssertThat(Mathf.Abs(_run.Node.Player.Position.X - start)).IsLess(0.01f);
    }

    [TestCase]
    public async Task A_jump_held_as_a_cutscene_ends_jumps_only_when_pressed_again()
    {
        _run = new RoomRun(RoomRun.RoomOf(
            "#######",
            "#.....#",
            "#.....#",
            "#.....#",
            "#.....#",
            "#..P..#",
            "#######"));
        GameMode mode = GameMode.Cutscene;
        _run.Node.GameModeSource = () => mode;
        await _run.Tick(RoomRun.Idle, 10);
        float ground = _run.Node.Player.Position.Y;

        // Jump is pressed in the cutscene, as to end a dialogue, and still held when play resumes.
        await _run.Tick(RoomRun.Jump, 5);
        mode = GameMode.Playing;
        await _run.Tick(RoomRun.Jump, 10);
        AssertThat(_run.Node.Player.Position.Y - ground).IsLess(0.01f);

        await _run.Tick(RoomRun.Idle);
        await _run.Tick(RoomRun.Jump, 10);
        AssertThat(_run.Node.Player.Position.Y - ground).IsGreater(0.5f);
    }

    [TestCase("debug", "start", true)]
    [TestCase("debug", "missing", false)]
    [TestCase("missing", "start", false)]
    public void A_world_builds_only_if_each_dialogue_names_a_title_its_file_holds(string file, string title, bool builds)
    {
        var player = new EntityConfig.Player
        {
            Id = "player",
            MapOrigin = Vector2I.Zero,
            MapSize = new Vector2I(1, 2),
            MapColor = Colors.White,
        };
        var dialogue = new EntityConfig.Dialogue
        {
            Id = "dialogue",
            MapOrigin = new Vector2I(1, 0),
            MapSize = Vector2I.One,
            MapColor = Colors.White,
            File = file,
            Title = title,
        };
        var world = new World("Test", [new Room("Test", default, new TileGrid(3, 3), [player, dialogue])]);
        _box = new DialogueBox();
        Tree.Root.AddChild(_box);

        if (builds)
        {
            RoomRun.FreeAndFinalize(WorldNode.Build(world, new WorldState(world), new AssetLibrary(), dialogue: _box));
        }
        else
        {
            AssertThrown(() => WorldNode.Build(world, new WorldState(world), new AssetLibrary(), dialogue: _box)).IsInstanceOf<ArgumentException>();
        }
    }
}
