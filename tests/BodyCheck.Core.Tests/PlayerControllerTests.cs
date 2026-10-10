using System.Numerics;

namespace BodyCheck.Core.Tests;

public class PlayerControllerTests
{
    private const float Dt = 1f / 60;

    private static readonly PlayerInput Idle = new(0, false);
    private static readonly PlayerInput Right = new(1, false);
    private static readonly PlayerInput Jump = new(0, true);

    // Most tests use the default tuning. These isolate one mechanism from the others.
    private static readonly PlayerTuning NoDrag = new() { Drag = 0 };
    private static readonly PlayerTuning NoDragFullThrust = new() { Drag = 0, JumpThrustFrac = 1 };

    private readonly World _world;
    private readonly PlayerTuning _tuning;
    private readonly PlayerController _player;

    public PlayerControllerTests() : this(new PlayerTuning())
    {
    }

    private PlayerControllerTests(PlayerTuning tuning, int ceilingHeight = 20)
    {
        // An empty room 60 wide with its floor top at y = 1.
        var rows = Enumerable.Repeat(new string('.', 60), ceilingHeight).Append(new string('#', 60));
        _world = new World(TileGrid.FromText(rows.ToList()));
        _tuning = tuning;
        _player = new PlayerController(new Vector2(5, 1 + _tuning.HalfSize.Y), _tuning);
        _world.Bodies.Add(_player.Body);
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
            apex = MathF.Max(apex, _player.Body.Position.Y - _tuning.HalfSize.Y - 1);
            if (_player.Body.OnGround)
            {
                return apex;
            }
        }
        throw new InvalidOperationException("The body never landed.");
    }

    [Fact]
    public void Running_settles_at_the_speed_where_drive_balances_drag()
    {
        float drive = _tuning.GroundForwardDriveForce;
        float balance = drive / (drive / _tuning.TopSpeed + _tuning.Drag);

        Tick(Right, 120);

        Assert.Equal(balance, _player.Body.Velocity.X, 3);
    }

    [Fact]
    public void Running_without_drag_approaches_top_speed_without_passing_it()
    {
        var scene = new PlayerControllerTests(NoDrag);

        scene.Tick(Right, 120);

        Assert.InRange(scene._player.Body.Velocity.X, 0.99f * NoDrag.TopSpeed, NoDrag.TopSpeed);
    }

    [Fact]
    public void Running_reaches_most_of_top_speed_quickly()
    {
        Tick(Right, 15);

        Assert.InRange(_player.Body.Velocity.X, 0.8f * _tuning.TopSpeed, float.MaxValue);
    }

    [Fact]
    public void Driving_does_not_slow_a_body_that_is_over_top_speed()
    {
        var scene = new PlayerControllerTests(NoDrag);
        scene._player.Body.ApplyImpulse(new Vector2(20, 0) * scene._player.Body.Mass);

        scene.Tick(Right, 30);

        Assert.Equal(20, scene._player.Body.Velocity.X);
    }

    [Fact]
    public void Friction_stops_the_body_without_reversing_it()
    {
        _player.Body.ApplyImpulse(new Vector2(5, 0));

        Tick(Idle, 30);

        Assert.Equal(0, _player.Body.Velocity.X);
    }

    [Fact]
    public void There_is_no_friction_in_the_air()
    {
        var scene = new PlayerControllerTests(NoDrag);
        scene._player.Teleport(new Vector2(5, 15), Vector2.Zero);
        scene.Tick(Idle);
        scene._player.Body.ApplyImpulse(new Vector2(5, 0));

        scene.Tick(Idle, 10);

        Assert.Equal(5, scene._player.Body.Velocity.X);
    }

    [Fact]
    public void Jumping_gives_an_upward_impulse()
    {
        var world = new World(_world.Tiles);
        var player = new PlayerController(_player.Body.Position, new PlayerTuning() { Mass = 3 });
        world.Bodies.Add(player.Body);
        world.Step(Dt);

        player.Apply(Jump, world, Dt);

        Assert.Equal(new Vector2(0, 3 * _tuning.JumpSpeed), player.Body.Momentum);
    }

    [Fact]
    public void Thrust_equal_to_weight_holds_the_rise_at_constant_speed()
    {
        var scene = new PlayerControllerTests(NoDragFullThrust);

        scene.Tick(Jump, NoDragFullThrust.JumpThrustTicks);

        Assert.Equal(NoDragFullThrust.JumpSpeed, scene._player.Body.Velocity.Y, 4);
    }

    [Fact]
    public void Thrust_below_weight_slows_the_rise_less_than_gravity_alone()
    {
        float lostToGravityAlone = -_world.Gravity.Y * _tuning.JumpThrustTicks * Dt;

        Tick(Jump, _tuning.JumpThrustTicks);

        Assert.InRange(_player.Body.Velocity.Y, _tuning.JumpSpeed - lostToGravityAlone, _tuning.JumpSpeed);
    }

    [Fact]
    public void A_tap_without_drag_jumps_about_as_high_as_the_impulse_alone()
    {
        var scene = new PlayerControllerTests(NoDrag);
        float impulseOnly = NoDrag.JumpSpeed * NoDrag.JumpSpeed / (2 * -scene._world.Gravity.Y);

        float apex = scene.ApexOfJump(ticksHeld: 1);

        Assert.InRange(apex, impulseOnly - 0.1f, impulseOnly + 0.2f);
    }

    [Fact]
    public void Holding_jump_adds_the_distance_risen_under_thrust()
    {
        float tap = new PlayerControllerTests(NoDragFullThrust).ApexOfJump(ticksHeld: 1);
        // A tap already gets the first tick of thrust.
        float risenUnderThrust = (NoDragFullThrust.JumpThrustTicks - 1) * NoDragFullThrust.JumpSpeed * Dt;

        float held = new PlayerControllerTests(NoDragFullThrust).ApexOfJump(ticksHeld: 600);

        Assert.Equal(risenUnderThrust, held - tap, 3);
    }

    // The next two tests check design goals for the default tuning: a jump reaches a ledge
    // that many tiles up, but not one a tile higher.

    [Fact]
    public void The_shortest_jump_clears_2_tiles_but_not_3()
    {
        float apex = ApexOfJump(ticksHeld: 1);

        Assert.InRange(apex, 2, 3);
    }

    [Fact]
    public void The_tallest_jump_clears_4_tiles_but_not_5()
    {
        float apex = ApexOfJump(ticksHeld: 600);

        Assert.InRange(apex, 4, 5);
    }

    [Fact]
    public void Holding_jump_after_landing_does_not_jump_again()
    {
        ApexOfJump(ticksHeld: 600);

        Tick(Jump, 10);

        Assert.True(_player.Body.OnGround);
    }

    [Fact]
    public void Jump_does_nothing_in_the_air()
    {
        _player.Teleport(new Vector2(5, 15), new Vector2(0, 0));
        Tick(Idle);
        float before = _player.Body.Velocity.Y;

        Tick(Jump);

        Assert.InRange(_player.Body.Velocity.Y, float.MinValue, before);
    }

    [Fact]
    public void Thrust_ends_when_the_body_hits_a_ceiling()
    {
        var low = new PlayerControllerTests(new PlayerTuning(), ceilingHeight: 2);

        low.Tick(Jump, 40);

        Assert.True(low._player.Body.OnGround);
    }
}
