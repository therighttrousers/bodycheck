using System;
using System.Threading.Tasks;

using GdUnit4;

using Godot;

using static GdUnit4.Assertions;

namespace BodyCheck.Spike.Tests;

/// <summary>
/// The player riding a patrolling robot, as Core's RidingTests has it, but simulated by the engine.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class RidingTest
{
    private static readonly JoltPlayerInput Idle = new(0, false);
    private static readonly JoltPlayerInput Jump = new(0, true);

    private JoltSpike _scene = null!;
    private JoltPlayerInput _input;

    private JoltBody Player => _scene.Player;
    private JoltBody Robot => _scene.Controllers[0].Body;

    private static SceneTree Tree => (SceneTree)Engine.GetMainLoop();

    /// <summary>Loads the room and waits for the player to land on the robot.</summary>
    private async Task Start()
    {
        _input = Idle;
        _scene = new JoltSpike { RoomOverride = JoltSpike.RobotRidingRoom, InputOverride = () => _input };
        Tree.Root.AddChild(_scene);
        await Tick(Idle, 120);
    }

    [AfterTest]
    public void TearDown() => _scene.Free();

    private async Task Tick(JoltPlayerInput input, int ticks = 1)
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

        AssertThat(Robot.Position.X - start).IsGreater(90f);
        AssertThat(Player.Support).IsSame(Robot);
        AssertThat(Player.Position.X - Robot.Position.X).IsEqualApprox(offset, 0.01f);
    }

    [TestCase]
    public async Task A_jump_from_a_robot_is_as_high_as_a_jump_from_tiles()
    {
        await Start();
        float start = Player.Position.Y;
        float apex = start;

        await Tick(Jump);
        for (int i = 0; i < 120; i++)
        {
            await Tick(Idle);
            apex = MathF.Max(apex, Player.Position.Y);
        }

        // The shortest jump from tiles clears 2 tiles but not 3.
        AssertThat(apex - start).IsBetween(2f, 3f);
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
