using System.Collections.Generic;
using System.Threading.Tasks;

using BodyCheck.Core;
using BodyCheck.Core.Controllers;
using BodyCheck.Core.Entities;

using GdUnit4;

using Godot;

using static GdUnit4.Assertions;

namespace BodyCheck.Tests;

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

    [TestCase]
    public async Task Spawning_against_a_wall_puts_the_block_beside_it_on_the_floor()
    {
        _run = new RoomRun(RoomRun.RoomOf(
            "##########",
            "#........#",
            "#........#",
            "#........#",
            "#...P....#",
            "##########"));
        // The block is wider than the player, so centered on a player against the wall it would be in the wall.
        await _run.Tick(RoomRun.Left, 60);
        await _run.Tick(RoomRun.Idle, 30);
        var player = _run.Player;
        var block = _run.Node.PlayerBlock;
        float floor = player.Bottom();
        AssertFloat(player.Left()).IsEqualApprox(1, 0.01f);

        await _run.Tick(Spawn);
        await _run.Tick(RoomRun.Idle, 10);
        AssertBool(block.Visible).IsTrue();
        AssertFloat(block.Left()).IsEqualApprox(1, 0.01f);
        AssertFloat(block.Bottom()).IsEqualApprox(floor, 0.01f);
        AssertFloat(player.Bottom()).IsEqualApprox(block.Top(), 0.01f);
        AssertBool(player.OnGround).IsTrue();
    }

    [TestCase]
    public async Task Spawning_with_no_room_to_stand_on_the_block_is_cancelled()
    {
        _run = new RoomRun(RoomRun.RoomOf(
            "##########",
            "#........#",
            "#........#",
            "#####....#",
            "#........#",
            "#P.......#",
            "##########"));
        await _run.Tick(RoomRun.Idle, 30);
        var player = _run.Player;
        var block = _run.Node.PlayerBlock;
        var cues = new List<EntityCue>();
        player.CueRaised += cues.Add;
        Vector3 start = player.Position;

        // Under a ceiling two tiles up, the player can't stand on the block.
        await _run.Tick(Spawn);
        await _run.Tick(RoomRun.Idle);
        AssertBool(block.Visible).IsFalse();
        AssertArray(cues).ContainsExactly(EntityCue.SpawnCancelled);
        AssertFloat(player.Position.Y).IsEqualApprox(start.Y, 0.01f);

        // Out from under it, once the cancel's cooldown has passed, it spawns, before a spawn's cooldown would have.
        await _run.Tick(RoomRun.Right, 35);
        await _run.Tick(RoomRun.Idle, 20);
        AssertFloat(player.Left()).IsGreater(5);
        AssertFloat(Features.Current.PlayerSpawnCancelCooldownTime).IsLess(56 / 60f);
        AssertFloat(Features.Current.PlayerSpawnCooldownTime).IsGreater(56 / 60f);
        await _run.Tick(Spawn);
        await _run.Tick(RoomRun.Idle, 10);
        AssertBool(block.Visible).IsTrue();
        AssertFloat(player.Bottom()).IsEqualApprox(block.Top(), 0.01f);
    }

    [TestCase(-1)]
    [TestCase(0)]
    [TestCase(1)]
    public async Task Spawning_at_the_bottom_of_a_one_tile_shaft_lifts_the_player_onto_the_block(int nudge)
    {
        _run = new RoomRun(RoomRun.RoomOf(
            "#####",
            "##.##",
            "##.##",
            "##.##",
            "##.##",
            "##P##",
            "#####"));
        // The block is exactly as wide as the shaft, wherever in it the player stands.
        await _run.Tick(RoomRun.Idle, 30);
        await _run.Tick(new PlayerInput(nudge, 0, false), 10);
        await _run.Tick(RoomRun.Idle, 10);
        var player = _run.Player;
        var block = _run.Node.PlayerBlock;
        float floor = player.Bottom();

        await _run.Tick(Spawn);
        await _run.Tick(RoomRun.Idle, 10);
        AssertBool(block.Visible).IsTrue();
        AssertFloat(block.Position.X).IsEqualApprox(2.5f, 0.01f);
        AssertFloat(block.Bottom()).IsEqualApprox(floor, 0.01f);
        AssertFloat(player.Bottom()).IsEqualApprox(block.Top(), 0.01f);
        AssertBool(player.OnGround).IsTrue();
    }

    [TestCase]
    public async Task Spawning_while_falling_down_a_one_tile_shaft_puts_the_block_under_the_player()
    {
        _run = new RoomRun(RoomRun.RoomOf(
            "#####",
            "##.##",
            "##P##",
            "##.##",
            "##.##",
            "##.##",
            "##.##",
            "##.##",
            "##.##",
            "##.##",
            "#####"));
        var player = _run.Player;
        var block = _run.Node.PlayerBlock;
        await _run.Tick(RoomRun.Idle, 10);
        AssertBool(player.OnGround).IsFalse();

        await _run.Tick(Spawn);
        AssertBool(block.Visible).IsTrue();
        AssertFloat(block.Top()).IsEqualApprox(player.Bottom(), 0.1f);

        // Both fall to the floor, the player onto the block.
        await _run.Tick(RoomRun.Idle, 90);
        AssertFloat(block.Bottom()).IsEqualApprox(1, 0.01f);
        AssertFloat(player.Bottom()).IsEqualApprox(block.Top(), 0.01f);
    }
}
