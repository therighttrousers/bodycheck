using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using BodyCheck.Core;
using BodyCheck.Core.Controllers;
using BodyCheck.Core.Entities;
using BodyCheck.Core.WorldData;

using GdUnit4;

using Godot;

using static BodyCheck.Game.Tests.RoomRun;
using static GdUnit4.Assertions;

namespace BodyCheck.Game.Tests;

/// <summary>
/// Bodies meeting each other and the tiles: collisions, pushing, blocks, and gaps in the floor.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class BodiesTest
{
    // How far two bodies may overlap, or one may be inside a wall, before a test calls it a failure.
    private const float Slack = 0.001f;

    // When two moving bodies meet, the engine finds out a tick late, so they overlap by up to a tick's
    // closing distance, and it takes a tick or two to part them. Measured: at most 0.15 tiles, for 2 ticks.
    private const float MaxImpactOverlap = 0.2f;
    private const int MaxImpactOverlapTicks = 3;

    private RoomRun? _run;

    /// <summary>Follows how far one body's edge is past another's, tick by tick.</summary>
    private sealed class Overlap
    {
        private int _run;

        public float Deepest { get; private set; } = float.MinValue;

        /// <summary>The longest unbroken stretch of ticks with an overlap deeper than <see cref="Slack"/>.</summary>
        public int LongestTicks { get; private set; }

        public void Add(float overlap)
        {
            Deepest = MathF.Max(Deepest, overlap);
            _run = overlap > Slack ? _run + 1 : 0;
            LongestTicks = Math.Max(LongestTicks, _run);
        }
    }

    private RoomRun Start(Room room, BodyTuning? playerBody = null, bool controllersEnabled = true)
    {
        _run = new RoomRun(room, playerBody, controllersEnabled);
        return _run;
    }

    [AfterTest]
    public void TearDown()
    {
        _run?.Free();
        _run = null;
    }

    [TestCase(1f)]
    [TestCase(3f)]
    public async Task Colliding_bodies_share_their_momentum_without_bouncing(float movingMass)
    {
        // With the controllers off, the player and the robot are just two boxes on a frictionless floor.
        RoomRun run = Start(
            RoomOf(
                "#..............................#",
                "#..............................#",
                "#....P.....R...................#",
                "################################"),
            BodyTuning.Player with { Mass = movingMass },
            controllersEnabled: false);
        Entity moving = run.Player;
        Entity resting = run.Node.EntitiesOf(EntityKind.Robot).Single();
        await run.Tick(Idle, 10);
        moving.ApplyCentralImpulse(new Vector3(8 * moving.Mass, 0, 0));

        float momentumBefore = 0;
        float momentumAfter = 0;
        float greatestSeparationSpeed = 0;
        await run.Tick(Idle, 90, () =>
        {
            float total = moving.MomentumX() + resting.MomentumX();
            if (resting.LinearVelocity.X == 0)
            {
                momentumBefore = total;
            }
            else
            {
                if (momentumAfter == 0)
                {
                    momentumAfter = total;
                }
                greatestSeparationSpeed = MathF.Max(
                    greatestSeparationSpeed, resting.LinearVelocity.X - moving.LinearVelocity.X);
            }
        });

        // Drag takes a little under 1% of the momentum each tick.
        AssertThat(momentumAfter).IsBetween(0.98f * momentumBefore, momentumBefore);
        AssertThat(greatestSeparationSpeed).IsLess(0.01f);
        AssertThat(resting.LinearVelocity.X).IsGreater(0f);
        AssertThat(moving.Right()).IsLessEqual(resting.Left() + Slack);
    }

    [TestCase]
    public async Task A_body_is_not_pushed_through_a_wall_or_into_its_pusher()
    {
        RoomRun run = Start(RoomOf(
            "#..........#",
            "#..........#",
            "#....P..B..#",
            "############"));
        Entity block = run.Node.EntitiesOf(EntityKind.Block).Single();
        const float wallFace = 11;

        float deepestInWall = float.MinValue;
        var overlap = new Overlap();
        await run.Tick(Right, 240, () =>
        {
            deepestInWall = MathF.Max(deepestInWall, block.Right() - wallFace);
            overlap.Add(run.Player.Right() - block.Left());
        });

        // A body reaching a wall can sink into it for a tick before the engine pushes it out. Measured:
        // at most 0.007 tiles, over a range of starting positions.
        AssertThat(deepestInWall).IsLess(0.01f);
        AssertThat(overlap.Deepest).IsLess(MaxImpactOverlap);
        AssertThat(overlap.LongestTicks).IsLessEqual(MaxImpactOverlapTicks);

        // The player has pinned the block against the wall, and is still pushing.
        AssertThat(block.Right()).IsLessEqual(wallFace + Slack);
        AssertThat(block.Right()).IsEqualApprox(wallFace, 0.01f);
        AssertThat(run.Player.Right()).IsEqualApprox(block.Left(), 0.01f);
    }

    [TestCase]
    public async Task A_running_player_pushes_a_robot_faster_than_it_walks()
    {
        RoomRun run = Start(RoomOf(
            "#" + new string('.', 98) + "#",
            "#" + new string('.', 98) + "#",
            "#..P....R" + new string('.', 90) + "#",
            new string('#', 100)));
        Entity robot = run.Node.EntitiesOf(EntityKind.Robot).Single();
        float robotTopSpeed = Features.Current.RobotTopSpeed;

        var overlap = new Overlap();
        await run.Tick(Right, 90, () => overlap.Add(run.Player.Right() - robot.Left()));

        AssertThat(robot.LinearVelocity.X).IsGreater(1.5f * robotTopSpeed);
        AssertThat(run.Player.LinearVelocity.X).IsEqualApprox(robot.LinearVelocity.X, 0.01f);
        AssertThat(overlap.Deepest).IsLess(MaxImpactOverlap);
        AssertThat(overlap.LongestTicks).IsLessEqual(MaxImpactOverlapTicks);
    }

    [TestCase]
    public async Task A_robot_that_walks_into_an_idle_player_turns_round()
    {
        RoomRun run = Start(RoomOf(
            "#..............................#",
            "#..............................#",
            "#....R.....P...................#",
            "################################"));
        Entity robot = run.Node.EntitiesOf(EntityKind.Robot).Single();
        float playerStart = run.Player.Position.X;

        var overlap = new Overlap();
        float robotFarthestRight = 0;
        bool turnedRound = false;
        await run.Tick(Idle, 180, () =>
        {
            overlap.Add(robot.Right() - run.Player.Left());
            robotFarthestRight = MathF.Max(robotFarthestRight, robot.Right());
            turnedRound |= robotFarthestRight > run.Player.Left() - 0.01f && robot.LinearVelocity.X < -1;
        });

        AssertThat(robotFarthestRight).IsGreater(run.Player.Left() - 0.01f);
        AssertThat(turnedRound).IsTrue();
        AssertThat(overlap.Deepest).IsLess(MaxImpactOverlap);
        AssertThat(overlap.LongestTicks).IsLessEqual(MaxImpactOverlapTicks);

        // The player's braking grip is far stronger than the robot's drive.
        AssertThat(run.Player.Position.X).IsEqualApprox(playerStart, 0.05f);
    }

    [TestCase]
    public async Task A_block_on_a_moving_platform_is_carried_by_it()
    {
        RoomRun run = Start(RoomOf(
            "#" + new string('.', 38) + "#",
            "#.P" + new string('.', 36) + "#",
            "#########" + new string('.', 30) + "#",
            "#" + new string('.', 19) + "B" + new string('.', 18) + "#",
            "#" + new string('.', 38) + "#",
            "#" + new string('.', 19) + "H" + new string('.', 18) + "#",
            "#" + new string('.', 38) + "#",
            "#" + new string('.', 38) + "#",
            new string('#', 40)));
        Entity platform = run.Node.EntitiesOf(EntityKind.Platform).Single();
        Entity block = run.Node.EntitiesOf(EntityKind.Block).Single();

        float platformStart = platform.Position.X;
        float farthestFromStart = 0;
        float greatestOffset = 0;
        await run.Tick(Idle, 30 * 60, () =>
        {
            farthestFromStart = MathF.Max(farthestFromStart, MathF.Abs(platform.Position.X - platformStart));
            greatestOffset = MathF.Max(greatestOffset, MathF.Abs(block.Position.X - platform.Position.X));
        });

        // The platform has crossed the room, and the block is still standing on it. The block slides a
        // little each time a wall stops the platform dead.
        AssertThat(farthestFromStart).IsGreater(10f);
        AssertThat(block.Bottom()).IsEqualApprox(platform.Top(), 0.01f);
        AssertThat(greatestOffset).IsLess(platform.HalfSize.X);
    }

    [TestCase]
    public async Task The_player_can_push_a_block_and_it_stops_when_let_go()
    {
        RoomRun run = Start(RoomOf(
            "#..............................#",
            "#..............................#",
            "#....P.B.......................#",
            "################################"));
        Entity block = run.Node.EntitiesOf(EntityKind.Block).Single();
        float blockStart = block.Position.X;

        var overlap = new Overlap();
        await run.Tick(Right, 120, () => overlap.Add(run.Player.Right() - block.Left()));
        float pushedTo = block.Position.X;
        await run.Tick(Idle, 60);

        AssertThat(pushedTo - blockStart).IsGreater(1f);
        AssertThat(overlap.Deepest).IsLess(MaxImpactOverlap);
        AssertThat(overlap.LongestTicks).IsLessEqual(MaxImpactOverlapTicks);
        AssertThat(block.LinearVelocity.X).IsEqualApprox(0f, 0.0001f);
    }

    [TestCase]
    public async Task A_jump_from_a_block_is_as_high_as_a_jump_from_tiles_and_leaves_the_block_where_it_was()
    {
        RoomRun run = Start(RoomOf(
            "#..............................#",
            "#..............................#",
            "#..............................#",
            "#..............................#",
            "#..............................#",
            "#....P.........................#",
            "#....B.........................#",
            "################################"));
        Entity block = run.Node.EntitiesOf(EntityKind.Block).Single();
        await run.Tick(Idle, 30);
        float blockStart = block.Position.X;
        AssertThat(run.Player.Support).IsSame(block);

        Telemetry.Reset();
        await run.Tick(Jump);
        await run.Tick(Idle, 120);

        // The shortest jump from tiles clears 2 tiles but not 3.
        AssertThat(Telemetry.Get(Metric.AirRise)!.Value.X).IsBetween(2f, 3f);
        AssertThat(block.Position.X).IsEqualApprox(blockStart, 0.01f);
    }

    public static IEnumerable<(float, int)> BoostTestData =>
        from position in new float[] { -0.3f, -0.4f, -0.5f, -0.6f }
        from ticks in new int[] { 5, 6, 7, 8, 9, 10 }
        select (position, ticks);

    [TestCase]
    [DataPoint(nameof(BoostTestData))]
    public async Task Regression_the_player_cannot_get_a_large_boost_from_running_off_a_block((float, int) data)
    {
        var (position, ticks) = data;

        RoomRun run = Start(RoomOf(
            "#..............................#",
            "#..............................#",
            "#..............................#",
            "#..............................#",
            "#..............................#",
            "#..............................#",
            "#......P.......................#",
            "#......B.......................#",
            "################################"));

        run.Player.Position += new Vector3(position, 0, 0);
        // Let the player settle on the block, drive right for a short time, then jump.
        await run.Tick(Idle, 10);
        await run.Tick(Right, ticks);
        await run.Tick(Jump, 120);

        AssertThat(Telemetry.GetFloat(Metric.AirRise) ?? 1e10).IsLess(5f);
    }
}
