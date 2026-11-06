using System.Linq;
using System.Threading.Tasks;

using BodyCheck.Core.Tasks;

using GdUnit4;

using Godot;

using static GdUnit4.Assertions;

namespace BodyCheck.Tests;

/// <summary>
/// Dialogue through Dialogue Manager, the <see cref="DialogueBox"/> and the dialogue mode of the
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
    public async Task A_dialogue_pauses_the_game_until_it_ends_and_what_waits_resumes_in_the_next_played_tick()
    {
        _run = new RoomRun(RoomRun.RoomOf(
            "#####",
            "#.P.#",
            "#####"));
        _box = new DialogueBox();
        Tree.Root.AddChild(_box);
        // Added after the world, as the game adds it.
        _manager = new GameModeManager { World = _run.Node, Dialogue = _box };
        Tree.Root.AddChild(_manager);
        await _run.Tick(RoomRun.Idle, 2);

        bool resumed = false;
        async TickTask Waits(ITick tick)
        {
            await _box.Present(tick, "debug", "start");
            resumed = true;
        }
        _ = Waits(_run.Node.Tick);

        await _run.Tick(RoomRun.Idle);
        AssertThat(_manager.GameMode).IsEqual(GameMode.Dialogue);
        AssertThat(Tree.Paused).IsTrue();

        // Dialogue Manager fetches each line a frame or more after it is asked, so go on whenever one is shown.
        for (int i = 0; i < 300 && _box.IsPresenting; i++)
        {
            _box.Advance();
            await _run.Tick(RoomRun.Idle);
        }
        AssertThat(_box.IsPresenting).IsFalse();

        // The tick after it ends, the world is still paused, and the game goes back to playing after its turn.
        await _run.Tick(RoomRun.Idle);
        AssertThat(_manager.GameMode).IsEqual(GameMode.Playing);
        AssertThat(Tree.Paused).IsFalse();
        AssertThat(resumed).IsFalse();

        // The next tick is played, and what waits for the dialogue resumes in it.
        await _run.Tick(RoomRun.Idle);
        AssertThat(resumed).IsTrue();
    }
}
