using BodyCheck.Core.WorldData;

using Godot;

namespace BodyCheck;

/// <summary>
/// Chooses where the player's camera looks: it moves the view's centre, the target, toward the wanted framing, within
/// the points where its <see cref="Map"/> lets the centre be, keeping the player in view, <see cref="PlayerMargin"/>
/// inside its edges. Since the target moves rather than being placed, it can't jump from one part of the map to
/// another. See <c>docs/designs/rendering.md</c>, on the camera.
/// <list type="bullet">
/// <item>
/// It moves at no more than <see cref="Speed"/>, toward the point of the map next to it that is nearest the wanted
/// framing, so that it slides along the map's edges, and stops where nothing next to it is nearer. In open space, it
/// goes to the wanted framing.
/// </item>
/// <item>
/// The box round the player, where the view shows the player, comes first: it pushes the target, off the map if need
/// be, and off the map, the target moves to the nearest point of it.
/// </item>
/// <item>
/// When it teleports, such as on the first frame, the target is placed at the point of the map nearest the wanted
/// framing.
/// </item>
/// </list>
/// Each pixel of the map stands for its whole cell, so that along the map's straight edges the target slides smoothly
/// rather than stepping from pixel to pixel.
/// </summary>
public sealed class CameraFraming
{
    /// <summary>
    /// How far inside each edge of the view the player's centre is kept, in tiles: far enough that the whole player, 1x2
    /// tiles, shows.
    /// </summary>
    internal const float PlayerMargin = 1;

    /// <summary>How fast the target may move, in tiles per game second: twice the player's top speed.</summary>
    internal const float Speed = 24;

    /// <summary>The most steps from cell to cell the target takes in a frame, as a backstop.</summary>
    private const int MaxSteps = 64;

    private static readonly Vector2 Scale = CameraMap.PixelsPerTile;
    private static readonly Vector2 Cell = Vector2.One / Scale;

    /// <summary>The view's centre as of the last frame; null until the first, or after a teleport.</summary>
    private Vector2? _target;

    public CameraFraming(World world) => Map = new CameraMap(world);

    /// <summary>Where the view's centre may be.</summary>
    public CameraMap Map { get; }

    /// <summary>Places the target afresh on the next frame, rather than moving it.</summary>
    public void Teleport() => _target = null;

    /// <summary>
    /// The view's centre for a player at <paramref name="player"/> who wants it at <paramref name="wanted"/>, for a view
    /// of <paramref name="halfSize"/> either side of its centre at the tiles' front faces,
    /// <paramref name="delta"/> game seconds after the last frame.
    /// </summary>
    public Vector2 Frame(Vector2 player, Vector2 wanted, Vector2 halfSize, float delta)
    {
        Vector2 box = (halfSize - new Vector2(PlayerMargin, PlayerMargin)).Max(Vector2.Zero);
        Vector2 boxMin = player - box;
        Vector2 boxMax = player + box;

        if (_target is not Vector2 target)
        {
            Vector2 placed = Nearest(wanted, boxMin, boxMax) ?? wanted.Clamp(boxMin, boxMax);
            _target = placed;
            return placed;
        }

        target = target.Clamp(boxMin, boxMax);
        float budget = Speed * delta;
        for (int step = 0; step < MaxSteps && budget > 0; step++)
        {
            // Next to the map, toward the wanted framing along it; off it, back onto it by the shortest way.
            Vector2? next = NearestAround(target, wanted, boxMin, boxMax);
            Vector2 goal;
            if (next is Vector2 around)
            {
                if (around.DistanceSquaredTo(wanted) >= target.DistanceSquaredTo(wanted) - 1e-8f)
                {
                    break;
                }
                goal = around;
            }
            else if (Nearest(target, boxMin, boxMax) is Vector2 back)
            {
                goal = back;
            }
            else
            {
                break;
            }
            float distance = target.DistanceTo(goal);
            if (distance <= budget)
            {
                target = goal;
                budget -= distance;
            }
            else
            {
                target += (goal - target) * (budget / distance);
                budget = 0;
            }
        }
        _target = target;
        return target;
    }

    /// <summary>
    /// The point of the map nearest <paramref name="point"/>, within the cell that <paramref name="from"/> is in and
    /// the cells next to it, and within the box from <paramref name="boxMin"/> to <paramref name="boxMax"/>; null if none of them is on the
    /// map.
    /// </summary>
    private Vector2? NearestAround(Vector2 from, Vector2 point, Vector2 boxMin, Vector2 boxMax)
    {
        int x = Mathf.FloorToInt(from.X * Scale.X);
        int y = Mathf.FloorToInt(from.Y * Scale.Y);
        return NearestIn(point, boxMin, boxMax, x - 1, y - 1, x + 1, y + 1);
    }

    /// <summary>
    /// The point of the map nearest <paramref name="point"/> within the box from <paramref name="boxMin"/> to
    /// <paramref name="boxMax"/>; null if none of the box is on the map.
    /// </summary>
    private Vector2? Nearest(Vector2 point, Vector2 boxMin, Vector2 boxMax) =>
        NearestIn(
            point, boxMin, boxMax,
            Mathf.FloorToInt(boxMin.X * Scale.X), Mathf.FloorToInt(boxMin.Y * Scale.Y),
            Mathf.FloorToInt(boxMax.X * Scale.X), Mathf.FloorToInt(boxMax.Y * Scale.Y));

    /// <summary>
    /// The point nearest <paramref name="point"/> in the cells of the map's pixels from (<paramref name="x0"/>,
    /// <paramref name="y0"/>) to (<paramref name="x1"/>, <paramref name="y1"/>), inclusive, that are on the map, within
    /// the box; null if there's none.
    /// </summary>
    private Vector2? NearestIn(Vector2 point, Vector2 boxMin, Vector2 boxMax, int x0, int y0, int x1, int y1)
    {
        Vector2? best = null;
        float bestDistance = float.PositiveInfinity;
        for (int y = y0; y <= y1; y++)
        {
            for (int x = x0; x <= x1; x++)
            {
                if (!Map.IsValid(x, y))
                {
                    continue;
                }
                Vector2 min = (new Vector2(x, y) * Cell).Max(boxMin);
                Vector2 max = (new Vector2(x + 1, y + 1) * Cell).Min(boxMax);
                if (min.X > max.X || min.Y > max.Y)
                {
                    continue;
                }
                Vector2 nearest = point.Clamp(min, max);
                float distance = nearest.DistanceSquaredTo(point);
                if (distance < bestDistance)
                {
                    best = nearest;
                    bestDistance = distance;
                }
            }
        }
        return best;
    }
}
