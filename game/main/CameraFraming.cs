using BodyCheck.Core.WorldData;

using Godot;

namespace BodyCheck;

/// <summary>
/// Chooses where the player's camera looks: the point nearest the wanted framing where its <see cref="Map"/> lets the
/// view's centre be, and where the view shows the player, <see cref="PlayerMargin"/> inside its edges. Where the wanted
/// framing is on the map, it's the wanted framing. See <c>docs/designs/rendering.md</c>, on the camera.
/// <para>
/// Each pixel of the map on which the centre may be stands for its whole cell, so that along the map's straight edges the
/// centre slides smoothly rather than stepping from pixel to pixel. Where no pixel in the box round the player is on the
/// map, the centre is the wanted framing, kept inside the box.
/// </para>
/// </summary>
public sealed class CameraFraming
{
    /// <summary>
    /// How far inside each edge of the view the player's centre is kept, in tiles: far enough that the whole player, 1x2
    /// tiles, shows.
    /// </summary>
    internal const float PlayerMargin = 1;

    public CameraFraming(World world) => Map = new CameraMap(world);

    /// <summary>Where the view's centre may be.</summary>
    public CameraMap Map { get; }

    /// <summary>
    /// The view's centre for a player at <paramref name="player"/> who wants it at <paramref name="wanted"/>, for a view
    /// of <paramref name="halfSize"/> either side of its centre at the tiles' front faces.
    /// </summary>
    public Vector2 Frame(Vector2 player, Vector2 wanted, Vector2 halfSize)
    {
        Vector2 box = (halfSize - new Vector2(PlayerMargin, PlayerMargin)).Max(Vector2.Zero);
        Vector2 boxMin = player - box;
        Vector2 boxMax = player + box;
        Vector2 scale = CameraMap.PixelsPerTile;
        Vector2 cell = Vector2.One / scale;

        Vector2 best = wanted.Clamp(boxMin, boxMax);
        float bestDistance = float.PositiveInfinity;
        int x0 = Mathf.FloorToInt(boxMin.X * scale.X);
        int x1 = Mathf.FloorToInt(boxMax.X * scale.X);
        int y0 = Mathf.FloorToInt(boxMin.Y * scale.Y);
        int y1 = Mathf.FloorToInt(boxMax.Y * scale.Y);
        for (int y = y0; y <= y1; y++)
        {
            for (int x = x0; x <= x1; x++)
            {
                if (!Map.IsValid(x, y))
                {
                    continue;
                }
                // The point of the pixel's cell, within the box, nearest the wanted framing.
                Vector2 min = (new Vector2(x, y) * cell).Max(boxMin);
                Vector2 max = (new Vector2(x + 1, y + 1) * cell).Min(boxMax);
                if (min.X > max.X || min.Y > max.Y)
                {
                    continue;
                }
                Vector2 point = wanted.Clamp(min, max);
                float distance = point.DistanceSquaredTo(wanted);
                if (distance < bestDistance)
                {
                    best = point;
                    bestDistance = distance;
                }
            }
        }
        return best;
    }
}
