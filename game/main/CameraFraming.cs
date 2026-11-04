using System;
using System.Collections.Generic;

using BodyCheck.Core.WorldData;

using Godot;

namespace BodyCheck;

/// <summary>
/// Chooses where the player's camera looks: as near the wanted framing as it can be without showing much that the
/// player shouldn't see. See <c>docs/designs/rendering.md</c>, on the camera. Each frame:
/// <list type="number">
/// <item>
/// How hidden each tile near the player is, from 0 to 1, by its detour: how much longer the shortest path to it from
/// the player, through empty tiles, is than a straight line. Open space has no detour, so it shows; space beyond a
/// nearby hole or gap has a small one; space beyond a wall, reached only the long way round or not at all, has a large
/// one. Paths may go into solid tiles but not out again, and each tile past a wall's face costs more, so the faces show
/// and the rock behind them less and less. Outside the world's rooms is hidden.
/// </item>
/// <item>
/// The cost of each candidate centre: how much hidden area its view would show, plus <see cref="Pull"/> times the
/// square of its distance from the wanted framing. The hidden area in a rectangle comes from a summed-area table.
/// </item>
/// <item>
/// A soft minimum of the candidates: their average, each weighted by <c>exp(-cost / Softness)</c>, so that the centre
/// moves smoothly as the player and the costs do.
/// </item>
/// </list>
/// </summary>
public sealed class CameraFraming
{
    /// <summary>A tile's detour, in tiles, below which it isn't hidden at all.</summary>
    internal const float FreeDetour = 4;
    /// <summary>A tile's detour, in tiles, above which it's fully hidden.</summary>
    internal const float HiddenDetour = 14;
    /// <summary>The length of a step from one solid tile to the next, in tiles, which makes deep rock hidden.</summary>
    internal const float RockStep = 6;
    /// <summary>How strongly the view is pulled toward the wanted framing: the cost, in tiles of hidden area, of being a tile away from it, squared.</summary>
    internal const float Pull = 0.5f;
    /// <summary>The temperature of the soft minimum, in tiles of hidden area.</summary>
    internal const float Softness = 1;
    /// <summary>How far from the wanted framing a candidate centre may be, in tiles, on each axis.</summary>
    internal const float Reach = 8;
    /// <summary>The spacing of the candidate centres, in tiles.</summary>
    private const float CandidateStep = 0.5f;
    /// <summary>How far past the candidates' views paths may go, in tiles, to get round things.</summary>
    private const int PathMargin = 4;

    private static readonly float Diagonal = MathF.Sqrt(2);

    private enum Tile : byte
    {
        Empty,
        Solid,
        /// <summary>Outside the world's rooms: always hidden, and no path goes through it.</summary>
        Outside,
    }

    private readonly Tile[,] _tiles;
    private readonly int _width;
    private readonly int _height;

    // Buffers for each frame's window of tiles around the wanted framing, kept between frames.
    private float[,] _distance = new float[0, 0];
    private float[,] _hidden = new float[0, 0];
    private double[,] _summed = new double[0, 0];
    private readonly PriorityQueue<(int X, int Y), float> _queue = new();

    public CameraFraming(World world)
    {
        _width = world.Size.X;
        _height = world.Size.Y;
        _tiles = new Tile[_width, _height];
        for (int x = 0; x < _width; x++)
        {
            for (int y = 0; y < _height; y++)
            {
                _tiles[x, y] = Tile.Outside;
            }
        }
        foreach (Room room in world.Rooms)
        {
            for (int x = room.Bounds.Position.X; x < room.Bounds.End.X; x++)
            {
                for (int y = room.Bounds.Position.Y; y < room.Bounds.End.Y; y++)
                {
                    _tiles[x, y] = world.IsSolid(x, y) ? Tile.Solid : Tile.Empty;
                }
            }
        }
    }

