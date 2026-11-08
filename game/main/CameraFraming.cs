using System;

using BodyCheck.Core.WorldData;

using Godot;

namespace BodyCheck;

/// <summary>
/// Chooses where the player's camera looks: as near the wanted framing as it can be without showing the camera
/// boundary, which is the tiles the rooms mark for the camera to keep out of view, and everything outside the rooms. See
/// <c>docs/designs/rendering.md</c>, on the camera.
/// <list type="bullet">
/// <item>
/// The cost of a candidate centre is how much of the boundary its view would show, as an area in tiles, plus
/// <see cref="Pull"/> times the square of its distance from the wanted framing. The view is taken a
/// <see cref="Margin"/> smaller on each side, so that a painted wall's face can show.
/// </item>
/// <item>
/// The centre is a soft minimum of the candidates, which are on a lattice fixed in the world, as far round the wanted
/// framing as <see cref="Reach"/> says: their average, each weighted by <c>exp(-cost / Softness)</c>, so that it moves
/// smoothly as the wanted framing does.
/// </item>
/// </list>
/// The boundary doesn't change, so its summed-area table is computed once, and the area of it in any rectangle is four
/// reads of the table. Where the boundary is far from the view, the pull alone weighs the candidates, symmetrically, so
/// the centre is the wanted framing. Near it, the centre eases away from it over about <c>sqrt(Softness / 2 Pull)</c>
/// tiles, 1 tile as tuned, and stops about at the margin, since each row or column of the boundary in view costs much
/// more than <see cref="Softness"/>.
/// </summary>
public sealed class CameraFraming
{
    /// <summary>How much of the boundary the view may show at each edge without cost, in tiles.</summary>
    internal const float Margin = 1;
    /// <summary>How strongly the view is pulled toward the wanted framing: the cost, in tiles of area, of being a tile away from it, squared.</summary>
    internal const float Pull = 0.25f;
    /// <summary>The temperature of the soft minimum, in tiles of area.</summary>
    internal const float Softness = 0.5f;
    /// <summary>
    /// How far from the wanted framing a candidate centre may be, past the view's half size, in tiles, on each axis: as
    /// far as the wanted framing goes from the player, so that a view showing the player is always a candidate.
    /// </summary>
    internal const float Reach = 6;
    /// <summary>The spacing of the candidate centres, in tiles.</summary>
    private const float CandidateStep = 0.5f;
    /// <summary>
    /// How far past the world, in tiles, the table goes, all of it boundary: farther than any candidate's view can reach
    /// at any reasonable aspect ratio.
    /// </summary>
    private const int Padding = 64;

    /// <summary>
    /// The summed-area table: at (i, j), the area of the boundary in the rectangle from the table's corner, at
    /// (-<see cref="Padding"/>, -<see cref="Padding"/>) in the world, to i, j tiles from it.
    /// </summary>
    private readonly double[,] _summed;

    /// <summary>Each candidate's cost, kept between frames so as not to allocate.</summary>
    private float[] _costs = [];

    public CameraFraming(World world)
    {
        int w = world.Size.X + 2 * Padding;
        int h = world.Size.Y + 2 * Padding;

        var inRoom = new bool[world.Size.X, world.Size.Y];
        foreach (Room room in world.Rooms)
        {
            for (int x = room.Bounds.Position.X; x < room.Bounds.End.X; x++)
            {
                for (int y = room.Bounds.Position.Y; y < room.Bounds.End.Y; y++)
                {
                    inRoom[x, y] = true;
                }
            }
        }
        bool IsBoundary(int x, int y) =>
            x < 0 || y < 0 || x >= world.Size.X || y >= world.Size.Y || !inRoom[x, y] || world.IsCameraBoundary(x, y);

        _summed = new double[w + 1, h + 1];
        for (int i = 0; i < w; i++)
        {
            for (int j = 0; j < h; j++)
            {
                double value = IsBoundary(i - Padding, j - Padding) ? 1 : 0;
                _summed[i + 1, j + 1] = value + _summed[i, j + 1] + _summed[i + 1, j] - _summed[i, j];
            }
        }
    }

    /// <summary>
    /// The view's centre for a player who wants it at <paramref name="wanted"/>, for a view of
    /// <paramref name="halfSize"/> either side of its centre at the tiles' front faces.
    /// </summary>
    public Vector2 Frame(Vector2 wanted, Vector2 halfSize)
    {
        Vector2 inner = (halfSize - new Vector2(Margin, Margin)).Max(Vector2.Zero);

        // The lattice is fixed in the world, so that each candidate's area of the boundary stays the same as the wanted
        // framing moves, and only the pull changes, smoothly. A lattice that moved with the wanted framing would sweep
        // a wall's sharp edge across its candidates, and the centre would move in small steps.
        Vector2 reach = halfSize + new Vector2(Reach, Reach);
        int i0 = Mathf.CeilToInt((wanted.X - reach.X) / CandidateStep);
        int i1 = Mathf.FloorToInt((wanted.X + reach.X) / CandidateStep);
        int j0 = Mathf.CeilToInt((wanted.Y - reach.Y) / CandidateStep);
        int j1 = Mathf.FloorToInt((wanted.Y + reach.Y) / CandidateStep);
        int count = (i1 - i0 + 1) * (j1 - j0 + 1);
        if (_costs.Length < count)
        {
            _costs = new float[count];
        }
        Span<float> costs = _costs;
        float least = float.MaxValue;
        for (int i = i0, k = 0; i <= i1; i++)
        {
            for (int j = j0; j <= j1; j++, k++)
            {
                Vector2 centre = new Vector2(i, j) * CandidateStep;
                costs[k] = BoundaryArea(centre - inner, centre + inner) + Pull * centre.DistanceSquaredTo(wanted);
                least = Math.Min(least, costs[k]);
            }
        }

        // Summed from the wanted framing, for precision far from the origin.
        Vector2 sum = Vector2.Zero;
        float weights = 0;
        for (int i = i0, k = 0; i <= i1; i++)
        {
            for (int j = j0; j <= j1; j++, k++)
            {
                // Less the least, so that the weights don't all underflow.
                float weight = MathF.Exp(-(costs[k] - least) / Softness);
                sum += weight * (new Vector2(i, j) * CandidateStep - wanted);
                weights += weight;
            }
        }
        return wanted + sum / weights;
    }

    /// <summary>The area of the boundary in the rectangle from <paramref name="min"/> to <paramref name="max"/>, in the world.</summary>
    private float BoundaryArea(Vector2 min, Vector2 max) =>
        (float)(Summed(max.X, max.Y) - Summed(min.X, max.Y) - Summed(max.X, min.Y) + Summed(min.X, min.Y));

    /// <summary>
    /// The table at a point in the world, interpolated bilinearly, which is exact: each tile's value is constant over
    /// it, so the area from the corner to a point within a tile is bilinear in the point.
    /// </summary>
    private double Summed(float x, float y)
    {
        int w = _summed.GetLength(0) - 1;
        int h = _summed.GetLength(1) - 1;
        float u = Math.Clamp(x + Padding, 0, w);
        float v = Math.Clamp(y + Padding, 0, h);
        int i = Math.Min((int)u, w - 1);
        int j = Math.Min((int)v, h - 1);
        double fu = u - i;
        double fv = v - j;
        return (1 - fu) * (1 - fv) * _summed[i, j] + fu * (1 - fv) * _summed[i + 1, j]
            + (1 - fu) * fv * _summed[i, j + 1] + fu * fv * _summed[i + 1, j + 1];
    }
}
