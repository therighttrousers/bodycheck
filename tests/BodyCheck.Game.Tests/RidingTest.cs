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
/// The player riding a patrolling robot.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class RidingTest
{
    private static readonly PlayerInput Idle = new(0, 0, false);
    private static readonly PlayerInput Jump = new(0, 0, true);

    private WorldNode _world = null!;
    private PlayerInput _input;

    private Entity Player => _world.Player;
    private Entity Robot => _world.EntitiesOf(EntityKind.Robot).Single();

    private static SceneTree Tree => (SceneTree)Engine.GetMainLoop();

    /// <summary>Loads the room and waits for the player to land on the robot.</summary>
    private async Task Start()
    {
        _input = Idle;
        _world = new WorldNode(new World(TestRoomLibrary.RobotRiding), new AssetLibrary()) { PlayerInput = () => _input };
        Tree.Root.AddChild(_world);
        await Tick(Idle, 120);
    }

    [AfterTest]
    public void TearDown() => RoomRun.FreeAndFinalize(_world);

    private async Task Tick(PlayerInput input, int ticks = 1)
    {
        _input = input;
        for (int i = 0; i < ticks; i++)
        {
            await Tree.ToSignal(Tree, SceneTree.SignalName.PhysicsFrame);
        }
    }

    [TestCase]
    public async Task An_idle_player_stays_where_it_stands_on_a_patrolling_robot()
    {
        await Start();
        float offset = Player.Position.X - Robot.Position.X;
        float start = Robot.Position.X;

        await Tick(Idle, 30 * 60);

        AssertThat(Robot.Position.X - start).IsGreater(67f);
        AssertThat(Player.Support).IsSame(Robot);
        AssertThat(Player.Position.X - Robot.Position.X).IsEqualApprox(offset, 0.01f);
    }

    [TestCase]
    public async Task A_jump_from_a_robot_is_as_high_as_a_jump_from_tiles()
    {
        await Start();
        Telemetry.Reset();

        await Tick(Jump);
        await Tick(Idle, 120);

        // The shortest jump from tiles clears 2 tiles but not 3.
        AssertThat(Telemetry.Get(Metric.AirRise)!.Value.X).IsBetween(2f, 3f);
    }

    [TestCase]
    public async Task A_jump_from_a_robot_keeps_the_robots_speed()
    {
        await Start();
        float robotSpeed = Robot.LinearVelocity.X;

        await Tick(Jump);
        await Tick(Idle, 2);

        AssertThat(robotSpeed).IsGreater(1f);
        AssertThat(Player.LinearVelocity.X).IsEqualApprox(robotSpeed, 0.1f);
    }
}
