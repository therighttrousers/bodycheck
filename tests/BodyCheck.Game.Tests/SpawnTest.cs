using System.Threading.Tasks;

using BodyCheck.Core;
using BodyCheck.Core.Controllers;

using GdUnit4;

using static GdUnit4.Assertions;

namespace BodyCheck.Game.Tests;

/// <summary>The player spawning its block under itself.</summary>
[TestSuite]
[RequireGodotRuntime]
public class SpawnTest
{
    private static readonly PlayerInput Spawn = new(0, 0, false, SpawnHeld: true);

    private RoomRun? _run;

    [AfterTest]
    public void TearDown()
    {
        _run?.Free();
        _run = null;
    }

    [TestCase]
    public async Task Spawning_on_the_floor_lifts_the_player_onto_the_block()
    {
        _run = new RoomRun(RoomRun.RoomOf(
            "##########",
            "#........#",
            "#........#",
            "#........#",
            "#...P....#",
            "##########"));
        await _run.Tick(RoomRun.Idle, 30);
        var player = _run.Player;
        var block = _run.Node.PlayerBlock;
        float floor = player.Bottom();
        AssertBool(block.Visible).IsFalse();

        await _run.Tick(Spawn);
        await _run.Tick(RoomRun.Idle, 10);
        AssertBool(block.Visible).IsTrue();
        AssertFloat(block.Bottom()).IsEqualApprox(floor, 0.01f);
        AssertFloat(player.Bottom()).IsEqualApprox(block.Top(), 0.01f);
        AssertBool(player.OnGround).IsTrue();
    }

    [TestCase]
    public async Task Spawning_again_takes_the_block_from_where_it_was_after_the_cooldown()
    {
        _run = new RoomRun(RoomRun.RoomOf(
            "##############",
            "#............#",
            "#............#",
            "#............#",
            "#..P.........#",
            "##############"));
        await _run.Tick(RoomRun.Idle, 30);
        var player = _run.Player;
        var block = _run.Node.PlayerBlock;
        await _run.Tick(Spawn);
        await _run.Tick(RoomRun.Idle, 10);

        // Off the block and away from it, along the floor.
        await _run.Tick(RoomRun.Right, 40);
        await _run.Tick(RoomRun.Idle, 60);
        AssertFloat(player.Position.X - block.Position.X).IsGreater(2);

        // The cooldown has passed by now, so this spawns it again, under the player.
        AssertFloat(Features.Current.PlayerSpawnCooldownTime).IsLess(110 / 60f);
        await _run.Tick(Spawn);
        await _run.Tick(RoomRun.Idle, 10);
        AssertFloat(block.Position.X).IsEqualApprox(player.Position.X, 0.05f);
        AssertFloat(player.Bottom()).IsEqualApprox(block.Top(), 0.01f);

        // But not again until the cooldown has passed again.
        await _run.Tick(RoomRun.Right, 40);
        await _run.Tick(Spawn);
        await _run.Tick(RoomRun.Idle, 10);
        AssertFloat(player.Position.X - block.Position.X).IsGreater(2);
    }
}
