using System.Linq;
using System.Threading.Tasks;

using BodyCheck.Core.Entities;
using BodyCheck.Core.Tasks;
using BodyCheck.Core.WorldData;

using GdUnit4;

using Godot;

using static GdUnit4.Assertions;

namespace BodyCheck.Tests;

/// <summary>Vertical doors, which move between their stops when asked to open or close.</summary>
[TestSuite]
[RequireGodotRuntime]
public class DoorTest
{
    // Long enough for a door to open or close, which gives up after a time of its own.
    private const int Ticks = 180;

    private RoomRun? _run;

    [AfterTest]
    public void TearDown()
    {
        _run?.Free();
        _run = null;
    }

    /// <summary>A text room with a door, whose stops are in its leftmost column.</summary>
    private static Room RoomWithDoor(string[] rows, Vector2I origin, Vector2I size, int openStopY, int closeStopY)
    {
        Room room = RoomRun.RoomOf(rows);
        var door = new EntityConfig.VerticalDoor
        {
            Id = "door",
            MapOrigin = origin,
            MapSize = size,
            MapColor = Colors.White,
            OpenStop = new Vector2I(origin.X, openStopY),
            CloseStop = new Vector2I(origin.X, closeStopY),
        };
        return new Room(room.Name, room.Origin, TileGrid.FromText(rows), room.EntityConfigs.Append(door));
    }

    private Entity Door => _run!.Node.EntitiesOf(EntityKind.VerticalDoor).Single();

    private IActionTarget DoorController => Door.Controller!;

    [TestCase]
    public async Task A_door_closes_onto_its_close_stop_and_opens_back_to_its_open_stop()
    {
        string[] rows =
        [
            "##########",
            "#........#",
            "#........#",
            "#........#",
            "#........#",
            "#........#",
            "#........#",
            "#P.......#",
            "##########",
        ];
        // A door 3 tiles high, against its open stop, which closes into the floor's tile.
        _run = new RoomRun(RoomWithDoor(rows, new Vector2I(6, 4), new Vector2I(1, 3), openStopY: 7, closeStopY: 0));

        TickTask close = DoorController.Close(_run.Node.Tick);
        await _run.Tick(RoomRun.Idle, Ticks);
        AssertThat(close.IsFinished).IsTrue();
        AssertThat(Door.Freeze).IsTrue();
        AssertFloat(Door.Bottom()).IsEqualApprox(1, 0.05f);

        TickTask open = DoorController.Open(_run.Node.Tick);
        await _run.Tick(RoomRun.Idle, Ticks);
        AssertThat(open.IsFinished).IsTrue();
        AssertThat(Door.Freeze).IsTrue();
        AssertFloat(Door.Top()).IsEqualApprox(7, 0.05f);
    }

    [TestCase]
    public async Task A_door_that_meets_something_stops_there_and_counts_as_closed()
    {
        string[] rows =
        [
            "##########",
            "#........#",
            "#........#",
            "#........#",
            "#........#",
            "#........#",
            "#........#",
            "#P....B..#",
            "##########",
        ];
        _run = new RoomRun(RoomWithDoor(rows, new Vector2I(6, 4), new Vector2I(1, 3), openStopY: 7, closeStopY: 0));

        TickTask close = DoorController.Close(_run.Node.Tick);
        await _run.Tick(RoomRun.Idle, Ticks);
        AssertThat(close.IsFinished).IsTrue();
        AssertThat(Door.Freeze).IsTrue();
        // On the block, which the door's thrust presses a little into the floor, not on its stop a tile lower.
        AssertFloat(Door.Bottom()).IsEqualApprox(2, 0.1f);

        // Closed, so asked again, it finishes at once.
        AssertThat(DoorController.Close(_run.Node.Tick).IsFinished).IsTrue();
    }

    [TestCase]
    public async Task A_toggle_queued_behind_a_run_in_progress_runs_after_it()
    {
        string[] rows =
        [
            "##########",
            "#........#",
            "#........#",
            "#........#",
            "#........#",
            "#........#",
            "#........#",
            "#P.......#",
            "##########",
        ];
        _run = new RoomRun(RoomWithDoor(rows, new Vector2I(6, 4), new Vector2I(1, 3), openStopY: 7, closeStopY: 0));

        _ = DoorController.Close(_run.Node.Tick);
        await _run.Tick(RoomRun.Idle, 2);
        // Asked while it closes, so it opens once it has closed.
        TickTask toggle = DoorController.ToggleOpenClose(_run.Node.Tick);
        await _run.Tick(RoomRun.Idle, 2 * Ticks);

        AssertThat(toggle.IsFinished).IsTrue();
        AssertFloat(Door.Top()).IsEqualApprox(7, 0.05f);
    }

    [TestCase]
    public async Task The_player_runs_on_after_riding_a_door_that_unfreezes_and_freezes_again()
    {
        // A door 3 tiles wide and 1 high, with the player standing on it, which closes into the floor's tile.
        string[] rows =
        [
            "##########",
            "#........#",
            "#........#",
            "#........#",
            "#.....P..#",
            "#........#",
            "#........#",
            "#........#",
            "#........#",
            "##########",
        ];
        _run = new RoomRun(RoomWithDoor(rows, new Vector2I(5, 4), new Vector2I(3, 1), openStopY: 5, closeStopY: 0));
        // Standing on the door while it is frozen open, so that the player's grip is anchored to it.
        await _run.Tick(RoomRun.Idle, 30);
        AssertFloat(_run.Player.Bottom()).IsEqualApprox(5, 0.05f);

        TickTask close = DoorController.Close(_run.Node.Tick);
        await _run.Tick(RoomRun.Idle, Ticks);
        AssertThat(close.IsFinished).IsTrue();
        AssertThat(Door.Freeze).IsTrue();
        AssertFloat(_run.Player.Bottom()).IsEqualApprox(2, 0.05f);

        // Its grip works on the door frozen again: it runs off it, onto the floor.
        float start = _run.Player.Position.X;
        await _run.Tick(RoomRun.Right, 30);
        AssertFloat(_run.Player.Position.X - start).IsGreater(1.5f);
    }
}
