namespace BodyCheck.Core;

/// <summary>
/// A grid of unit tiles. Tile (x, y) covers [x, x+1] × [y, y+1], with y up and row 0 at the bottom.
/// Everything outside the grid is solid.
/// </summary>
public sealed class TileGrid
{
    private readonly bool[,] _solid;

    public TileGrid(int width, int height)
    {
        Width = width;
        Height = height;
        _solid = new bool[width, height];
    }

    public int Width { get; }
    public int Height { get; }

    public bool IsSolid(int x, int y) =>
        x < 0 || y < 0 || x >= Width || y >= Height || _solid[x, y];

    public void SetSolid(int x, int y, bool solid) => _solid[x, y] = solid;

    /// <summary>
    /// Builds a grid from text rows, top row first. '#' is solid; anything else is empty.
    /// </summary>
    public static TileGrid FromText(IReadOnlyList<string> rows)
    {
        var grid = new TileGrid(rows.Max(row => row.Length), rows.Count);
        for (int row = 0; row < rows.Count; row++)
        {
            for (int x = 0; x < rows[row].Length; x++)
            {
                grid._solid[x, rows.Count - 1 - row] = rows[row][x] == '#';
            }
        }
        return grid;
    }
}
