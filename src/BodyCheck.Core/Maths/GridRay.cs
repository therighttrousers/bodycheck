using Godot;

namespace BodyCheck.Core.Maths;

/// <summary>Rays cast through a grid of unit cells, such as tiles, each cell <c>(x, y)</c> from <c>(x, y)</c> to <c>(x + 1, y + 1)</c>.</summary>
public static class GridRay
{
    /// <summary>
    /// The point where the line from <paramref name="from"/> to <paramref name="to"/> first enters a cell that
    /// <paramref name="hits"/> is true of, or null if it enters none. The cell that <paramref name="from"/> is in doesn't
    /// count, since the line doesn't enter it, so whether it starts inside one is the caller's question; the cell that
    /// <paramref name="to"/> is in does. It walks the cells the line passes through, one step to a side at a time, as in
    /// Amanatides and Woo's traversal.
    /// </summary>
    public static Vector2? Cast(Vector2 from, Vector2 to, Func<int, int, bool> hits)
    {
        int x = Mathf.FloorToInt(from.X);
        int y = Mathf.FloorToInt(from.Y);
        int endX = Mathf.FloorToInt(to.X);
        int endY = Mathf.FloorToInt(to.Y);
        Vector2 d = to - from;
        int stepX = Math.Sign(endX - x);
        int stepY = Math.Sign(endY - y);
        // How far along the line, from 0 to 1, each step to a side is, and where the next one is.
        float deltaX = stepX == 0 ? float.PositiveInfinity : 1 / MathF.Abs(d.X);
        float deltaY = stepY == 0 ? float.PositiveInfinity : 1 / MathF.Abs(d.Y);
        float nextX = stepX > 0 ? (x + 1 - from.X) * deltaX : stepX < 0 ? (from.X - x) * deltaX : float.PositiveInfinity;
        float nextY = stepY > 0 ? (y + 1 - from.Y) * deltaY : stepY < 0 ? (from.Y - y) * deltaY : float.PositiveInfinity;

        // Exactly as many steps as it takes to reach the last cell, and none along an axis once it's there, so that
        // rounding can't take it past.
        int steps = Math.Abs(endX - x) + Math.Abs(endY - y);
        for (int n = 0; n < steps; n++)
        {
            float t;
            if (y == endY || (x != endX && nextX <= nextY))
            {
                x += stepX;
                t = nextX;
                nextX += deltaX;
            }
            else
            {
                y += stepY;
                t = nextY;
                nextY += deltaY;
            }
            if (hits(x, y))
            {
                return from + d * MathF.Min(t, 1);
            }
        }
        return null;
    }
}
