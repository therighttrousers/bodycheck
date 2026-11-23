using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using BodyCheck.Core;
using BodyCheck.Core.Entities;
using BodyCheck.Core.Scripts;
using BodyCheck.Core.Tasks;
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

    /// <summary>A script that does nothing, for a trigger to run, and the library that makes it.</summary>
    private sealed class Nothing : IScript, IScriptLibrary
    {
        public TickTask Run(IScriptWorld world) => TickTask.Finished;

        public bool Holds(string className, IReadOnlyList<System.Type> entityTypes) =>
            className == nameof(Nothing) && entityTypes.Count == 0;

        public IScript Create(string className, IReadOnlyList<Entity> entities) => this;
    }

    /// <summary>A <c>Script</c> entity that runs <see cref="Nothing"/>.</summary>
    private static readonly EntityConfig.Script NothingScript = new()
    {
        Id = "Nothing",
        MapOrigin = Vector2I.Zero,
        MapSize = Vector2I.One,
        MapColor = Colors.White,
        ClassName = nameof(Nothing),
        Entities = [],
    };

    /// <summary>A run of <paramref name="room"/>, whose triggers run <see cref="NothingScript"/>.</summary>
    private static RoomRun RunOf(Room room) => new(room, scripts: new Nothing());

    /// <summary>
    /// A text room with a trigger whose box is <paramref name="size"/> tiles with its lower-left tile at
    /// <paramref name="origin"/>, which runs a script that does nothing.
    /// </summary>
    private static Room RoomWithTrigger(
        string[] rows, Vector2I origin, Vector2I size, TriggerSetOffBy setOffBy = TriggerSetOffBy.Touch)
    {
        Room room = RoomRun.RoomOf(rows);
        var trigger = new EntityConfig.Trigger
        {
            Id = "Trigger",
            MapOrigin = origin,
            MapSize = size,
            MapColor = Colors.White,
            Step = new Step(NothingScript.Id, TriggerAction.Run),
            StartsOn = true,
            SetOffBy = setOffBy,
        };
        return new Room(
            room.Name, room.Origin, TileGrid.FromText(rows), room.EntityConfigs.Append(trigger).Append(NothingScript));
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
        _run = RunOf(RoomWithTrigger(rows, new Vector2I(6, 1), new Vector2I(3, 2)));
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
        _run = RunOf(RoomWithTrigger(rows, new Vector2I(3, 1), new Vector2I(3, 3), TriggerSetOffBy.Interact));
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
    public async Task Interact_held_through_a_cutscene_is_no_new_press_when_it_ends()
    {
        // The same room and trigger, with the game's mode set by the test, as a cutscene the trigger starts would set it.
        string[] rows =
        [
            "##########",
            "#........#",
            "#........#",
            "#........#",
            "#...P...S#",
            "##########",
        ];
        _run = RunOf(RoomWithTrigger(rows, new Vector2I(3, 1), new Vector2I(3, 3), TriggerSetOffBy.Interact));
        GameMode mode = GameMode.Playing;
        _run.Node.GameModeSource = () => mode;
        var setOffs = new List<string>();
        _run.Node.TriggerSetOff += trigger => setOffs.Add(trigger.Id);
        var up = new PlayerInput(0, 1, false);
        await _run.Tick(RoomRun.Idle, 10);

        // A press sets it off, and up stays held through the cutscene and after it.
        await _run.Tick(up, 1);
        AssertArray(setOffs).ContainsExactly("Trigger");
        mode = GameMode.Cutscene;
        await _run.Tick(up, 10);
        mode = GameMode.Playing;
        await _run.Tick(up, 10);
        AssertArray(setOffs).ContainsExactly("Trigger");

        // A press after the cutscene sets it off again.
        await _run.Tick(RoomRun.Idle, 1);
        await _run.Tick(up, 1);
        AssertArray(setOffs).ContainsExactly("Trigger", "Trigger");
    }

    [TestCase]
    public async Task Interacting_sets_off_a_trigger_that_moves_with_an_entity_before_one_that_doesnt()
    {
        // Two interact triggers 3 tiles square around the player: one that stays where it is, first in the room's order,
        // and one that moves with the block beside the player.
        string[] rows =
        [
            "##########",
            "#........#",
            "#........#",
            "#........#",
            "#..PB...S#",
            "##########",
        ];
        Room room = RoomRun.RoomOf(rows);
        EntityConfig block = room.EntityConfigs.Single(config => config.Kind == EntityKind.Block);
        static EntityConfig.Trigger Trigger(string id, Vector2I origin, string? movesWith) => new()
        {
            Id = id,
            MapOrigin = origin,
            MapSize = new Vector2I(3, 3),
            MapColor = Colors.White,
            Step = new Step(NothingScript.Id, TriggerAction.Run),
            StartsOn = true,
            SetOffBy = TriggerSetOffBy.Interact,
            MovesWith = movesWith,
        };
        _run = RunOf(new Room(
            room.Name,
            room.Origin,
            TileGrid.FromText(rows),
            room.EntityConfigs.Append(Trigger("Stays", new Vector2I(2, 1), null)).Append(Trigger("Moves", new Vector2I(3, 1), block.Id))
                .Append(NothingScript)));
        var setOffs = new List<string>();
        _run.Node.TriggerSetOff += trigger => setOffs.Add(trigger.Id);
        Entity blockEntity = _run.Node.EntitiesOf(EntityKind.Block).Single();

        await _run.Tick(RoomRun.Idle, 10);
        AssertThat(_run.Node.InteractTrigger?.Id).IsEqual("Moves");
        // The prompt goes over the top of its area, where the block has it.
        Vector2 top = _run.Node.InteractTriggerTop!.Value;
        AssertFloat(top.X).IsEqualApprox(blockEntity.Position.X, 0.01f);
        AssertFloat(top.Y).IsEqualApprox(4, 0.01f);
        await _run.Tick(new PlayerInput(0, 1, false), 1);
        AssertArray(setOffs).ContainsExactly("Moves");

        // Its area goes with the block, and the other is set off instead.
        _run.Node.Remove(blockEntity);
        await _run.Tick(RoomRun.Idle, 2);
        AssertThat(_run.Node.InteractTrigger?.Id).IsEqual("Stays");
        await _run.Tick(new PlayerInput(0, 1, false), 1);
        AssertArray(setOffs).ContainsExactly("Moves", "Stays");
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
        _run = RunOf(RoomWithTrigger(rows, new Vector2I(20, 1), new Vector2I(1, 7)));
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
