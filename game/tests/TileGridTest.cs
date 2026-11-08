using BodyCheck.Core.WorldData;

using GdUnit4;

using static GdUnit4.Assertions;

namespace BodyCheck.Tests;

[TestSuite]
public class TileGridTest
{
    private static TileGrid NewGrid() => TileGrid.FromText(
    [
        "..........#.........",
        "..........#.........",
        "..........#.........",
        "..........#.........",
        "####################",
    ]);

    [TestCase]
    public void FromText_puts_the_last_row_at_the_bottom()
    {
        TileGrid grid = NewGrid();

        AssertThat(grid.GetTileIndex(0, 0)).IsEqual(TileIndex.Ground);
        AssertThat(grid.GetTileIndex(0, 1)).IsEqual(TileIndex.Empty);
        AssertThat(grid.GetTileIndex(10, 4)).IsEqual(TileIndex.Ground);
    }

    [TestCase]
    public void Outside_the_grid_is_empty()
    {
        TileGrid grid = NewGrid();

        AssertThat(grid.GetTileIndex(-1, 2)).IsEqual(TileIndex.Empty);
        AssertThat(grid.GetTileIndex(20, 2)).IsEqual(TileIndex.Empty);
        AssertThat(grid.GetTileIndex(5, 5)).IsEqual(TileIndex.Empty);
    }
}
