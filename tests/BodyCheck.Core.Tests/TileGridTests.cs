using BodyCheck.Core.WorldData;

namespace BodyCheck.Core.Tests;

public class TileGridTests
{
    private static TileGrid NewGrid() => TileGrid.FromText(
    [
        "..........#.........",
        "..........#.........",
        "..........#.........",
        "..........#.........",
        "####################",
    ]);

    [Fact]
    public void FromText_puts_the_last_row_at_the_bottom()
    {
        TileGrid grid = NewGrid();

        Assert.True(grid.IsSolid(0, 0));
        Assert.False(grid.IsSolid(0, 1));
        Assert.True(grid.IsSolid(10, 4));
    }

    [Fact]
    public void Outside_the_grid_is_solid()
    {
        TileGrid grid = NewGrid();

        Assert.True(grid.IsSolid(-1, 2));
        Assert.True(grid.IsSolid(20, 2));
        Assert.True(grid.IsSolid(5, 5));
    }
}
