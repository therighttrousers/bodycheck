using System.Linq;
using System.Threading.Tasks;

using BodyCheck.Core.Controllers;
using BodyCheck.Core.Entities;
using BodyCheck.Core.Simulation;
using BodyCheck.Core.WorldData;

using GdUnit4;

using Godot;

using static GdUnit4.Assertions;

namespace BodyCheck.Game.Tests;

/// <summary>
/// The player phasing through a robot. Phase is aimed while it is held, and made when it is released. Nothing pauses
/// the game here, so the player aims while it runs, except where a test pauses it as the game does.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class PhaseTest
{
    private static readonly PlayerInput AimRight = new(1, 0, false, PhaseHeld: true);
    private static readonly PlayerInput AimDown = new(0, -1, false, PhaseHeld: true);
    private static readonly PlayerInput AimNowhere = new(0, 0, false, PhaseHeld: true);
    private static readonly PlayerInput Down = new(0, -1, false);

    private static SceneTree Tree => (SceneTree)Engine.GetMainLoop();

    private RoomRun? _run;

    [AfterTest]
    public void TearDown()
    {
        Tree.Paused = false;
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

        // The teleport and the impulses take effect in the step after the release.
        await _run.Tick(AimRight);
        await _run.Tick(RoomRun.Right);
        AssertFloat(player.Position.X).IsEqualApprox(center + tuning.PhaseMinSeparation / 2, 0.5f);
        AssertFloat(robot.Position.X).IsEqualApprox(center - tuning.PhaseMinSeparation / 2, 0.5f);
        AssertFloat(player.LinearVelocity.X).IsEqualApprox(tuning.PhaseSpeed, 0.5f);
        AssertFloat(player.LinearVelocity.Y).IsEqualApprox(0, 0.5f);
        AssertFloat(robot.LinearVelocity.X).IsLess(-1);

        // Running on, the player keeps its speed.
        await _run.Tick(RoomRun.Right, 5);
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
        await _run!.Tick(AimDown);
        await _run.Tick(Down);
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
        await _run!.Tick(AimRight);
        await _run.Tick(RoomRun.Right);
        AssertFloat(player.Position.DistanceTo(start)).IsLess(0.1f);
        AssertFloat(robot.LinearVelocity.Length()).IsLess(1);
    }

    [TestCase]
    public async Task PressingPhaseGrabsTheTarget()
    {
        var (player, robot) = await Start(
            "####################",
            "#..................#",
            "#..................#",
            "#.P.......R........#",
            "####################");

        // Run at the robot until it is in reach.
        for (int i = 0; i < 120 && robot.Left() - player.Right() > 0.9f; i++)
        {
            await _run!.Tick(RoomRun.Right);
        }

        // They move together, at the velocity of their center of mass, from the step after the press.
        await _run!.Tick(AimNowhere, 2);
        AssertFloat(player.LinearVelocity.X).IsGreater(1);
        AssertFloat(player.LinearVelocity.X - robot.LinearVelocity.X).IsEqualApprox(0, 0.5f);
    }

    [TestCase]
    public async Task WhileAimingThePlayerDoesntFallAwayFromAHoveringPlatform()
    {
        // The player starts in the air beside the platform, and aims at once.
        _run = new RoomRun(RoomRun.RoomOf(
            "############",
            "#..........#",
            "#P.H.......#",
            "#..........#",
            "#..........#",
            "#..........#",
            "############"));
        Entity player = _run.Player;
        Entity platform = _run.Node.EntitiesOf(EntityKind.Platform).Single();

        await _run.Tick(AimNowhere);
        float height = player.Position.Y - platform.Position.Y;
        await _run.Tick(AimNowhere, 30);
        AssertFloat(player.Position.Y - platform.Position.Y).IsEqualApprox(height, 0.05f);
    }

    [TestCase]
    public async Task ReleasingWithNoDirectionHeldCancels()
    {
        var (player, _) = await Start(
            "##########",
            "#........#",
            "#........#",
            "#...RP...#",
            "##########");
        Vector3 start = player.Position;

        await _run!.Tick(AimNowhere);
        await _run.Tick(RoomRun.Idle, 2);
        AssertFloat(player.Position.DistanceTo(start)).IsLess(0.01f);
        AssertObject(_run.Node.PlayerController.Aim).IsNull();
    }

    [TestCase]
    public async Task AimingWhilePausedPhasesOnTheTickAfterRelease()
    {
        var (player, robot) = await Start(
            "####################",
            "#..................#",
            "#..................#",
            "#..PR..............#",
            "####################");
        WorldNode world = _run!.Node;

        // As the game does: paused while the world asks for it, stepping only the controllers.
        await _run.Tick(AimRight);
        AssertBool(world.PauseRequested).IsTrue();
        Tree.Paused = true;
        Vector3 start = player.Position;
        await _run.PausedTick(AimNowhere, 10);
        await _run.PausedTick(AimRight);
        AssertThat(world.PlayerController.Aim?.Direction).IsEqual(Vector2.Right);
        AssertFloat(player.Position.DistanceTo(start)).IsEqual(0);

        await _run.PausedTick(RoomRun.Right);
        AssertBool(world.PauseRequested).IsFalse();
        Tree.Paused = false;
        await _run.Tick(RoomRun.Right);
        AssertFloat(player.Position.X).IsGreater(robot.Position.X);
        AssertFloat(player.LinearVelocity.X).IsGreater(1);
    }
}
