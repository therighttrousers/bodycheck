using System.Numerics;

namespace BodyCheck.Core.Tests;

public class PlayerControllerTests
{
    private const float Dt = 1f / 60;
    private static readonly Vector2 HalfSize = new(0.4f, 0.45f);
    private static readonly PlayerTuning Tuning = new();

    private static readonly PlayerInput Idle = new(0, false);
    private static readonly PlayerInput Right = new(1, false);
    private static readonly PlayerInput Jump = new(0, true);

    private readonly World _world;
    private readonly Body _body;
    private readonly PlayerController _player;

    public PlayerControllerTests() : this(ceilingHeight: 20)
    {
    }

    private PlayerControllerTests(int ceilingHeight)
    {
        // An empty room 60 wide with its floor top at y = 1.
        var rows = Enumerable.Repeat(new string('.', 60), ceilingHeight).Append(new string('#', 60));
        _world = new World(TileGrid.FromText(rows.ToList()));
        _body = new Body(new Vector2(5, 1 + HalfSize.Y), HalfSize, mass: 1);
        _world.Bodies.Add(_body);
        _player = new PlayerController(_body, Tuning);
        Tick(Idle);
    }

    private void Tick(PlayerInput input, int ticks = 1)
    {
        for (int i = 0; i < ticks; i++)
        {
            _player.Apply(input, _world, Dt);
            _world.Step(Dt);
        }
    }

    /// <summary>Runs until the body lands, and returns the greatest height its feet reached above the floor.</summary>
    private float ApexOfJump(int ticksHeld)
    {
        float apex = 0;
        for (int tick = 0; tick < 600; tick++)
        {
            Tick(tick < ticksHeld ? Jump : Idle);
            apex = MathF.Max(apex, _body.Position.Y - HalfSize.Y - 1);
            if (_body.OnGround)
            {
                return apex;
            }
        }
        throw new InvalidOperationException("The body never landed.");
    }

    [Fact]
    public void Running_approaches_top_speed_without_passing_it()
    {
        Tick(Right, 120);

        Assert.InRange(_body.Velocity.X, 0.95f * Tuning.TopSpeed, Tuning.TopSpeed);
    }

    [Fact]
    public void Running_reaches_most_of_top_speed_quickly()
    {
        Tick(Right, 12);

        Assert.True(_body.Velocity.X > 0.8f * Tuning.TopSpeed);
    }

    [Fact]
    public void Driving_does_not_slow_a_body_that_is_over_top_speed()
    {
        _body.ApplyImpulse(new Vector2(20, 0));

        Tick(Right, 30);

        Assert.Equal(20, _body.Velocity.X);
    }

    [Fact]
    public void Friction_stops_the_body_without_reversing_it()
    {
        _body.ApplyImpulse(new Vector2(5, 0));

        Tick(Idle, 30);

        Assert.Equal(0, _body.Velocity.X);
    }

    [Fact]
    public void There_is_no_friction_in_the_air()
    {
        _body.Position = new Vector2(5, 15);
        Tick(Idle);
        _body.ApplyImpulse(new Vector2(5, 0));

        Tick(Idle, 10);

        Assert.Equal(5, _body.Velocity.X);
    }

    [Fact]
    public void Jumping_gives_an_upward_impulse()
    {
        var world = new World(_world.Tiles);
        var body = new Body(_body.Position, HalfSize, mass: 3);
        world.Bodies.Add(body);
        var player = new PlayerController(body, Tuning);
        world.Step(Dt);

        player.Apply(Jump, world, Dt);

        Assert.Equal(new Vector2(0, 3 * Tuning.JumpSpeed), body.Momentum);
    }

    [Fact]
    public void Holding_jump_rises_at_constant_speed_while_the_thrust_lasts()
    {
        Tick(Jump, 6);

        Assert.Equal(Tuning.JumpSpeed, _body.Velocity.Y, 4);
    }

    [Fact]
    public void A_tap_jumps_about_as_high_as_the_impulse_alone()
    {
        float impulseOnly = Tuning.JumpSpeed * Tuning.JumpSpeed / (2 * -_world.Gravity.Y);

        float apex = ApexOfJump(ticksHeld: 1);

        Assert.InRange(apex, impulseOnly - 0.1f, impulseOnly + 0.2f);
    }

    [Fact]
    public void Holding_jump_adds_the_distance_risen_under_thrust()
    {
        float tap = new PlayerControllerTests().ApexOfJump(ticksHeld: 1);

        float held = ApexOfJump(ticksHeld: 600);

        Assert.InRange(held - tap, 0.9f * Tuning.JumpSpeed * Tuning.JumpHoldTime, Tuning.JumpSpeed * Tuning.JumpHoldTime);
    }

    [Fact]
    public void Holding_jump_after_landing_does_not_jump_again()
    {
        ApexOfJump(ticksHeld: 600);

        Tick(Jump, 10);

        Assert.True(_body.OnGround);
    }

    [Fact]
    public void Jump_does_nothing_in_the_air()
    {
        _body.Position = new Vector2(5, 15);
        Tick(Idle);
        float before = _body.Velocity.Y;

        Tick(Jump);

        Assert.True(_body.Velocity.Y < before);
    }

    [Fact]
    public void Thrust_ends_when_the_body_hits_a_ceiling()
    {
        var low = new PlayerControllerTests(ceilingHeight: 2);

        low.Tick(Jump, 40);

        Assert.True(low._body.OnGround);
    }
}
