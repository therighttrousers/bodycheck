using System;
using System.Collections.Generic;

using BodyCheck.Core.WorldData;

using Godot;

namespace BodyCheck;

/// <summary>
/// Where the view's centre may be, for a view of 4:3, on a grid of pixels finer than the tiles: the points from which
/// the view shows no more of the camera boundary than its margin, and where no view would, a skeleton of the space,
/// where it shows the least, evenly on each side. The camera boundary is the tiles the rooms mark for the camera to keep
/// out of view, and everything outside the rooms. See <c>docs/designs/rendering.md</c>, on the camera.
/// <list type="number">
/// <item>
/// The space that isn't boundary is scaled up by <see cref="PixelsPerTile"/>, 3 across and 4 up and down, so that a
/// view of 20x15 tiles is 60x60 pixels, and thinning, which takes a pixel off each side at each step, eats into the
/// space at the view's own aspect.
/// </item>
/// <item>
/// It is thinned <see cref="ThinSteps"/> steps, by Zhang and Suen's thinning, half the view's size in pixels. Where the
/// view fits, what's left is where it shows none of the boundary. Thinning keeps every connection and every end, so
/// where it doesn't fit, a line, one or two pixels thick, is left along the middle of the space.
/// </item>
/// <item>
/// It is grown by the margin, <see cref="MarginTiles"/>, so that the view may show a painted wall's face, and the lines
/// are thickened.
/// </item>
/// </list>
/// </summary>
public sealed class CameraMap
{
    /// <summary>How many pixels each tile is, across and up and down.</summary>
    internal static readonly Vector2I PixelsPerTile = new(3, 4);

    /// <summary>How many steps the space is thinned, in pixels at each side: half of a view of 20x15 tiles.</summary>
    internal const int ThinSteps = 30;

    /// <summary>How far the thinned space is grown on each side, in tiles, so that the view may show that much boundary.</summary>
    internal const int MarginTiles = 1;

    /// <summary>Whether the view's centre may be at each pixel, row by row from the bottom.</summary>
    private readonly bool[] _valid;

    /// <summary>The camera boundary, in the world's tiles.</summary>
    private readonly bool[,] _boundary;

    /// <summary>The map from <paramref name="world"/>'s camera boundary.</summary>
    public CameraMap(World world)
    {
        _boundary = Boundary(world);
        Width = world.Size.X * PixelsPerTile.X;
        Height = world.Size.Y * PixelsPerTile.Y;
        _valid = new bool[Width * Height];
        for (int y = 0; y < Height; y++)
        {
            for (int x = 0; x < Width; x++)
            {
                _valid[y * Width + x] = !_boundary[x / PixelsPerTile.X, y / PixelsPerTile.Y];
            }
        }
        for (int step = 0; step < ThinSteps; step++)
        {
            // Both halves, even if the first removes nothing.
            bool thinned = Thin(firstHalf: true);
            if (!(Thin(firstHalf: false) | thinned))
            {
                break;
            }
        }
        Grow(MarginTiles * PixelsPerTile.X, 1, 0);
        Grow(MarginTiles * PixelsPerTile.Y, 0, 1);
    }

    /// <summary>The map's width, in pixels.</summary>
    public int Width { get; }

    /// <summary>The map's height, in pixels.</summary>
    public int Height { get; }

    /// <summary>
    /// Whether the view's centre may be in pixel (<paramref name="x"/>, <paramref name="y"/>), counted from the world's
    /// bottom left corner; false outside the world. A pixel stands for the point at its centre.
    /// </summary>
    public bool IsValid(int x, int y) => x >= 0 && y >= 0 && x < Width && y < Height && _valid[y * Width + x];

    /// <summary>
    /// Whether tile (<paramref name="x"/>, <paramref name="y"/>) is camera boundary: marked for the camera to keep out of
    /// view, or in no room, or outside the world.
    /// </summary>
    public bool IsBoundary(int x, int y) =>
        x < 0 || y < 0 || x >= _boundary.GetLength(0) || y >= _boundary.GetLength(1) || _boundary[x, y];

    /// <summary>
    /// The camera boundary, in <paramref name="world"/>'s tiles: the tiles its rooms mark for the camera to keep out of
    /// view, and those in no room.
    /// </summary>
    private static bool[,] Boundary(World world)
    {
        var boundary = new bool[world.Size.X, world.Size.Y];
        for (int x = 0; x < world.Size.X; x++)
        {
            for (int y = 0; y < world.Size.Y; y++)
            {
                boundary[x, y] = true;
            }
        }
        foreach (Room room in world.Rooms)
        {
            for (int x = room.Bounds.Position.X; x < room.Bounds.End.X; x++)
            {
                for (int y = room.Bounds.Position.Y; y < room.Bounds.End.Y; y++)
                {
                    boundary[x, y] = world.IsCameraBoundary(x, y);
                }
            }
        }
        return boundary;
    }

    /// <summary>
    /// One half of a step of Zhang and Suen's thinning: removes, all at once, each pixel on the edge of the space that can
    /// go without cutting the space or shortening a line. The first half takes pixels off the right and the bottom, and
    /// the top left corners; the second, off the left and the top, and the bottom right corners. Whether it removed any.
    /// </summary>
    private bool Thin(bool firstHalf)
    {
        var removed = new List<int>();
        for (int y = 0; y < Height; y++)
        {
            for (int x = 0; x < Width; x++)
            {
                if (!_valid[y * Width + x])
                {
                    continue;
                }
                // The neighbours, clockwise from the one above.
                bool up = IsValid(x, y + 1);
                bool upRight = IsValid(x + 1, y + 1);
                bool right = IsValid(x + 1, y);
                bool downRight = IsValid(x + 1, y - 1);
                bool down = IsValid(x, y - 1);
                bool downLeft = IsValid(x - 1, y - 1);
                bool left = IsValid(x - 1, y);
                bool upLeft = IsValid(x - 1, y + 1);
                ReadOnlySpan<bool> around = [up, upRight, right, downRight, down, downLeft, left, upLeft];

                // With fewer than 2 neighbours, it's the end of a line; with more than 6, inside the space.
                int neighbours = 0;
                // Where it has more than one run of neighbours, removing it would cut the space.
                int runs = 0;
                for (int i = 0; i < around.Length; i++)
                {
                    neighbours += around[i] ? 1 : 0;
                    runs += !around[i] && around[(i + 1) % around.Length] ? 1 : 0;
                }
                if (neighbours is < 2 or > 6 || runs != 1)
                {
                    continue;
                }
                bool onEdge = firstHalf
                    ? !(up && right && down) && !(right && down && left)
                    : !(up && right && left) && !(up && down && left);
                if (onEdge)
                {
                    removed.Add(y * Width + x);
                }
            }
        }
        foreach (int i in removed)
        {
            _valid[i] = false;
        }
        return removed.Count > 0;
    }

    /// <summary>Grows the space by <paramref name="pixels"/> on both sides along (<paramref name="dx"/>, <paramref name="dy"/>).</summary>
    private void Grow(int pixels, int dx, int dy)
    {
        bool[] before = (bool[])_valid.Clone();
        for (int y = 0; y < Height; y++)
        {
            for (int x = 0; x < Width; x++)
            {
                if (!before[y * Width + x])
                {
                    continue;
                }
                for (int d = -pixels; d <= pixels; d++)
                {
                    int gx = x + d * dx;
                    int gy = y + d * dy;
                    if (gx >= 0 && gy >= 0 && gx < Width && gy < Height)
                    {
                        _valid[gy * Width + gx] = true;
                    }
                }
            }
        }
    }
}
