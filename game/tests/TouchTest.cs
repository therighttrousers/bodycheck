using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using BodyCheck.Core;
using BodyCheck.Core.Entities;
using BodyCheck.Core.WorldData;

using GdUnit4;

using Godot;

using static GdUnit4.Assertions;

namespace BodyCheck.Tests;

/// <summary>Triggers set off by the player's touch.</summary>
[TestSuite]
[RequireGodotRuntime]
public class TouchTest
{
    private RoomRun? _run;

    [AfterTest]
    public void TearDown()
    {
        _run?.Free();
        _run = null;
    }

    /// <summary>
    /// A text room with a trigger whose box is <paramref name="size"/> tiles with its lower-left tile at
    /// <paramref name="origin"/>, which makes the room's respawn current.
    /// </summary>
    private static Room RoomWithTrigger(
        string[] rows, Vector2I origin, Vector2I size, TriggerSetOffBy setOffBy = TriggerSetOffBy.Touch)
    {
        Room room = RoomRun.RoomOf(rows);
        EntityConfig respawn = room.EntityConfigs.Single(config => config.Kind == EntityKind.Respawn);
        var trigger = new EntityConfig.Trigger
        {
            Id = "Trigger",
            MapOrigin = origin,
            MapSize = size,
            MapColor = Colors.White,
            Step = new Step(respawn.Id, TriggerAction.Default),
            StartsOn = true,
            SetOffBy = setOffBy,
        };
        return new Room(room.Name, room.Origin, TileGrid.FromText(rows), room.EntityConfigs.Append(trigger));
    }

    [TestCase]
    public async Task The_player_sets_a_trigger_off_each_time_it_comes_into_it()
    {
        // A trigger 3 tiles wide, 5 tiles to the right of the player.
        string[] rows =
        [
            "################",
            "#..............#",
            "#..............#",
            "#P............S#",
            "################",
        ];
        _run = new RoomRun(RoomWithTrigger(rows, new Vector2I(6, 1), new Vector2I(3, 2)));
        var touches = new List<string>();
        _run.Node.TriggerSetOff += trigger => touches.Add(trigger.Id);

        // Running right through it, taking several ticks to cross, then back.
        await _run.Tick(RoomRun.Right, 60);
        AssertArray(touches).ContainsExactly("Trigger");
        await _run.Tick(RoomRun.Left, 60);
        AssertArray(touches).ContainsExactly("Trigger", "Trigger");
    }

    [TestCase]
    public async Task The_player_sets_off_an_interact_trigger_by_pressing_up_in_it_on_the_ground()
    {
        // A trigger 3 tiles wide and 3 high around the player, which starts on the ground.
        string[] rows =
        [
            "##########",
            "#........#",
            "#........#",
            "#........#",
            "#...P...S#",
            "##########",
        ];
        _run = new RoomRun(RoomWithTrigger(rows, new Vector2I(3, 1), new Vector2I(3, 3), TriggerSetOffBy.Interact));
        var setOffs = new List<string>();
        _run.Node.TriggerSetOff += trigger => setOffs.Add(trigger.Id);
        var up = new PlayerInput(0, 1, false);

        // Coming into it, and standing in it, set nothing off.
        await _run.Tick(RoomRun.Idle, 10);
        AssertArray(setOffs).IsEmpty();
        AssertThat(_run.Node.InteractTrigger?.Id).IsEqual("Trigger");

        // A press sets it off once, however long it is held.
        await _run.Tick(up, 10);
        AssertArray(setOffs).ContainsExactly("Trigger");

        // In the air, a press sets nothing off.
        await _run.Tick(RoomRun.Jump, 3);
        await _run.Tick(new PlayerInput(0, 1, true), 1);
        AssertArray(setOffs).ContainsExactly("Trigger");
        AssertThat(_run.Node.InteractTrigger).IsNull();
    }

    [TestCase]
    public async Task A_body_that_crosses_a_trigger_within_a_tick_but_moves_less_than_both_are_wide_sets_it_off()
    {
        // A trigger 1 tile wide, which the player crosses in mid-air.
        string[] rows =
        [
            "##############################",
            "#............................#",
            "#............................#",
            "#P...........................#",
            "#............................#",
            "#............................#",
            "#............................#",
            "#...........................S#",
            "##############################",
        ];
        _run = new RoomRun(RoomWithTrigger(rows, new Vector2I(20, 1), new Vector2I(1, 7)));
        var touches = new List<string>();
        _run.Node.TriggerSetOff += trigger => touches.Add(trigger.Id);

        // 1.5 tiles a tick, more than the trigger is wide, but less than the trigger and the player's 0.8 tiles together.
        float speed = 1.5f * Engine.PhysicsTicksPerSecond;
        Entity player = _run.Player;
        // Holding the player's speed, and holding it up, before each tick's step.
        await _run.Tick(RoomRun.Idle, 20, () => player.LinearVelocity = new Vector3(speed, 0, 0));

        AssertFloat(player.Position.X).IsGreater(21);
        AssertArray(touches).ContainsExactly("Trigger");
    }
}
