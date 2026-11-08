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
/// The centre is a soft minimum of the candidates, which are on a lattice fixed in the world, wherever the view shows
/// the player: their average, each weighted by <c>exp(-cost / Softness)</c>, so that it moves smoothly as the player
/// and the wanted framing do.
/// </item>
/// </list>
/// The boundary doesn't change, so its summed-area table is computed once, and the area of it in any rectangle is four
/// reads of the table. Where the boundary is far from the view, the pull alone weighs the candidates, symmetrically, so
/// the centre is the wanted framing. Near it, the centre eases away from it over about <c>sqrt(Softness / 2 Pull)</c>
/// tiles, about 2 as tuned, and stops within the margin, since each row or column of the boundary in view costs several
/// times <see cref="Softness"/>. The box round the player trims the pull on the side away from the player, so the
/// centre is drawn a little toward the player when the wanted framing is far from it.
/// </summary>
public sealed class CameraFraming
{
    /// <summary>How much of the boundary the view may show at each edge without cost, in tiles.</summary>
    internal const float Margin = 1;
    /// <summary>How strongly the view is pulled toward the wanted framing: the cost, in tiles of area, of being a tile away from it, squared.</summary>
    internal const float Pull = 0.5f;
    /// <summary>The temperature of the soft minimum, in tiles of area.</summary>
    internal const float Softness = 5f;
    /// <summary>
    /// How far inside each edge of the view the player's centre is kept, in tiles: far enough that the whole player, 1x2
    /// tiles, shows.
    /// </summary>
    internal const float PlayerMargin = 1;
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
    /// The view's centre for a player at <paramref name="player"/> who wants it at <paramref name="wanted"/>, for a view
    /// of <paramref name="halfSize"/> either side of its centre at the tiles' front faces. The view always shows the
    /// player, <see cref="PlayerMargin"/> inside its edges.
    /// </summary>
    public Vector2 Frame(Vector2 player, Vector2 wanted, Vector2 halfSize)
    {
        Vector2 inner = (halfSize - new Vector2(Margin, Margin)).Max(Vector2.Zero);

        // The candidates are the centres whose views show the player. The lattice is fixed in the world, so that each
        // candidate's area of the boundary stays the same as the player moves, and only the pull changes, smoothly. A
        // lattice that moved with the wanted framing would sweep a wall's sharp edge across its candidates, and the
        // centre would move in small steps. Each candidate stands for the cell of the lattice round it, and is weighted
        // by how much of its cell is in the box, so that it fades in and out as the box moves over it.
        Vector2 box = (halfSize - new Vector2(PlayerMargin, PlayerMargin)).Max(Vector2.Zero);
        Vector2 boxMin = player - box;
        Vector2 boxMax = player + box;
        int i0 = Mathf.RoundToInt(boxMin.X / CandidateStep);
        int i1 = Mathf.RoundToInt(boxMax.X / CandidateStep);
        int j0 = Mathf.RoundToInt(boxMin.Y / CandidateStep);
        int j1 = Mathf.RoundToInt(boxMax.Y / CandidateStep);
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
                if (InBox(centre.X, boxMin.X, boxMax.X) > 0 && InBox(centre.Y, boxMin.Y, boxMax.Y) > 0)
                {
                    least = Math.Min(least, costs[k]);
                }
            }
        }

        // Summed from the player, for precision far from the origin.
        Vector2 sum = Vector2.Zero;
        float weights = 0;
        for (int i = i0, k = 0; i <= i1; i++)
        {
            for (int j = j0; j <= j1; j++, k++)
            {
                Vector2 centre = new Vector2(i, j) * CandidateStep;
                // Less the least of those in the box, so that the weights don't all underflow.
                float weight = MathF.Exp(-(costs[k] - least) / Softness)
                    * InBox(centre.X, boxMin.X, boxMax.X) * InBox(centre.Y, boxMin.Y, boxMax.Y);
                sum += weight * (centre - player);
                weights += weight;
            }
        }
        return player + sum / weights;
    }

    /// <summary>
    /// How much of the lattice's cell round <paramref name="centre"/>, along one axis, is between
    /// <paramref name="min"/> and <paramref name="max"/>, from 0 to 1.
    /// </summary>
    private static float InBox(float centre, float min, float max)
    {
        float half = CandidateStep / 2;
        return Math.Clamp((Math.Min(centre + half, max) - Math.Max(centre - half, min)) / CandidateStep, 0, 1);
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
