using System.Collections.Generic;
using System.Threading.Tasks;

using BodyCheck.Core.WorldData;

using GdUnit4;

using static GdUnit4.Assertions;

namespace BodyCheck.Tests;

/// <summary>Respawns: where the player starts when the world restarts, set by coming near one.</summary>
[TestSuite]
[RequireGodotRuntime]
public class RespawnTest
{
    // Respawns 4 and 12 tiles to the right of the player start, identified by their places in the room.
    private static readonly string[] Rows =
    [
        "################",
        "#..............#",
        "#..............#",
        "#P...S.......S.#",
        "################",
    ];

    private RoomRun? _run;

    [AfterTest]
    public void TearDown()
    {
        _run?.Free();
        _run = null;
    }

    [TestCase]
    public async Task Coming_within_reach_of_a_respawn_makes_it_the_current_one()
    {
        _run = new RoomRun(RoomRun.RoomOf(Rows));
        var reached = new List<string>();
        _run.Node.CurrentRespawnChanged += respawn => reached.Add(respawn.Id);

        // The nearer respawn is 3.1 tiles from the player's box, out of reach.
        await _run.Tick(RoomRun.Idle, 10);
        AssertObject(_run.Node.CurrentRespawn).IsNull();

        // Running right past both, then back past the nearer one.
        await _run.Tick(RoomRun.Right, 60);
        await _run.Tick(RoomRun.Left, 40);
        AssertArray(reached).ContainsExactly("Test 5,3", "Test 13,3", "Test 5,3");
        AssertString(_run.Node.CurrentRespawn?.Id).IsEqual("Test 5,3");
    }

    [TestCase]
    public async Task The_player_starts_at_the_respawn_it_is_given()
    {
        _run = new RoomRun(RoomRun.RoomOf(Rows), respawnId: "Test 13,3");
        await _run.Tick(RoomRun.Idle, 10);

        AssertFloat(_run.Player.Position.X).IsEqualApprox(13.5f, 0.01f);
        AssertString(_run.Node.CurrentRespawn?.Id).IsEqual("Test 13,3");
    }

    [TestCase]
    public async Task The_player_starts_at_the_player_start_if_the_respawn_is_gone()
    {
        _run = new RoomRun(RoomRun.RoomOf(Rows), respawnId: "Removed");
        await _run.Tick(RoomRun.Idle, 10);

        AssertFloat(_run.Player.Position.X).IsEqualApprox(1.5f, 0.01f);
        AssertObject(_run.Node.CurrentRespawn).IsNull();
    }
}
