using System;
using System.Linq;

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

        AssertArray(world.EntityConfigs.Select(config => (config.Kind, config.Start))).ContainsExactly(
            (EntityKind.Player, new Vector2(1.5f, 0)), (EntityKind.Block, new Vector2(4.5f, 1)));
    }

    [TestCase]
    public void Puts_a_platforms_range_at_its_rooms_origin()
    {
        // The platform moves between the walls, with its middle from 2.5 to 6.5 in its room.
        Room platformRoom = Room.FromText("Platform", "#...H...#", new Vector2I(3, 1));
        var world = new World("Test", [Room.FromText("Player", "P.."), platformRoom]);

        var platform = world.EntityConfigs.OfType<EntityConfig.Platform>().Single();
        AssertThat((platform.RangeA, platform.RangeB)).IsEqual((new Vector2(5.5f, 1.5f), new Vector2(9.5f, 1.5f)));
    }
}