    /// <summary>
    /// The view's centre for a player at <paramref name="eye"/> who wants it at <paramref name="wanted"/>, for a view
    /// of <paramref name="halfSize"/> either side of its centre at the tiles' front faces.
    /// </summary>
    public Vector2 Frame(Vector2 eye, Vector2 wanted, Vector2 halfSize)
    {
        // The window: every tile a candidate's view can show, and a margin for paths.
        int x0 = Mathf.FloorToInt(wanted.X - Reach - halfSize.X) - PathMargin;
        int y0 = Mathf.FloorToInt(wanted.Y - Reach - halfSize.Y) - PathMargin;
        int x1 = Mathf.CeilToInt(wanted.X + Reach + halfSize.X) + PathMargin;
        int y1 = Mathf.CeilToInt(wanted.Y + Reach + halfSize.Y) + PathMargin;
        int w = x1 - x0;
        int h = y1 - y0;

        FindDistances(eye, x0, y0, w, h);
        SumHidden(eye, x0, y0, w, h);

        // The soft minimum, on a lattice of candidates centred on the wanted framing, so that it moves with it.
        int n = (int)(Reach / CandidateStep);
        Span<float> costs = stackalloc float[(2 * n + 1) * (2 * n + 1)];
        float least = float.MaxValue;
        for (int i = -n, k = 0; i <= n; i++)
        {
            for (int j = -n; j <= n; j++, k++)
            {
                var offset = new Vector2(i, j) * CandidateStep;
                Vector2 centre = wanted + offset;
                float hidden = HiddenArea(centre - halfSize - new Vector2(x0, y0), centre + halfSize - new Vector2(x0, y0), w, h);
                costs[k] = hidden + Pull * offset.LengthSquared();
                least = Math.Min(least, costs[k]);
            }
        }
        Vector2 sum = Vector2.Zero;
        float weights = 0;
        for (int i = -n, k = 0; i <= n; i++)
        {
            for (int j = -n; j <= n; j++, k++)
            {
                float weight = MathF.Exp(-(costs[k] - least) / Softness);
                sum += weight * new Vector2(i, j) * CandidateStep;
                weights += weight;
            }
        }
        return wanted + sum / weights;
    }

    private Tile TileAt(int x, int y) =>
        x < 0 || y < 0 || x >= _width || y >= _height ? Tile.Outside : _tiles[x, y];

    /// <summary>
    /// The length of the shortest path from <paramref name="eye"/> to each tile of the window, by Dijkstra's algorithm,
    /// with infinity where there's none. It starts from the empty tiles around the eye, each at its straight-line
    /// distance, so that the distances change smoothly as the eye moves.
    /// </summary>
    private void FindDistances(Vector2 eye, int x0, int y0, int w, int h)
    {
        if (_distance.GetLength(0) < w || _distance.GetLength(1) < h)
        {
            _distance = new float[w, h];
        }
        for (int i = 0; i < w; i++)
        {
            for (int j = 0; j < h; j++)
            {
                _distance[i, j] = float.PositiveInfinity;
            }
        }

        _queue.Clear();
        int ex = Mathf.FloorToInt(eye.X);
        int ey = Mathf.FloorToInt(eye.Y);
        for (int x = ex - 1; x <= ex + 1; x++)
        {
            for (int y = ey - 1; y <= ey + 1; y++)
            {
                int i = x - x0;
                int j = y - y0;
                if (i >= 0 && j >= 0 && i < w && j < h && TileAt(x, y) == Tile.Empty)
                {
                    _distance[i, j] = Octile(new Vector2(x + 0.5f, y + 0.5f) - eye);
                    _queue.Enqueue((i, j), _distance[i, j]);
                }
            }
        }

        while (_queue.TryDequeue(out (int I, int J) at, out float distance))
        {
            if (distance > _distance[at.I, at.J])
            {
                continue; // Already shortened since this was queued.
            }
            Tile from = TileAt(at.I + x0, at.J + y0);
            for (int di = -1; di <= 1; di++)
            {
                for (int dj = -1; dj <= 1; dj++)
                {
                    int i = at.I + di;
                    int j = at.J + dj;
                    if ((di == 0 && dj == 0) || i < 0 || j < 0 || i >= w || j >= h)
                    {
                        continue;
                    }
                    Tile to = TileAt(i + x0, j + y0);
                    bool diagonal = di != 0 && dj != 0;
                    float step;
                    if (to == Tile.Outside || (from == Tile.Solid && to == Tile.Empty))
                    {
                        continue; // Nothing goes through the outside, or out of rock.
                    }
                    else if (to == Tile.Empty)
                    {
                        // Diagonally only between two empty tiles, so as not to cut corners.
                        if (diagonal && (TileAt(at.I + x0 + di, at.J + y0) != Tile.Empty || TileAt(at.I + x0, at.J + y0 + dj) != Tile.Empty))
                        {
                            continue;
                        }
                        step = diagonal ? Diagonal : 1;
                    }
                    else if (diagonal)
                    {
                        continue; // Into and through rock, only straight.
                    }
                    else
                    {
                        step = from == Tile.Solid ? RockStep : 1;
                    }

                    float shorter = distance + step;
                    if (shorter < _distance[i, j])
                    {
                        _distance[i, j] = shorter;
                        _queue.Enqueue((i, j), shorter);
                    }
                }
            }
        }
    }

