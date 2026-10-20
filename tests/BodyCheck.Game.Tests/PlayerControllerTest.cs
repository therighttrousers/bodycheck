using System;
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
    private static readonly PlayerTuning FullThrust = new() { JumpThrustFrac = 1 };

    private WorldNode? _world;
    private PlayerInput _input;

    private static readonly float Gravity = (float)ProjectSettings.GetSetting("physics/3d/default_gravity");

    private Entity Player => _world!.Player;
    private PlayerTuning Tuning => _world!.PlayerTuning;
    private Vector3 Velocity => Player.LinearVelocity;

    private static SceneTree Tree => (SceneTree)Engine.GetMainLoop();

    /// <summary>Loads an empty room, replacing the one loaded before, and by default waits for the player to settle.</summary>
    /// <param name="settleTicks">How long to wait before returning. Pass 0 for a player that starts in the air.</param>
    private async Task Start(
        PlayerTuning? tuning = null, BodyTuning? body = null,
        int height = 20, int playerX = 1, int playerY = 1, int settleTicks = 30)
    {
        _world?.Free();
        _input = Idle;
        Room frame = TestRoomLibrary.EmptyFrame(60, height, playerX, playerY);
        _world = new WorldNode(new World(frame), EntityVisuals.For, playerTuning: tuning, playerBody: body)
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
        float drive = Tuning.GroundForwardDriveForce;
        float balance = drive / (drive / Tuning.TopSpeed + Player.Tuning.Drag);

        await Tick(Right, 120);

        AssertThat(Velocity.X).IsEqualApprox(balance, 0.001f);
    }

    [TestCase]
    public async Task Running_without_drag_approaches_top_speed_without_passing_it()
    {
        await Start(body: NoDrag);

        await Tick(Right, 120);

        AssertThat(Velocity.X).IsBetween(0.99f * Tuning.TopSpeed, Tuning.TopSpeed);
    }

    [TestCase]
    public async Task Running_reaches_most_of_top_speed_quickly()
    {
        await Start();

        await Tick(Right, 20);

        AssertThat(Velocity.X).IsGreater(0.8f * Tuning.TopSpeed);
    }

    [TestCase]
    public async Task Driving_does_not_slow_a_body_that_is_over_top_speed()
    {
        await Start(body: NoDrag);

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
        AssertThat(Velocity.Y).IsEqualApprox(Tuning.JumpSpeed - Gravity * Dt, 0.001f);
    }

    [TestCase]
    public async Task Thrust_equal_to_weight_holds_the_rise_at_constant_speed()
    {
        await Start(FullThrust, NoDrag);

        await Tick(Jump);
        float speedAfterImpulse = Velocity.Y;
        await Tick(Jump, Tuning.JumpThrustTicks);

        AssertThat(Velocity.Y).IsEqualApprox(speedAfterImpulse, 0.001f);
    }

    [TestCase]
    public async Task Thrust_below_weight_slows_the_rise_less_than_gravity_alone()
    {
        await Start();
        float lostToGravityAlone = Gravity * Tuning.JumpThrustTicks * Dt;

        await Tick(Jump, Tuning.JumpThrustTicks);

        AssertThat(Velocity.Y).IsBetween(Tuning.JumpSpeed - lostToGravityAlone, Tuning.JumpSpeed);
    }

    [TestCase]
    public async Task A_tap_without_drag_jumps_about_as_high_as_the_impulse_alone()
    {
        await Start(body: NoDrag);
        float impulseOnly = Tuning.JumpSpeed * Tuning.JumpSpeed / (2 * Gravity);

        float apex = await ApexOfJump(ticksHeld: 1);

        AssertThat(apex).IsBetween(impulseOnly - 0.2f, impulseOnly + 0.2f);
    }

    [TestCase]
    public async Task Holding_jump_adds_the_distance_risen_under_thrust()
    {
        await Start(FullThrust, NoDrag);
        float tap = await ApexOfJump(ticksHeld: 1);

        // Thrust starts the tick after the impulse, by when gravity has acted once. A tap gets none.
        float risenUnderThrust = Tuning.JumpThrustTicks * (Tuning.JumpSpeed - Gravity * Dt) * Dt;

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
        for (int tick = 0; tick <= Tuning.JumpThrustTicks; tick++)
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
        AssertThat(Telemetry.Get(Metric.AirTicks)).IsNotNull(); // It has landed.

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

    [TestCase]
    public async Task Thrust_ends_when_the_body_hits_a_ceiling()
    {
        // A ceiling 1.4 tiles above the player's head, and thrust that would otherwise hold it there for 10 seconds.
        await Start(FullThrust with { JumpThrustTicks = 600 }, NoDrag, height: 5);

        await Tick(Jump, 60);

        AssertThat(Player.OnGround).IsTrue();
    }
}
