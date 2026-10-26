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

        AssertThat(grid.IsSolid(0, 0)).IsTrue();
        AssertThat(grid.IsSolid(0, 1)).IsFalse();
        AssertThat(grid.IsSolid(10, 4)).IsTrue();
    }

    [TestCase]
    public void Outside_the_grid_is_empty()
    {
        TileGrid grid = NewGrid();

        AssertThat(grid.IsSolid(-1, 2)).IsFalse();
        AssertThat(grid.IsSolid(20, 2)).IsFalse();
        AssertThat(grid.IsSolid(5, 5)).IsFalse();
    }
}