    /// <summary>
    /// How hidden each tile of the window is, and its summed-area table. A solid tile next to an empty one is a face of
    /// that space, so it's no more hidden than the least hidden of its empty neighbours, whichever side they're on.
    /// </summary>
    private void SumHidden(Vector2 eye, int x0, int y0, int w, int h)
    {
        if (_hidden.GetLength(0) < w || _hidden.GetLength(1) < h)
        {
            _hidden = new float[w, h];
            _summed = new double[w + 1, h + 1];
        }
        for (int i = 0; i < w; i++)
        {
            for (int j = 0; j < h; j++)
            {
                float distance = _distance[i, j];
                float detour = distance - Octile(new Vector2(i + x0 + 0.5f, j + y0 + 0.5f) - eye);
                _hidden[i, j] = float.IsFinite(distance)
                    ? Mathf.Clamp((detour - FreeDetour) / (HiddenDetour - FreeDetour), 0, 1)
                    : 1;
            }
        }
        for (int i = 0; i < w; i++)
        {
            for (int j = 0; j < h; j++)
            {
                if (TileAt(i + x0, j + y0) != Tile.Solid)
                {
                    continue;
                }
                foreach ((int di, int dj) in (ReadOnlySpan<(int, int)>)[(-1, 0), (1, 0), (0, -1), (0, 1)])
                {
                    int ni = i + di;
                    int nj = j + dj;
                    if (ni >= 0 && nj >= 0 && ni < w && nj < h && TileAt(ni + x0, nj + y0) == Tile.Empty)
                    {
                        _hidden[i, j] = Math.Min(_hidden[i, j], _hidden[ni, nj]);
                    }
                }
            }
        }

        for (int i = 0; i <= w; i++)
        {
            _summed[i, 0] = 0;
        }
        for (int j = 0; j <= h; j++)
        {
            _summed[0, j] = 0;
        }
        for (int i = 0; i < w; i++)
        {
            for (int j = 0; j < h; j++)
            {
                _summed[i + 1, j + 1] = _hidden[i, j] + _summed[i, j + 1] + _summed[i + 1, j] - _summed[i, j];
            }
        }
    }

    /// <summary>
    /// The hidden area in the rectangle from <paramref name="min"/> to <paramref name="max"/>, in the window's
    /// coordinates. The table interpolated bilinearly is exact, since each tile's value is constant over it.
    /// </summary>
    private float HiddenArea(Vector2 min, Vector2 max, int w, int h) =>
        (float)(Summed(max.X, max.Y, w, h) - Summed(min.X, max.Y, w, h) - Summed(max.X, min.Y, w, h) + Summed(min.X, min.Y, w, h));

    private double Summed(float u, float v, int w, int h)
    {
        u = Math.Clamp(u, 0, w);
        v = Math.Clamp(v, 0, h);
        int i = Math.Min((int)u, w - 1);
        int j = Math.Min((int)v, h - 1);
        double fu = u - i;
        double fv = v - j;
        return (1 - fu) * (1 - fv) * _summed[i, j] + fu * (1 - fv) * _summed[i + 1, j]
            + (1 - fu) * fv * _summed[i, j + 1] + fu * fv * _summed[i + 1, j + 1];
    }

    /// <summary>The length of the shortest path along the grid's straight and diagonal steps, in open space.</summary>
    private static float Octile(Vector2 d)
    {
        float a = MathF.Abs(d.X);
        float b = MathF.Abs(d.Y);
        return Math.Max(a, b) + (Diagonal - 1) * Math.Min(a, b);
    }
}
