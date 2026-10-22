using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using BodyCheck.Core;
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
        // The player and the robot have the same mass.
        float center = (player.Position.X + robot.Position.X) / 2;

        // The teleport and the impulses take effect in the step after the release.
        await _run!.Tick(AimRight);
        await _run.Tick(RoomRun.Right);
        float separation = player.HalfSize.X + robot.HalfSize.X + Features.Current.PlayerPhaseGap;
        AssertFloat(player.Position.X).IsEqualApprox(center + separation / 2, 0.5f);
        AssertFloat(robot.Position.X).IsEqualApprox(center - separation / 2, 0.5f);
        AssertFloat(player.LinearVelocity.X).IsEqualApprox(Features.Current.PlayerPhaseSpeed, 0.5f);
        AssertFloat(player.LinearVelocity.Y).IsEqualApprox(0, 0.5f);
        AssertFloat(robot.LinearVelocity.X).IsLess(-1);

        // Running on, the player keeps its speed.
        await _run.Tick(RoomRun.Right, 5);
        AssertFloat(player.LinearVelocity.X).IsGreater(Features.Current.PlayerPhaseSpeed - 1);
    }

    [TestCase]
    public async Task PhaseDownThroughARobotOnTheFloorSwapsThem()
    {
        var (player, robot) = await Start(
            "##########",
            "#........#",
            "#........#",
            "#........#",
            "#...P....#",
            "#...R....#",
            "##########");
        float floor = player.Bottom();

        await _run!.Tick(AimDown);
        await _run.Tick(Down, 10);
        // There is no room below the robot, so the player ends up on the floor.
        AssertFloat(player.Bottom()).IsEqualApprox(floor, 0.5f);
        // The robot is thrown above the player.
        AssertFloat(robot.Bottom()).IsGreater(player.Top());
        AssertFloat(robot.LinearVelocity.Y).IsGreater(1);
    }

    [TestCase]
    public async Task PhaseWithTargetNextToAWallSucceeds()
    {
        var (player, robot) = await Start(
            "##########",
            "#........#",
            "#........#",
            "#PR#.....#",
            "##########");
        Vector3 playerStart = player.Position;
        Vector3 robotStart = robot.Position;

        await _run!.Tick(AimRight);
        await _run.Tick(RoomRun.Right);
        AssertFloat(player.Position.X).IsEqualApprox(robotStart.X, 0.1f);
        AssertFloat(robot.Position.X).IsEqualApprox(playerStart.X, 0.1f);
    }

    [TestCase]
    public async Task HoldingPhaseGrabsATargetThatComesInReach()
    {
        var (player, robot) = await Start(
            "####################",
            "#..................#",
            "#..................#",
            "#.P.......R........#",
            "####################");
        PlayerController controller = _run!.Node.PlayerController;

        // Run at the robot holding phase, until the aim begins.
        for (int i = 0; i < 120 && controller.Phase is null; i++)
        {
            await _run.Tick(AimRight);
        }
        AssertObject(controller.Phase?.Target).IsSame(robot);

        // They move together, at the velocity of their center of mass, from the step after the grab.
        await _run.Tick(AimRight);
        AssertFloat(player.LinearVelocity.X).IsGreater(1);
        AssertFloat(player.LinearVelocity.X - robot.LinearVelocity.X).IsEqualApprox(0, 0.5f);
    }

    [TestCase]
    public async Task RunningAtStackedRobotsGrabsTheBottomOne()
    {
        // The top robot is behind the bottom one, and lags further as they patrol, so its corner reaches the
        // player's head early. Reach is a sphere around the player's center, which the bottom robot enters first.
        _run = new RoomRun(RoomRun.RoomOf(
            "############",
            "#..........#",
            "#......R...#",
            "#..........#",
            "#.P....R...#",
            "############"));
        Entity top = _run.Node.EntitiesOf(EntityKind.Robot).MaxBy(robot => robot.Position.Y)!;
        Entity bottom = _run.Node.EntitiesOf(EntityKind.Robot).MinBy(robot => robot.Position.Y)!;
        top.Position += new Vector3(-0.1f, 0, 0);
        PlayerController controller = _run.Node.PlayerController;
        await _run.Tick(RoomRun.Idle, 30);

        for (int i = 0; i < 120 && controller.Phase is null; i++)
        {
            await _run.Tick(AimRight);
        }
        AssertObject(controller.Phase?.Target).IsSame(bottom);
    }

    [TestCase]
    public async Task PhaseThroughABlockOnTheFloor()
    {
        // A block is shorter than the player, so the two can't both stand on the floor with their centers level.
        _run = new RoomRun(RoomRun.RoomOf(
            "############",
            "#..........#",
            "#..........#",
            "#..PB......#",
            "############"));
        Entity player = _run.Player;
        Entity block = _run.Node.EntitiesOf(EntityKind.Block).Single();
        await _run.Tick(RoomRun.Idle, 30);

        await _run.Tick(AimRight);
        await _run.Tick(RoomRun.Right);
        AssertFloat(player.Position.X).IsGreater(block.Position.X);
    }

    [TestCase]
    public async Task ARobotPastABumpIsOutOfReach()
    {
        // Reach is 0.75 tiles past the player's side, and a tile is 1 wide.
        _run = new RoomRun(RoomRun.RoomOf(
            "############",
            "#..........#",
            "#..........#",
            "#..P#R.....#",
            "############"), controllersEnabled: false);
        await _run.Tick(RoomRun.Idle, 30);
        _run.Node.ControllersEnabled = true;

        await _run.Tick(AimRight, 5);
        AssertObject(_run.Node.PlayerController.Phase).IsNull();
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
    public async Task PhaseRaisesCues()
    {
        var (player, _) = await Start(
            "##########",
            "#........#",
            "#........#",
            "#...PR...#",
            "##########");
        var cues = new List<EntityCue>();
        player.CueRaised += cues.Add;

        await _run!.Tick(AimRight, 2);
        await _run.Tick(RoomRun.Right);
        AssertThat(cues).IsEqual(new List<EntityCue> { EntityCue.PhaseBegan, EntityCue.PhaseThrown });
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
        AssertObject(_run.Node.PlayerController.Phase).IsNull();
    }

    /// <summary>
    /// Letting go of the direction the given number of ticks before releasing phase, while the game runs or while it is
    /// paused: within <see cref="Features.PlayerPhaseReleaseTicks"/>, the phase is made in that direction.
    /// </summary>
    [TestCase(0, false)]
    [TestCase(0, true)]
    [TestCase(3, false)]
    [TestCase(3, true)]
    public async Task LettingGoOfTheDirectionJustBeforeReleasingStillPhases(int ticksBefore, bool paused)
    {
        var (player, robot) = await Start(
            "####################",
            "#..................#",
            "#..................#",
            "#..PR..............#",
            "####################");
        AssertInt(ticksBefore).IsLess(Features.Current.PlayerPhaseReleaseTicks);

        await _run!.Tick(AimRight);
        Tree.Paused = paused;
        Task Tick(PlayerInput input, int ticks) => paused ? _run.PausedTick(input, ticks) : _run.Tick(input, ticks);
        await Tick(AimNowhere, ticksBefore);
        await Tick(RoomRun.Idle, 1);
        Tree.Paused = false;
        await _run.Tick(RoomRun.Idle);

        AssertFloat(player.Position.X).IsGreater(robot.Position.X);
    }

    [TestCase]
    public async Task LettingGoOfTheDirectionLongerBeforeReleasingCancels()
    {
        var (player, robot) = await Start(
            "####################",
            "#..................#",
            "#..................#",
            "#..PR..............#",
            "####################");

        await _run!.Tick(AimRight);
        await _run.Tick(AimNowhere, Features.Current.PlayerPhaseReleaseTicks);
        await _run.Tick(RoomRun.Idle, 2);

        AssertFloat(player.Position.X).IsLess(robot.Position.X);
        AssertObject(_run.Node.PlayerController.Phase).IsNull();
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
        AssertThat(world.PlayerController.Phase?.Direction).IsEqual(Vector3.Right);
        AssertFloat(player.Position.DistanceTo(start)).IsEqual(0);

        await _run.PausedTick(RoomRun.Right);
        AssertBool(world.PauseRequested).IsFalse();
        AssertFloat(player.Position.DistanceTo(start)).IsEqual(0);
        Tree.Paused = false;
        await _run.Tick(RoomRun.Right);
        AssertFloat(player.Position.X).IsGreater(robot.Position.X);
        AssertFloat(player.LinearVelocity.X).IsGreater(1);
    }

    [TestCase]
    public async Task ALongPauseDoesntChangeThePhase()
    {
        // In the air beside a hovering platform, where the grab has drift to take back.
        _run = new RoomRun(RoomRun.RoomOf(
            "############",
            "#..........#",
            "#..........#",
            "#P.H.......#",
            "#..........#",
            "#..........#",
            "############"));
        Entity player = _run.Player;
        var aimUp = new PlayerInput(0, 1, false, PhaseHeld: true);
        await _run.Tick(aimUp, 30);

        Tree.Paused = true;
        await _run.PausedTick(aimUp, 120);
        await _run.PausedTick(RoomRun.Idle);
        Tree.Paused = false;
        await _run.Tick(RoomRun.Idle);

        AssertFloat(player.LinearVelocity.X).IsEqualApprox(0, 0.5f);
        AssertFloat(player.LinearVelocity.Y).IsEqualApprox(Features.Current.PlayerPhaseSpeed, 0.5f);
    }

    [TestCase]
    public async Task JumpRightAfterAPausedPhaseDoesNothing()
    {
        var (player, _) = await Start(
            "####################",
            "#..................#",
            "#..................#",
            "#..PR..............#",
            "####################");

        // Released while paused, and jump pressed on the first tick of play, when the contacts are still those of
        // the floor where the player was.
        await _run!.Tick(AimRight);
        Tree.Paused = true;
        await _run.PausedTick(RoomRun.Right);
        Tree.Paused = false;
        await _run.Tick(new PlayerInput(1, 0, true));

        AssertFloat(player.LinearVelocity.Y).IsLess(1);
    }
}
