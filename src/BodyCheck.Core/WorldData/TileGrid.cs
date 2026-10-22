using Godot;

namespace BodyCheck.Core.WorldData;

public interface ITileGrid
{
    Vector2I Origin { get; }
    Vector2I Size { get; }
    Rect2I Bounds { get; }
    bool IsSolid(int x, int y);
}

/// <summary>
/// A grid of unit tiles. Tile (x, y) covers [x, x+1] × [y, y+1], with y up and row 0 at the bottom.
/// Everything outside the grid is solid.
/// </summary>
public sealed class TileGrid : ITileGrid
{
    private readonly bool[,] _solid;

    public TileGrid(int width, int height)
    {
        Size = new Vector2I(width, height);
        _solid = new bool[width, height];
    }

    public Vector2I Origin { get; } = Vector2I.Zero;
    public Vector2I Size { get; }
    public Rect2I Bounds => new(Origin, Size);
    public bool IsSolid(int x, int y) => x >= 0 && y >= 0 && x < Size.X && y < Size.Y && _solid[x, y];

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
