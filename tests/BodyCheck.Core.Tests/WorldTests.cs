using BodyCheck.Core.WorldData;

using Godot;

namespace BodyCheck.Core.Tests;

public class WorldTests
{
    [Fact]
    public void Refuses_rooms_that_overlap_but_not_rooms_that_touch()
    {
        Room first = Room.FromText("First", "P..\n...");
        Room touching = Room.FromText("Touching", "...", new Vector2I(3, 1));
        Room overlapping = Room.FromText("Overlapping", "...", new Vector2I(2, 1));

        _ = new World("Touch", [first, touching]);
        Assert.Throws<ArgumentException>(() => new World("Overlap", [first, overlapping]));
    }

    [Theory]
    [InlineData("...", "..P", true)] // The only player is in the second room
    [InlineData("...", "...", false)]
    [InlineData("P..", "..P", false)]
    public void Has_exactly_one_player_across_its_rooms(string first, string second, bool valid)
    {
        World Create() => new("Test", [Room.FromText("First", first), Room.FromText("Second", second, new Vector2I(3, 0))]);

        if (valid)
        {
            _ = Create();
        }
        else
        {
            Assert.Throws<ArgumentException>(Create);
        }
    }

    [Fact]
    public void Puts_tiles_and_starts_at_their_rooms_origins_with_solid_tiles_between()
    {
        // Two 3x2 rooms, the second a tile to the right of the first and a tile up.
        Room left = Room.FromText("Left", "#..\n.P.");
        Room right = Room.FromText("Right", "...\nB.#", new Vector2I(4, 1));
        var world = new World("Test", [left, right]);
        TileGrid tiles = world.CreateTiles();

        Assert.Equal((7, 3), (tiles.Width, tiles.Height));
        Assert.True(tiles.IsSolid(0, 1)); // Left's '#'
        Assert.False(tiles.IsSolid(1, 0));
        Assert.True(tiles.IsSolid(6, 1)); // Right's '#'
        Assert.False(tiles.IsSolid(5, 2));
        Assert.True(tiles.IsSolid(3, 1)); // Between the rooms
        Assert.True(tiles.IsSolid(0, 2)); // Above the left room

        Assert.Equal(
            [(EntityKind.Player, new Vector2(1.5f, 0)), (EntityKind.Block, new Vector2(4.5f, 1))],
            world.EntityConfigs.Select(config => (config.Kind, config.Start)));
    }
}
