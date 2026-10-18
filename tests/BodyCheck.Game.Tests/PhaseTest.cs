using System.Linq;
using System.Threading.Tasks;

using BodyCheck.Core.Controllers;
using BodyCheck.Core.Entities;
using BodyCheck.Core.WorldData;

using GdUnit4;

using Godot;

using static GdUnit4.Assertions;

namespace BodyCheck.Game.Tests;

/// <summary>
/// The player phasing through a robot.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class PhaseTest
{
    private static readonly PlayerInput PhaseRight = new(1, 0, false, PhaseHeld: true);
    private static readonly PlayerInput PhaseDown = new(0, -1, false, PhaseHeld: true);
    private static readonly PlayerInput PhaseThrough = new(0, 0, false, PhaseHeld: true);

    private RoomRun? _run;

    [AfterTest]
    public void TearDown()
    {
        _run?.Free();
        _run = null;
    }

    /// <summary>Loads a room with the player and a robot standing on the floor, and waits for them to settle.</summary>
    private async Task<(Entity Player, Entity Robot)> Start(params string[] rows)
    {
        // Robots that don't patrol, so that where they are is where they started.
        _run = new RoomRun(RoomRun.RoomOf(rows), controllersEnabled: false);
        await _run.Tick(RoomRun.Idle, 30);
        _run.Node.ControllersEnabled = true;
        return (_run.Player, _run.Node.EntitiesOf(EntityKind.Robot).Single());
    }

    [TestCase]
    public async Task PhaseKeepsTheCenterOfMassAndThrowsTheTargetBack()
    {
        var (player, robot) = await Start(
            "####################",
            "#..................#",
            "#..................#",
            "#..PR..............#",
            "####################");
        PlayerTuning tuning = _run!.Node.PlayerTuning;
        // The player and the robot have the same mass.
        float center = (player.Position.X + robot.Position.X) / 2;

        // The teleport and the impulses take effect in the step after the press.
        await _run.Tick(PhaseRight);
        AssertFloat(player.Position.X).IsEqualApprox(center + tuning.PhaseMinSeparation / 2, 0.5f);
        AssertFloat(robot.Position.X).IsEqualApprox(center - tuning.PhaseMinSeparation / 2, 0.5f);
        AssertFloat(player.LinearVelocity.X).IsEqualApprox(tuning.PhaseSpeed, 0.5f);
        AssertFloat(player.LinearVelocity.Y).IsEqualApprox(0, 0.5f);
        AssertFloat(robot.LinearVelocity.X).IsLess(-1);

        // Held, it doesn't phase again.
        await _run.Tick(PhaseRight, 5);
        AssertFloat(player.LinearVelocity.X).IsGreater(tuning.PhaseSpeed - 1);
    }

    [TestCase]
    public async Task PhaseDownThroughARobotOnTheFloorSwapsThem()
    {
        var (player, robot) = await Start(
            "##########",
            "#........#",
            "#........#",
            "#........#",
            "#........#",
            "#..PR....#",
            "##########");
        float floor = player.Bottom();

        // There is no room below the robot, so the pair slides up until the player fits where the robot stood.
        await _run!.Tick(PhaseDown);
        AssertFloat(player.Bottom()).IsEqualApprox(floor, 0.5f);
        AssertFloat(robot.Bottom()).IsGreater(player.Top());
        AssertFloat(robot.LinearVelocity.Y).IsGreater(1);
    }

    [TestCase]
    public async Task PhaseThroughAWallDoesNothing()
    {
        var (player, robot) = await Start(
            "##########",
            "#........#",
            "#........#",
            "#.PR#....#",
            "##########");
        Vector3 start = player.Position;

        // The player only starts to run.
        await _run!.Tick(PhaseRight, 2);
        AssertFloat(player.Position.DistanceTo(start)).IsLess(0.1f);
        AssertFloat(robot.LinearVelocity.Length()).IsLess(1);
    }

    [TestCase]
    public async Task WithNoDirectionHeldPhaseGoesThroughTheTarget()
    {
        var (player, robot) = await Start(
            "##########",
            "#........#",
            "#........#",
            "#...RP...#",
            "##########");

        await _run!.Tick(PhaseThrough);
        AssertFloat(player.Position.X).IsLess(robot.Position.X);
        AssertFloat(player.LinearVelocity.X).IsLess(0);
    }
}
