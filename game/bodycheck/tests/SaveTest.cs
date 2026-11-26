using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

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
        state.Story.TheCall.InnerDoorWontCloseReason = 2;
        SaveData saved = state.ToSave(DateTimeOffset.Now);
        // As if from a world not filled in yet.
        saved = saved with
        {
            Entities = new Dictionary<string, EntityData>(saved.Entities)
            {
                ["elsewhere"] = new DoorData { LastAction = OpenableAction.Close },
            },
        };
        _store.Save(SaveStore.DefaultSlot, saved);
        string file = File.ReadAllText(Path.Combine(_directory, SaveStore.DefaultSlot + ".json"));
        AssertThat(JsonNode.Parse(file)!["Story"]!.ToJsonString())
            .IsEqual("""{"TheCall":{"InnerDoorWontCloseReason":2}}""");

        var loaded = new GameState(_store.Load(SaveStore.DefaultSlot)!);
        loaded.Fill(world);

        AssertThat(loaded.Started).IsEqual(state.Started);
        AssertThat(loaded.WorldName).IsEqual(world.Name);
        AssertThat(loaded.GetRespawnId(world.Name)).IsEqual("respawn");
        AssertThat(loaded.Player.CanPhase).IsFalse();
        AssertThat(loaded.Story.TheCall.InnerDoorWontCloseReason).IsEqual(2);
        AssertThat(loaded.GetEntity<DoorState>("door").LastAction).IsEqual("Open");
        AssertThat(loaded.GetEntity<SwitchState>("trigger").IsOn).IsFalse();
        AssertThat(loaded.ToSave(DateTimeOffset.Now).Entities["elsewhere"]).IsEqual(new DoorData { LastAction = OpenableAction.Close });
    }

    [TestCase]
    public void Everything_a_save_holds_is_the_games_own_and_reads_from_a_file_without_it()
    {
        List<string> problems = [];
        HashSet<Type> seen = [];
        void Check(Type type)
        {
            if (!seen.Add(type))
            {
                return;
            }
            if (type.IsEnum && type.GetCustomAttribute<JsonConverterAttribute>()?.ConverterType
                != typeof(StrictEnumConverter<>).MakeGenericType(type))
            {
                problems.Add($"{type.Name} isn't read by name only.");
            }
            JsonTypeInfo info = SaveJson.Info.Options.GetTypeInfo(type);
            foreach (JsonPropertyInfo property in info.Properties)
            {
                if ((property.AttributeProvider as MemberInfo)?.DeclaringType?.Assembly != typeof(SaveData).Assembly
                    && info.Kind == JsonTypeInfoKind.Object)
                {
                    problems.Add($"{type.Name}.{property.Name} isn't the game's own.");
                }
                if (property.AttributeProvider is PropertyInfo { SetMethod: MethodInfo set } member
                    && set.ReturnParameter.GetRequiredCustomModifiers().Contains(typeof(IsExternalInit))
                    && !property.IsRequired)
                {
                    problems.Add($"{type.Name}.{member.Name} is init without required.");
                }
                Check(property.PropertyType);
            }
            foreach (JsonDerivedType derived in info.PolymorphismOptions?.DerivedTypes ?? [])
            {
                Check(derived.DerivedType);
            }
            if (info.ElementType is Type element)
            {
                Check(element);
            }
        }

        Check(typeof(SaveData));

        AssertThat(problems).IsEmpty();
        AssertThat(seen).Contains(typeof(TheCallBeat), typeof(SwitchData), typeof(OpenableAction));
    }

    [TestCase]
    public void A_save_without_a_field_added_since_gets_the_fields_default()
    {
        Directory.CreateDirectory(_directory);
        File.WriteAllText(Path.Combine(_directory, SaveStore.DefaultSlot + ".json"), """
            { "Version": 1, "Started": "2026-11-26T00:00:00+00:00", "Saved": "2026-11-26T00:00:00+00:00",
              "WorldName": null, "RespawnIds": {},
              // A comment, and a trailing comma, as in a file edited by hand.
              "Entities": { "player": { "Kind": "Player", "CanPhase": false }, "door": { "Kind": "Door" }, }, }
            """);

        SaveData save = _store.Load(SaveStore.DefaultSlot)!;

        AssertThat(save.Entities["player"]).IsEqual(new PlayerData { CanPhase = false, CanSpawn = true });
        AssertThat(save.Entities["door"]).IsEqual(new DoorData { LastAction = OpenableAction.None });
        AssertThat(new GameState(save).Story.TheCall.InnerDoorWontCloseReason).IsEqual(0);
    }

    [TestCase("{")]
    [TestCase("""{ "Version": 1, "Started": "2026-11-26T00:00:00+00:00", "Saved": "2026-11-26T00:00:00+00:00", "RespawnIds": {}, "Entities": { "t": { "Kind": "Switch" } } }""")]
    [TestCase("null")]
    [TestCase("""{ "Version": 1 }""")]
    [TestCase("""{ "Version": 1, "Started": "2026-11-26T00:00:00+00:00", "Saved": "2026-11-26T00:00:00+00:00", "RespawnIds": {}, "Entities": { "d": { "Kind": "Door", "LastAction": 1 } } }""")]
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
