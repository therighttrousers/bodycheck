using System;
using System.Linq;

using BodyCheck.Core;
using BodyCheck.Core.WorldData;

using GdUnit4;

using Godot;

using static GdUnit4.Assertions;

namespace BodyCheck.Tests;

[TestSuite]
[RequireGodotRuntime]
public class WorldTest
{
    [TestCase]
    public void Refuses_rooms_that_overlap_but_not_rooms_that_touch()
    {
        Room first = Room.FromText("First", "P..\n...");
        Room touching = Room.FromText("Touching", "...", new Vector2I(3, 1));
        Room overlapping = Room.FromText("Overlapping", "...", new Vector2I(2, 1));

        _ = new World("Touch", [first, touching]);
        AssertThrown(() => new World("Overlap", [first, overlapping])).IsInstanceOf<ArgumentException>();
    }

    [TestCase("...", "..P", true)] // The only player is in the second room
    [TestCase("...", "...", false)]
    [TestCase("P..", "..P", false)]
    public void Has_exactly_one_player_across_its_rooms(string first, string second, bool valid)
    {
        World Create() => new("Test", [Room.FromText("First", first), Room.FromText("Second", second, new Vector2I(3, 0))]);

        if (valid)
        {
            _ = Create();
        }
        else
        {
            AssertThrown(() => Create()).IsInstanceOf<ArgumentException>();
        }
    }

    [TestCase]
    public void Puts_tiles_and_starts_at_their_rooms_origins_with_empty_tiles_between()
    {
        // Two 3x2 rooms, the second a tile to the right of the first and a tile up.
        Room left = Room.FromText("Left", "#..\n.P.");
        Room right = Room.FromText("Right", "...\nB.#", new Vector2I(4, 1));
        var world = new World("Test", [left, right]);

        AssertThat((world.Size.X, world.Size.Y)).IsEqual((7, 3));
        AssertThat(world.IsSolid(0, 1)).IsTrue(); // Left's '#'
        AssertThat(world.IsSolid(1, 0)).IsFalse();
        AssertThat(world.IsSolid(6, 1)).IsTrue(); // Right's '#'
        AssertThat(world.IsSolid(5, 2)).IsFalse();
        AssertThat(world.IsSolid(3, 1)).IsFalse(); // Between the rooms
        AssertThat(world.IsSolid(0, 2)).IsFalse(); // Above the left room

        AssertArray(world.Bodies.Select(config => (config.Kind, config.MapOrigin))).ContainsExactly(
            (EntityKind.Player, new Vector2I(1, 0)), (EntityKind.Block, new Vector2I(4, 1)));
    }

    [TestCase]
    public void Puts_a_platforms_range_at_its_rooms_origin()
    {
        // The platform moves between the walls, with its middle from 2.5 to 6.5 in its room.
        Room platformRoom = Room.FromText("Platform", "#...H...#", new Vector2I(3, 1));
        var world = new World("Test", [Room.FromText("Player", "P.."), platformRoom]);

        var platform = world.Bodies.OfType<EntityConfig.Platform>().Single();
        AssertThat((platform.RangeA, platform.RangeB)).IsEqual((new Vector2I(5, 1), new Vector2I(9, 1)));
    }

    private static EntityConfig.Player Player => new()
    {
        Id = "player",
        MapOrigin = Vector2I.Zero,
        MapSize = new Vector2I(1, 2),
        MapColor = Colors.White,
    };

    private static EntityConfig.VerticalDoor Door => new()
    {
        Id = "door",
        MapOrigin = new Vector2I(2, 1),
        MapSize = new Vector2I(1, 3),
        MapColor = Colors.White,
        OpenStop = new Vector2I(2, 4),
        CloseStop = new Vector2I(2, -1),
    };

    private static EntityConfig.Dialogue Dialogue => new()
    {
        Id = "dialogue",
        MapOrigin = new Vector2I(1, 0),
        MapSize = Vector2I.One,
        MapColor = Colors.White,
        File = "File",
        Title = "Title",
        Garble = 0,
    };

    private static EntityConfig.Trigger Trigger(string target, TriggerAction action = TriggerAction.Default) => new()
    {
        Id = "trigger",
        MapOrigin = new Vector2I(1, 0),
        MapSize = Vector2I.One,
        MapColor = Colors.White,
        Step = new Step(target, action),
    };

    private static EntityConfig.Once Once(string target, TriggerAction action = TriggerAction.Default) => new()
    {
        Id = "once",
        MapOrigin = new Vector2I(1, 0),
        MapSize = Vector2I.One,
        MapColor = Colors.White,
        Step = new Step(target, action),
    };

    private static EntityConfig.Sequence Sequence(params Step[] steps) => new()
    {
        Id = "sequence",
        MapOrigin = new Vector2I(1, 0),
        MapSize = Vector2I.One,
        MapColor = Colors.White,
        Steps = steps,
    };

    private static World WorldOf(params EntityConfig[] configs) =>
        new("Test", [new Room("Test", default, new TileGrid(3, 3), configs)]);

    [TestCase]
    public void Keeps_bodies_apart_from_control_entities_and_finds_each_by_id()
    {
        World world = WorldOf(
            Trigger("sequence"),
            Player,
            Sequence(new Step("dialogue", TriggerAction.Default), new Step("door", TriggerAction.Close)),
            Door,
            Dialogue);

        AssertArray(world.Bodies.Select(config => config.Kind)).ContainsExactly(EntityKind.Player, EntityKind.VerticalDoor);
        AssertArray(world.Controls.Select(config => config.Kind)).ContainsExactly(
            EntityKind.Trigger, EntityKind.Sequence, EntityKind.Dialogue);
        AssertThat(world.ConfigsById["door"].Kind).IsEqual(EntityKind.VerticalDoor);
    }

    [TestCase("missing", TriggerAction.Default)] // Not in the world
    [TestCase("door", TriggerAction.Default)] // Responds to Close only
    [TestCase("dialogue", TriggerAction.Close)] // Responds to Default only
    [TestCase("trigger", TriggerAction.Default)] // Responds to no action
    [TestCase("sequence", TriggerAction.Default)] // Back to the once, through the sequence
    public void Refuses_a_route_that_its_target_cant_run(string target, TriggerAction action) =>
        AssertThrown(() => WorldOf(
                Player, Door, Dialogue, Trigger("dialogue"), Once(target, action),
                Sequence(new Step("once", TriggerAction.Default))))
            .IsInstanceOf<ArgumentException>();

    [TestCase]
    public void Refuses_two_entities_with_one_id() =>
        AssertThrown(() => WorldOf(Player, Dialogue, Dialogue)).IsInstanceOf<ArgumentException>();

    [TestCase]
    public void Has_kinds_that_respond_to_Default_only_or_not_at_all()
    {
        foreach (EntityKind kind in Enum.GetValues<EntityKind>())
        {
            bool defaultOnly = kind.Responses.SequenceEqual([TriggerAction.Default]);
            AssertThat(defaultOnly || !kind.Responses.Contains(TriggerAction.Default))
                .OverrideFailureMessage($"{kind} responds to Default and to others.")
                .IsTrue();
        }
    }

    [TestCase]
    public void Refuses_an_entity_with_an_empty_box() =>
        AssertThrown(() => WorldOf(Player with { MapSize = new Vector2I(1, 0) })).IsInstanceOf<ArgumentException>();
}
