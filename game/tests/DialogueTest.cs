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
        _box = new DialogueBox();
        Tree.Root.AddChild(_box);
        // Added after the world, as the game adds it.
        _manager = new GameModeManager { World = _run.Node };
        Tree.Root.AddChild(_manager);
        await _run.Tick(RoomRun.Idle, 2);

        bool resumed = false;
        async TickTask Waits(ITick tick)
        {
            await new DialogueRunner(_box, _run.Node, "debug", "start").Default(tick);
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
            RoomRun.FreeAndFinalize(new WorldNode(world, new AssetLibrary(), dialogue: _box));
        }
        else
        {
            AssertThrown(() => new WorldNode(world, new AssetLibrary(), dialogue: _box)).IsInstanceOf<ArgumentException>();
        }
    }
}
