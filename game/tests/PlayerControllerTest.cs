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

namespace BodyCheck.Tests;

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

    /// <summary>How many ticks of thrust a held jump gets at speed 1: every tick that starts within the thrust time.</summary>
    private static int ThrustTicks => (int)MathF.Ceiling(Features.Current.PlayerJumpThrustTime / Dt);

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
        Engine.TimeScale = 1;
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
        return Telemetry.Get<float>(Metric.AirRise);
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
        await Start(new() { PlayerMass = 3 });

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
        await Tick(Jump, ThrustTicks);

        // Thrust cancels gravity for the thrust time, which may end partway through the last tick.
        float lostAfterThrust = Gravity * (ThrustTicks * Dt - Features.Current.PlayerJumpThrustTime);
        AssertThat(Velocity.Y).IsEqualApprox(speedAfterImpulse - lostAfterThrust, 0.001f);
    }

    [TestCase]
    public async Task Thrust_below_weight_slows_the_rise_less_than_gravity_alone()
    {
        await Start();
        float lostToGravityAlone = Gravity * ThrustTicks * Dt;

        await Tick(Jump, ThrustTicks);

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

        // Thrust starts the tick after the impulse, by when gravity has acted once, and holds that speed for the thrust
        // time. A tap gets none.
        float risenUnderThrust = Features.Current.PlayerJumpThrustTime * (Features.Current.PlayerJumpSpeed - Gravity * Dt);

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
        for (int tick = 0; tick <= ThrustTicks; tick++)
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

    [TestCase]
    public async Task The_tallest_jump_at_half_speed_rises_as_high_as_at_full_speed()
    {
        await Start();
        float full = await ApexOfJump(ticksHeld: 600);

        // As the game does at a gameplay speed of 0.5. Thrust is in game time, so it lasts twice as many ticks.
        Engine.TimeScale = 0.5;
        await Start();
        float half = await ApexOfJump(ticksHeld: 600);

        // Thrust adds the same speed at any tick length, but shorter steps integrate the rise more closely, so it goes
        // a little higher: measured 4.58 tiles, against 4.50.
        AssertThat(half).IsEqualApprox(full, 0.1f);
    }

    /// <summary>
    /// Running off a ledge, the player can jump until the last coyote tick, counted from the first in the air, but not
    /// on the tick after. Coyote time is real time, so it lasts as many ticks at any time scale.
    /// </summary>
    [TestCase(false, 1)]
    [TestCase(true, 1)]
    [TestCase(false, 0.5)]
    [TestCase(true, 0.5)]
    public async Task Coyote_time_lasts_exactly_its_ticks(bool oneTickLate, double timeScale)
    {
        Engine.TimeScale = timeScale;
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
        // detection, which starts above about 18 tiles per second, used to stop it in the air.
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

    /// <summary>
    /// Jumping up into a shaft 1 tile wide, off center so that the head would clip the corner of its mouth, the player
    /// is nudged into the shaft when the clip is within the correction, and bumps the corner when it isn't.
    /// </summary>
    [TestCase(0.25f, true)]
    [TestCase(0.5f, false)]
    public async Task Jumping_into_a_shaft_off_center_corrects_a_small_clip(float offCenter, bool entersShaft)
    {
        // The player's box is 0.8 tiles wide, so it clips the shaft's mouth by 0.15 tiles, or 0.4.
        await Start(room: RoomRun.RoomOf(
            "#########",
            "####.####",
            "####.####",
            "####.####",
            "####.####",
            "####.####",
            "#.......#",
            "#.......#",
            "#...P...#",
            "#########"));
        Player.MoveTo(Player.Position + new Vector3(offCenter, 0, 0));
        await Tick(Idle, 10);

        // The mouth of the shaft is 4 tiles up, and the feet start 1 tile up.
        float highestFeet = 0;
        for (int i = 0; i < 60; i++)
        {
            await Tick(Jump);
            highestFeet = MathF.Max(highestFeet, Player.Position.Y - Player.HalfSize.Y);
        }

        AssertThat(highestFeet > 4).IsEqual(entersShaft);
    }

    /// <summary>
    /// The player slides down a wall into a tunnel, under the wall, and stands flush with the corner of the tunnel's
    /// mouth. A full jump rises as high as from open ground, along the wall, without bumping the corner or being knocked
    /// more than a pixel away from the wall: from a tunnel 3 tiles high, and from one 2 tiles high, where the head is
    /// within a tick's rise of the corner when the jump starts.
    /// </summary>
    [TestCase(3)]
    [TestCase(2)]
    public async Task Jumping_flush_with_a_corner_rises_past_it(int tunnelHeight)
    {
        await Start(room: RoomRun.RoomOf(
        [
            "..P#..",
            .. Enumerable.Repeat("...#..", 3 - tunnelHeight + 1),
            "...###",
            .. Enumerable.Repeat("......", tunnelHeight),
            "######",
        ]), settleTicks: 0);
        for (int i = 0; i < 120 && !Player.Contacts.HasFlag(Contacts.Right); i++)
        {
            await Tick(Right);
        }
        for (int i = 0; i < 120 && !Player.OnGround; i++)
        {
            await Tick(Idle);
        }
        await Tick(Idle, 10);
        AssertThat(Player.Position.X + Player.HalfSize.X).IsEqualApprox(3, 0.01f);

        float highestFeet = 0;
        float leftmostSide = 3;
        for (int i = 0; i < 60; i++)
        {
            await Tick(Jump);
            highestFeet = MathF.Max(highestFeet, Player.Position.Y - Player.HalfSize.Y);
            leftmostSide = MathF.Min(leftmostSide, Player.Position.X + Player.HalfSize.X);
        }

        // The feet start 1 tile up, and the highest jump rises 4.5 tiles.
        AssertThat(highestFeet).IsGreater(5.4f);
        AssertThat(leftmostSide).IsGreaterEqual(3 - 1f / Constants.TilePixels);
    }

    /// <summary>
    /// Jumping from a tunnel 2 tiles high, with the head clipping the right corner of an opening in its ceiling, and
    /// moving sideways so that the jump leans by an angle from straight up. The player is nudged left into the opening
    /// when it leans left, however far, or leans right by no more than the correction's angle, but not when it leans
    /// further right. The head is within the jump's first step of the ceiling, so it is nudged on the jump's own tick.
    /// Leaning left, the clip is more than the jump's first step drifts left, so that the drift alone doesn't clear it.
    /// Leaning right by 18 degrees, near the angle of 20, the step drifts back towards the corner by 0.07 tiles, so the
    /// nudge must be that much larger. Not at the angle itself: the lean that counts is the predicted velocity's, which
    /// gravity and drag change a little from the one set here.
    /// </summary>
    [TestCase(-40f, 0.2f, true)]
    [TestCase(10f, 0.1f, true)]
    [TestCase(18f, 0.1f, true)]
    [TestCase(40f, 0.1f, false)]
    public async Task Jumping_at_an_angle_corrects_toward_the_lean_or_against_it_within_the_angle(
        float leanDegrees, float clip, bool entersOpening)
    {
        const float maxAngle = 20;
        await Start(new() { PlayerUpwardCornerCorrection = 0.25f, PlayerUpwardCornerCorrectionAngle = maxAngle },
            room: RoomRun.RoomOf(
                "#.........#",
                "#.........#",
                "####...####",
                "#.........#",
                "#.....P...#",
                "###########"));
        // The opening's right corner is at x = 7.
        Player.MoveTo(new Vector3(7 - Player.HalfSize.X + clip, Player.Position.Y, 0));
        await Tick(Idle, 10);

        float speedX = Features.Current.PlayerJumpSpeed * MathF.Tan(Mathf.DegToRad(leanDegrees));
        Player.LinearVelocity = new Vector3(speedX, 0, 0);
        float highestFeet = 0;
        float predictedAngle = 0;
        for (int i = 0; i < 60; i++)
        {
            await Tick(Jump);
            highestFeet = MathF.Max(highestFeet, Player.Position.Y - Player.HalfSize.Y);
            if (i == 0)
            {
                // The velocity used to determine whether the player should be nudged in the direction of the side.
                Vector3 v = Telemetry.Get(Metric.PredictedVelocity, Vector2.Zero).ToVector3();
                predictedAngle = Mathf.RadToDeg(MathF.Atan2(v.X, v.Y));
            }
        }

        if (entersOpening)
        {
            // Test validity: the predicted velocity must lean right by no more than the angle, to allow the nudge left.
            AssertThat(predictedAngle).IsLessEqual(maxAngle);
            // The ceiling's underside is 3 tiles up, and the feet start 1 tile up.
            AssertThat(highestFeet).IsGreater(3);
        }
        else
        {
            // Test validity: the predicted velocity must lean right by more than the angle, to forbid the nudge left.
            AssertThat(predictedAngle).IsGreater(maxAngle);
            // The ceiling's underside is 3 tiles up, and the feet start 1 tile up.
            AssertThat(highestFeet).IsLessEqual(3);
        }
    }

    /// <summary>
    /// Running right over a comb-shaped floor, 10 gaps 1 tile wide, the player crosses every gap at full speed, and at
    /// half speed, in every phase of the ticks tried: the starting place moves by about a fifth of a tick's run at full
    /// speed. Over each gap, the player is unsupported for 0.2 tiles of its run, and falls a little, so that its feet would
    /// catch on the far tile's corner. At a top speed of 2 tiles per second it falls longer, and leans down by about 50
    /// degrees by the time it meets the corner, more than the correction's angle, so it falls into the first gap.
    /// </summary>
    [TestCase(12f, 0f, true)]
    [TestCase(12f, 0.04f, true)]
    [TestCase(12f, 0.08f, true)]
    [TestCase(12f, 0.12f, true)]
    [TestCase(12f, 0.16f, true)]
    [TestCase(6f, 0f, true)]
    [TestCase(6f, 0.04f, true)]
    [TestCase(6f, 0.08f, true)]
    [TestCase(6f, 0.12f, true)]
    [TestCase(6f, 0.16f, true)]
    [TestCase(2f, 0f, false)]
    public async Task Running_over_a_comb_crosses_its_gaps_above_a_speed(float topSpeed, float offset, bool crosses)
    {
        await Start(
            new()
            {
                PlayerTopSpeed = topSpeed,
                PlayerForwardCornerCorrection = 0.25f,
                PlayerForwardCornerCorrectionAngle = 30,
            },
            room: RoomRun.RoomOf(
                "#" + new string('.', 38) + "#",
                "#P" + new string('.', 37) + "#",
                "#" + new string('#', 12) + string.Concat(Enumerable.Repeat(".#", 10)) + new string('#', 7),
                new string('#', 40)));
        Player.MoveTo(Player.Position + new Vector3(offset, 0, 0));
        await Tick(Idle, 5);

        // The comb's teeth are 2 tiles up, and the bottoms of its gaps 1 tile up. It ends at x = 33.
        float lowestFeet = float.PositiveInfinity;
        for (int i = 0; i < 1200 && Player.Position.X < 35; i++)
        {
            await Tick(Right);
            lowestFeet = MathF.Min(lowestFeet, Player.Position.Y - Player.HalfSize.Y);
        }

        AssertThat(lowestFeet > 1.5f).IsEqual(crosses);
    }

    /// <summary>
    /// With a longer, weaker jump thrust, carrying a block of mass 1, a jump rises only about 0.82 tiles. Running right
    /// and jumping 2.8 tiles before a step 1 tile high, the feet meet the step's face near the top of the rise, short of
    /// its top by about 0.18 tiles, and the player is nudged up onto it, with the block it carries.
    /// </summary>
    [TestCase(2.4f)] // Almost as close as possible for a valid test (at 2.2 or closer, it reaches the step too low)
    [TestCase(2.8f)] // First contact with step is almost at apex of jump
    [TestCase(3f)] // Nudge will be against velocity, which is downward at first contact with step
    public async Task Jumping_onto_a_step_carrying_a_block_corrects_a_short_rise(float jumpPosition)
    {
        await Start(
            new()
            {
                PlayerJumpThrustTime = 0.6f,
                PlayerJumpThrustFrac = 0.518f,
                BlockMass = 1,
                PlayerForwardCornerCorrection = 0.25f,
                PlayerForwardCornerCorrectionAngle = 30,
            },
            room: RoomRun.RoomOf(
                "#..................#",
                "#.B................#",
                "#..................#",
                "#.P.........########",
                "####################"),
            settleTicks: 40);
        Entity block = _world!.Entities.Single(entity => entity != Player && entity.Visible);
        AssertThat(block.Support).IsEqual(Player);

        // Wait until the player's leading edge is within range of the step, which is at x = 12.
        for (int i = 0; i < 300 && 12 - (Player.Position.X + Player.HalfSize.X) > jumpPosition; i++)
        {
            await Tick(Right);
        }
        // The feet as of the step in which the player reaches the step's face, where it is either stopped or corrected.
        // Not after it: stopped, the player goes on rising against the face, and the contact shows only then.
        float? reachingFeet = null;
        for (int i = 0; i < 90; i++)
        {
            float feet = Player.Position.Y - Player.HalfSize.Y;
            await Tick(new PlayerInput(1, 0, true));
            if (reachingFeet is null
                && (Player.Contacts.HasFlag(Contacts.Right)
                    || Player.Position.X + Player.HalfSize.X >= 12 - Entity.ContactTolerance))
            {
                reachingFeet = feet;
            }
        }

        // Test validity: reaching the step, the player must be high enough to get corner-corrected.
        AssertThat(reachingFeet ?? 0).IsBetween(2 - Features.Current.PlayerForwardCornerCorrection, 2);

        // The feet start 1 tile up, and the step's top is 2 tiles up.
        AssertThat(Player.Position.Y - Player.HalfSize.Y).IsEqualApprox(2, 0.01f);
        AssertThat(Player.OnGround).IsTrue();
        AssertThat(block.Support).IsEqual(Player);
    }

    /// <summary>
    /// As above, but standing against the step, pushing into it, and jumping while still pushing. The player's
    /// velocity into the step is zero as of the engine's last step, so only what this tick's drive adds to it shows
    /// that the player would meet the step. Its run into the step in a tick is less than the contact tolerance. It is
    /// nudged while still rising, so even with an angle of 0, which allows no nudge against its velocity: nudged only at
    /// the top of the rise, it would be falling by then. The step is 1 tile wide, so the player lets go on landing on it.
    /// </summary>
    [TestCase]
    public async Task Jumping_onto_a_step_carrying_a_block_from_against_it_corrects_a_short_rise()
    {
        await Start(
            new()
            {
                PlayerJumpThrustTime = 0.6f,
                PlayerJumpThrustFrac = 0.518f,
                BlockMass = 1,
                PlayerForwardCornerCorrection = 0.25f,
                PlayerForwardCornerCorrectionAngle = 0,
            },
            room: RoomRun.RoomOf(
                "#.........#",
                "#....B....#",
                "#.........#",
                "#...#P....#",
                "###########"),
            settleTicks: 40);
        Entity block = _world!.Entities.Single(entity => entity != Player && entity.Visible);
        AssertThat(block.Support).IsEqual(Player);

        // The step's face is at x = 5.
        PlayerInput left = new(-1, 0, false);
        await Tick(left, 30);
        AssertThat(Player.Position.X - Player.HalfSize.X).IsEqualApprox(5, 0.01f);
        for (int i = 0; i < 90 && !(Player.OnGround && Player.Position.Y - Player.HalfSize.Y > 1.5f); i++)
        {
            await Tick(left with { JumpHeld = true });
        }
        await Tick(Idle, 20);

        // The feet start 1 tile up, and the step's top is 2 tiles up.
        AssertThat(Player.Position.Y - Player.HalfSize.Y).IsEqualApprox(2, 0.01f);
        AssertThat(Player.OnGround).IsTrue();
        AssertThat(block.Support).IsEqual(Player);
    }

    [TestCase]
    public async Task Thrust_ends_when_the_body_hits_a_ceiling()
    {
        // A ceiling 1.4 tiles above the player's head, and thrust that would otherwise hold it there for 10 seconds.
        await Start(FullThrust with { PlayerJumpThrustTime = 10 }, NoDrag, height: 5);

        await Tick(Jump, 60);

        AssertThat(Player.OnGround).IsTrue();
    }
}
