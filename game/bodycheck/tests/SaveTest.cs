using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using BodyCheck.Core;
using BodyCheck.Core.Entities;
using BodyCheck.Core.WorldData;
using BodyCheck.State;

using GdUnit4;

using Godot;

using static BodyCheck.Tests.Exceptions;
using static GdUnit4.Assertions;

namespace BodyCheck.Tests;

/// <summary>Saves, written and read through a store, and the game's state made from them.</summary>
[TestSuite]
[RequireGodotRuntime]
public class SaveTest
{
    private string _directory = null!;
    private SaveStore _store = null!;

    [BeforeTest]
    public void Setup()
    {
        _directory = Path.Combine(Path.GetTempPath(), "bodycheck-save-test-" + Guid.NewGuid());
        _store = new SaveStore(_directory);
    }

    [AfterTest]
    public void Teardown()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    /// <summary>A world of one room, with a door, <c>door</c>, and a trigger, <c>trigger</c>, that opens it.</summary>
    private static World WorldWithDoor()
    {
        string[] rows =
        [
            "##########",
            "#........#",
            "#........#",
            "#........#",
            "#P.......#",
            "##########",
        ];
        Room room = RoomRun.RoomOf(rows);
        EntityConfig[] configs =
        [
            new EntityConfig.VerticalDoor
            {
                Id = "door",
                MapOrigin = new Vector2I(6, 1),
                MapSize = new Vector2I(1, 2),
                MapColor = Colors.White,
                OpenStop = new Vector2I(6, 4),
                CloseStop = new Vector2I(6, 0),
            },
            new EntityConfig.Trigger
            {
                Id = "trigger",
                MapOrigin = new Vector2I(2, 1),
                MapSize = Vector2I.One,
                MapColor = Colors.White,
                Step = new Step("door", TriggerAction.Open),
                StartsOn = true,
                SetOffBy = TriggerSetOffBy.Touch,
            },
        ];
        return new World(new Room(room.Name, room.Origin, TileGrid.FromText(rows), room.EntityConfigs.Concat(configs)));
    }

    [TestCase]
    public void A_saved_game_comes_back_and_keeps_what_belongs_to_worlds_not_entered_yet()
    {
        World world = WorldWithDoor();
        var state = new GameState(world) { WorldName = world.Name };
        IGameState core = state;
        core.GetEntity<IGameState.IDoorState>("door").LastAction = OpenableAction.Open;
        core.GetEntity<IGameState.ISwitchState>("trigger").IsOn = false;
        state.SetRespawnId(world.Name, "respawn");
        state.Player.CanPhase = false;
        // As if from a world not filled in yet.
        SaveData saved = state.ToSave(DateTimeOffset.Now) with
        {
            Doors = new Dictionary<string, OpenableAction>
            {
                ["door"] = OpenableAction.Open,
                ["elsewhere"] = OpenableAction.Close,
            },
        };
        _store.Save(SaveStore.DefaultSlot, saved);

        var loaded = new GameState(_store.Load(SaveStore.DefaultSlot)!);
        loaded.Fill(world);

        AssertThat(loaded.Started).IsEqual(state.Started);
        AssertThat(loaded.WorldName).IsEqual(world.Name);
        AssertThat(loaded.GetRespawnId(world.Name)).IsEqual("respawn");
        AssertThat(loaded.Player.CanPhase).IsFalse();
        AssertThat(loaded.GetEntity<GameState.DoorState>("door").LastAction).IsEqual("Open");
        AssertThat(loaded.GetEntity<GameState.SwitchState>("trigger").IsOn).IsFalse();
        AssertThat(loaded.ToSave(DateTimeOffset.Now).Doors["elsewhere"]).IsEqual(OpenableAction.Close);
    }

    [TestCase("{")]
    [TestCase("null")]
    [TestCase("""{ "version": 1 }""")]
    public void A_save_that_cant_be_read_is_kept_aside_so_that_the_next_game_starts_new(string text)
    {
        Directory.CreateDirectory(_directory);
        string path = Path.Combine(_directory, SaveStore.DefaultSlot + ".json");
        File.WriteAllText(path, text);

        Thrown<InvalidDataException>(() => _store.Load(SaveStore.DefaultSlot));

        AssertThat(File.ReadAllText(Path.Combine(_directory, SaveStore.DefaultSlot + ".bad.json"))).IsEqual(text);
        AssertThat(_store.Load(SaveStore.DefaultSlot)).IsNull();
    }

    [TestCase]
    public void A_save_of_another_version_is_kept_aside()
    {
        _store.Save(SaveStore.DefaultSlot, new GameState().ToSave(DateTimeOffset.Now) with { Version = 2 });

        Thrown<InvalidDataException>(() => _store.Load(SaveStore.DefaultSlot));
        AssertThat(_store.Load(SaveStore.DefaultSlot)).IsNull();
    }
}
