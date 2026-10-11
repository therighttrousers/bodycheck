using System.Numerics;

namespace BodyCheck.Core.Tests;

/// <summary>
/// The player riding a patrolling robot, with both controllers running as they do in the game.
/// </summary>
public class RidingTests
{
    private const float Dt = 1f / 60;

    private static readonly PlayerInput Idle = new(0, false);
    private static readonly PlayerInput Jump = new(0, true);

    private readonly World _world;
    private readonly PlayerController _player;
    private readonly RobotController _robot;

    public RidingTests()
    {
        // An empty room 400 wide with its floor top at y = 1: room for a robot to patrol one way for a long time.
        _world = new World(TileGrid.FromText(
            Enumerable.Repeat(new string('.', 400), 12).Append(new string('#', 400)).ToList()));

        var robotTuning = new RobotTuning();
        var playerTuning = new PlayerTuning();
        float robotTop = 1 + 2 * robotTuning.HalfSize.Y;
        _robot = new RobotController(new Vector2(20, 1 + robotTuning.HalfSize.Y), robotTuning);
        _player = new PlayerController(new Vector2(20, robotTop + playerTuning.HalfSize.Y), playerTuning);

        // The same order as the game: the player moves first.
        _world.Bodies.Add(_player.Body);
        _world.Bodies.Add(_robot.Body);
    }

    private void Tick(PlayerInput input, int ticks = 1)
    {
        for (int i = 0; i < ticks; i++)
        {
            _player.Apply(input, _world, Dt);
            _robot.Apply(_world, Dt);
            _world.Step(Dt);
        }
    }

    [Fact]
    public void An_idle_player_stays_where_it_stands_on_a_patrolling_robot()
    {
        Tick(Idle, 30 * 60);

        Assert.True(_robot.Body.Position.X > 100);
        Assert.Same(_robot.Body, _player.Body.Support);
        Assert.Equal(_robot.Body.Position.X, _player.Body.Position.X, 2);
    }

    [Fact]
    public void A_jump_from_a_robot_is_as_high_as_a_jump_from_tiles()
    {
        Tick(Idle, 120);
        float start = _player.Body.Position.Y;
        float apex = start;

        Tick(Jump);
        for (int i = 0; i < 120; i++)
        {
            Tick(Idle);
            apex = MathF.Max(apex, _player.Body.Position.Y);
        }

        // The shortest jump from tiles clears 2 tiles but not 3.
        Assert.InRange(apex - start, 2, 3);
    }

    [Fact]
    public void A_jump_from_a_robot_keeps_the_robots_speed()
    {
        Tick(Idle, 120);
        float robotSpeed = _robot.Body.Velocity.X;

        Tick(Jump);

        Assert.True(robotSpeed > 1);
        Assert.Equal(robotSpeed, _player.Body.Velocity.X, 1);
    }
}
