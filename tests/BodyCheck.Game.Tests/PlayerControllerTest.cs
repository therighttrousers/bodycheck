using System;
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
/// The player running and jumping in an empty room.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class PlayerControllerTest
{
    private const float Dt = 1f / 60;

    private static readonly PlayerInput Idle = new(0, 0, false);
    private static readonly PlayerInput Right = new(1, 0, false);
    private static readonly PlayerInput Jump = new(0, 0, true);

    private static readonly BodyTuning NoDrag = BodyTuning.Player with { Drag = 0 };
    private static readonly Features FullThrust = new() { PlayerJumpThrustFrac = 1 };

    private WorldNode? _world;
    private PlayerInput _input;

    private static readonly float Gravity = (float)ProjectSettings.GetSetting("physics/3d/default_gravity");

    private Entity Player => _world!.Player;
    private Vector3 Velocity => Player.LinearVelocity;

    private static SceneTree Tree => (SceneTree)Engine.GetMainLoop();

    /// <summary>
    /// Puts the given features in effect, or the defaults, and loads the given room, or an empty one, replacing the one
    /// loaded before. By default waits for the player to settle.
    /// </summary>
    /// <param name="settleTicks">How long to wait before returning. Pass 0 for a player that starts in the air.</param>
    private async Task Start(
        Features? features = null, BodyTuning? body = null,
        int height = 20, int playerX = 1, int playerY = 1, int settleTicks = 30, Room? room = null)
    {
        _world?.Free();
        Features.Set(features ?? new());
        _input = Idle;
        Room frame = room ?? TestRoomLibrary.EmptyFrame(60, height, playerX, playerY);
        _world = new WorldNode(new World(frame), new AssetLibrary(), playerBody: body)
        {
            PlayerInput = () => _input,
        };
        Tree.Root.AddChild(_world);
        await Tick(Idle, settleTicks);
    }

    [AfterTest]
    public void TearDown()
    {
        if (_world is not null)
        {
            RoomRun.FreeAndFinalize(_world);
        }
        _world = null;
        Features.Reset();
    }

    private async Task Tick(PlayerInput input, int ticks = 1)
    {
        _input = input;
        for (int i = 0; i < ticks; i++)
        {
            await Tree.ToSignal(Tree, SceneTree.SignalName.PhysicsFrame);
        }
    }

    /// <summary>Jumps, holding jump for a number of ticks, and returns how high the feet rose, as reported on landing.</summary>
    private async Task<float> ApexOfJump(int ticksHeld)
    {
        Telemetry.Reset();
        await Tick(Jump, ticksHeld);
        await Tick(Idle, 120);
        return Telemetry.Get(Metric.AirRise)!.Value.X;
    }

    [TestCase]
    public async Task Running_settles_at_the_speed_where_drive_balances_drag()
    {
        await Start();
        float drive = Features.Current.PlayerGroundDriveForce;
        float balance = drive / (drive / Features.Current.PlayerTopSpeed + Player.Tuning.Drag);

        await Tick(Right, 120);

        AssertThat(Velocity.X).IsEqualApprox(balance, 0.001f);
    }

    [TestCase]
    public async Task Running_without_drag_approaches_top_speed_without_passing_it()
    {
        await Start(body: NoDrag);

        await Tick(Right, 120);

        AssertThat(Velocity.X).IsBetween(0.99f * Features.Current.PlayerTopSpeed, Features.Current.PlayerTopSpeed);
    }

    [TestCase]
    public async Task Running_reaches_most_of_top_speed_quickly()
    {
        await Start();

        await Tick(Right, 20);

        AssertThat(Velocity.X).IsGreater(0.8f * Features.Current.PlayerTopSpeed);
    }

    [TestCase]
    public async Task Driving_does_not_slow_a_body_that_is_over_top_speed()
    {
        // Without ground friction, nothing brakes the push while the controller is off.
        await Start(new() { PlayerGroundFriction = 0 }, body: NoDrag);

        // A controller sees an impulse only after the engine's next step, so the push gets a tick of its own.
        _world!.ControllersEnabled = false;
        Player.ApplyCentralImpulse(new Vector3(20, 0, 0) * Player.Mass);
        await Tick(Idle);
        _world!.ControllersEnabled = true;

        await Tick(Right, 30);

        AssertThat(Velocity.X).IsEqualApprox(20f, 0.001f);
    }

    [TestCase]
    public async Task A_disabled_player_does_not_drive()
    {
        await Start();
        float startX = Player.Position.X;

        // Kept active, so that its body stays in the engine, and only what steps it can stop it.
        Player.DisableMode = CollisionObject3D.DisableModeEnum.KeepActive;
        Player.ProcessMode = Node.ProcessModeEnum.Disabled;
        await Tick(Right, 30);

        AssertThat(Player.Position.X).IsEqualApprox(startX, 0.001f);
    }

    [TestCase]
    public async Task Friction_stops_the_body_without_reversing_it()
    {
        await Start();
        Player.ApplyCentralImpulse(new Vector3(5, 0, 0));

        await Tick(Idle, 30);

        AssertThat(Velocity.X).IsEqual(0f);
    }

    [TestCase]
    public async Task There_is_no_friction_in_the_air()
    {
        await Start(body: NoDrag, playerX: 5, playerY: 15, settleTicks: 0);
        Player.ApplyCentralImpulse(new Vector3(5, 0, 0));

        await Tick(Idle, 10);

        AssertThat(Player.OnGround).IsFalse();
        AssertThat(Velocity.X).IsEqualApprox(5f, 0.001f);
    }

    [TestCase]
    public async Task Jumping_gives_an_upward_impulse()
    {
        await Start(body: BodyTuning.Player with { Mass = 3 });

        await Tick(Jump);

        // The engine's step has taken one tick of gravity off the jump speed.
        AssertThat(Velocity.Y).IsEqualApprox(Features.Current.PlayerJumpSpeed - Gravity * Dt, 0.001f);
    }

    [TestCase]
    public async Task Thrust_equal_to_weight_holds_the_rise_at_constant_speed()
    {
        await Start(FullThrust, NoDrag);

        await Tick(Jump);
        float speedAfterImpulse = Velocity.Y;
        await Tick(Jump, Features.Current.PlayerJumpThrustTicks);

        AssertThat(Velocity.Y).IsEqualApprox(speedAfterImpulse, 0.001f);
    }

    [TestCase]
    public async Task Thrust_below_weight_slows_the_rise_less_than_gravity_alone()
    {
        await Start();
        float lostToGravityAlone = Gravity * Features.Current.PlayerJumpThrustTicks * Dt;

        await Tick(Jump, Features.Current.PlayerJumpThrustTicks);

        AssertThat(Velocity.Y).IsBetween(Features.Current.PlayerJumpSpeed - lostToGravityAlone, Features.Current.PlayerJumpSpeed);
    }

    [TestCase]
    public async Task A_tap_without_drag_jumps_about_as_high_as_the_impulse_alone()
    {
        await Start(body: NoDrag);
        float impulseOnly = Features.Current.PlayerJumpSpeed * Features.Current.PlayerJumpSpeed / (2 * Gravity);

        float apex = await ApexOfJump(ticksHeld: 1);

        AssertThat(apex).IsBetween(impulseOnly - 0.2f, impulseOnly + 0.2f);
    }

    [TestCase]
    public async Task Holding_jump_adds_the_distance_risen_under_thrust()
    {
        await Start(FullThrust, NoDrag);
        float tap = await ApexOfJump(ticksHeld: 1);

        // Thrust starts the tick after the impulse, by when gravity has acted once. A tap gets none.
        float risenUnderThrust = Features.Current.PlayerJumpThrustTicks * (Features.Current.PlayerJumpSpeed - Gravity * Dt) * Dt;

        await Start(FullThrust, NoDrag);
        float held = await ApexOfJump(ticksHeld: 600);

        AssertThat(held - tap).IsEqualApprox(risenUnderThrust, 0.01f);
    }

    // The next two tests check design goals for the default tuning: a jump reaches a ledge
    // that many tiles up, but not one a tile higher.

    [TestCase]
    public async Task The_shortest_jump_clears_2_tiles_but_not_3()
    {
        await Start();

        float apex = await ApexOfJump(ticksHeld: 1);

        AssertThat(apex).IsBetween(2f, 3f);
    }

    [TestCase]
    public async Task The_tallest_jump_clears_4_tiles_but_not_5()
    {
        await Start();

        float apex = await ApexOfJump(ticksHeld: 600);

        AssertThat(apex).IsBetween(4f, 5f);
    }

    [TestCase]
    public async Task A_jump_at_full_running_speed_rises_at_about_45_degrees_while_thrust_lasts()
    {
        await Start();
        await Tick(Right, 120);

        // The take-off tick, then the thrust ticks.
        float steepest = float.MinValue;
        float shallowest = float.MaxValue;
        for (int tick = 0; tick <= Features.Current.PlayerJumpThrustTicks; tick++)
        {
            await Tick(new PlayerInput(1, 0, true));
            float angle = Mathf.RadToDeg(MathF.Atan2(Velocity.Y, Velocity.X));
            steepest = MathF.Max(steepest, angle);
            shallowest = MathF.Min(shallowest, angle);
        }

        // Thrust slows the rise less than gravity alone would, so the path stays close to straight.
        // Measured: 45.7 degrees at take-off, falling to 42.6 when thrust ends.
        AssertThat(steepest).IsBetween(42f, 48f);
        AssertThat(shallowest).IsBetween(42f, 48f);
    }

    [TestCase]
    public async Task Holding_jump_after_landing_does_not_jump_again()
    {
        await Start();
        Telemetry.Reset();
        await Tick(Jump, 120);
        AssertThat(Telemetry.Get(Metric.AirTime)).IsNotNull(); // It has landed.

        await Tick(Jump, 10);

        AssertThat(Player.OnGround).IsTrue();
        AssertThat(Velocity.Y).IsEqual(0f);
    }

    [TestCase]
    public async Task Jump_does_nothing_in_the_air()
    {
        await Start(playerX: 5, playerY: 15, settleTicks: 0);
        await Tick(Idle, 2);
        float before = Velocity.Y;

        await Tick(Jump, 2);

        AssertThat(Player.OnGround).IsFalse();
        AssertThat(Velocity.Y).IsLess(before);
    }

    /// <summary>
    /// Running off a ledge, the player can jump until the last coyote tick, counted from the first in the air, but not
    /// on the tick after.
    /// </summary>
    [TestCase(false)]
    [TestCase(true)]
    public async Task Coyote_time_lasts_exactly_its_ticks(bool oneTickLate)
    {
        await Start(room: RoomRun.RoomOf(
            "##########",
            "#........#",
            "#........#",
            "#........#",
            "#P.......#",
            "####.....#",
            "####.....#",
            "##########"));
        for (int i = 0; i < 120 && Player.OnGround; i++)
        {
            await Tick(Right);
        }
        AssertThat(Player.OnGround).IsFalse();

        int airTicks = Features.Current.PlayerCoyoteTicks + (oneTickLate ? 1 : 0);
        await Tick(Right, airTicks - 1);
        await Tick(new PlayerInput(1, 0, true));

        // Rising after a jump, falling without one.
        AssertThat(Velocity.Y > 0).IsEqual(!oneTickLate);
    }

    /// <summary>
    /// Falling, a tap of jump jumps on landing if the landing is within the buffer ticks, counted from the tap, but not
    /// if the tap is one tick earlier.
    /// </summary>
    [TestCase(false)]
    [TestCase(true)]
    public async Task Jump_buffer_lasts_exactly_its_ticks(bool oneTickEarly)
    {
        await Start(playerX: 5, playerY: 5, settleTicks: 0);
        int fallTicks = 0;
        for (; fallTicks < 120 && !Player.OnGround; fallTicks++)
        {
            await Tick(Idle);
        }

        await Start(playerX: 5, playerY: 5, settleTicks: 0);
        int bufferTicks = Features.Current.PlayerJumpBufferTicks + (oneTickEarly ? 1 : 0);
        await Tick(Idle, fallTicks - bufferTicks);
        await Tick(Jump);
        await Tick(Idle, bufferTicks + 1);

        // Rising after a jump, at rest without one.
        AssertThat(Velocity.Y > 0).IsEqual(!oneTickEarly);
    }

    [TestCase]
    public async Task Falling_fast_while_driving_into_a_wall_reaches_the_floor()
    {
        // The player starts 34 tiles up, beside a wall, and holds towards it all the way down. The engine's continuous
        // detection, which starts above about 18 tiles per second, used to stop it in the air (028).
        string[] rows =
        [
            new string('#', 20),
            .. Enumerable.Range(0, 38).Select(row => row == 3
                ? "#..........P#......#"
                : "#...........#......#"),
            new string('#', 20),
        ];
        await Start(room: Room.FromText("Wall", string.Join('\n', rows)), settleTicks: 0);

        float fastest = 0;
        for (int i = 0; i < 150; i++)
        {
            await Tick(Right);
            fastest = MathF.Max(fastest, -Velocity.Y);
        }

        AssertThat(fastest).IsGreater(20f);
        AssertThat(Player.Contacts.HasFlag(Contacts.Right)).IsTrue();
        AssertThat(Player.OnGround).IsTrue();
    }

    [TestCase]
    public async Task Thrust_ends_when_the_body_hits_a_ceiling()
    {
        // A ceiling 1.4 tiles above the player's head, and thrust that would otherwise hold it there for 10 seconds.
        await Start(FullThrust with { PlayerJumpThrustTicks = 600 }, NoDrag, height: 5);

        await Tick(Jump, 60);

        AssertThat(Player.OnGround).IsTrue();
    }
}
