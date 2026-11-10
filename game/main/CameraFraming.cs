using System;

using BodyCheck.Core;
using BodyCheck.Core.Maths;
using BodyCheck.Core.WorldData;

using Godot;

namespace BodyCheck;

/// <summary>
/// Chooses where the player's camera looks, keeping the player in view, <see cref="PlayerMargin"/> inside its edges.
/// See <c>docs/designs/rendering.md</c>, on the camera.
/// <list type="bullet">
/// <item>
/// Each frame, the goal is the point nearest the wanted framing where the <see cref="Map"/> lets the view's centre be.
/// As the wanted framing moves, the goal may jump, across a gap in the map or round a corner of it. A jump that would
/// cross the camera boundary, which no view should see through, is refused: the goal is then the nearest point that
/// can be seen from where it was.
/// </item>
/// <item>
/// The target follows the goal like a critically damped spring, which may take it off the map for a while. After a
/// jump, its smoothing time is long enough that it pans at about <see cref="Features.CameraPanSpeed"/> at most, and
/// then shortens toward <see cref="Features.CameraMinSmoothTime"/>, so that a pan speeds up toward its end.
/// </item>
/// </list>
/// Each pixel of the map stands for its whole cell, so that along the map's straight edges the goal slides smoothly
/// rather than stepping from pixel to pixel.
/// </summary>
public sealed class CameraFraming
{
    /// <summary>
    /// How far inside each edge of the view the player's centre is kept, in tiles: far enough that the whole player, 1x2
    /// tiles, shows.
    /// </summary>
    internal const float PlayerMargin = 1;

    /// <summary>The smoothing time, in game seconds, of the smoothing time's own shortening toward its least after a jump.</summary>
    private const float SmoothTimeShortening = 0.5f;

    private static readonly Vector2 Scale = CameraMap.PixelsPerTile;
    private static readonly Vector2 Cell = Vector2.One / Scale;

    /// <summary>The target as of the last frame; null until the first.</summary>
    private Vector2? _target;
    private Vector2 _velocity;
    /// <summary>The goal as of the last frame.</summary>
    private Vector2 _goal;
    private float _smoothTime;

    public CameraFraming(World world) => Map = new CameraMap(world);

    /// <summary>Where the view's centre may be.</summary>
    public CameraMap Map { get; }

    /// <summary>The view's centre, and the goal it moves toward, for <see cref="Telemetry"/>.</summary>
    public readonly record struct Result(Vector2 Centre, Vector2 Goal);

    /// <summary>Puts the target at <paramref name="target"/>, at rest.</summary>
    public void Teleport(Vector2 target)
    {
        _target = target;
        _goal = target;
        _velocity = Vector2.Zero;
        _smoothTime = Features.Current.CameraMinSmoothTime;
    }

    /// <summary>
    /// The view's centre for a player at <paramref name="player"/> who wants it at <paramref name="wanted"/>, for a view
    /// of <paramref name="halfSize"/> either side of its centre at the tiles' front faces, <paramref name="delta"/> game
    /// seconds after the last frame. On the first frame, it's
    /// the goal.
    /// </summary>
    public Result Frame(Vector2 player, Vector2 wanted, Vector2 halfSize, float delta)
    {
        Vector2 box = (halfSize - new Vector2(PlayerMargin, PlayerMargin)).Max(Vector2.Zero);
        var area = new Box(player - box, player + box);

        Vector2 goal = Nearest(wanted, area, from: null) ?? wanted.Clamp(area.Min, area.Max);
        if (_target is not Vector2 target)
        {
            Teleport(goal);
            return new Result(goal, goal);
        }

        Features features = Features.Current;
        if (IsJump(_goal, goal))
        {
            if (CrossesBoundary(_goal, goal))
            {
                goal = Nearest(wanted, area, from: _goal) ?? goal;
            }
        }
        if (IsJump(_goal, goal))
        {
            float jump = _goal.DistanceTo(goal);
            _smoothTime = Math.Max(
                features.CameraMinSmoothTime, Spring.SmoothTimeForPeakSpeed(jump, features.CameraPanSpeed));
        }
        else
        {
            _smoothTime = Smoothing.Exponential(_smoothTime, features.CameraMinSmoothTime, SmoothTimeShortening, delta);
        }
        _goal = goal;

        (target, _velocity) = Spring.CriticallyDamped(target, _velocity, goal, _smoothTime, delta);
        // The player in view comes first. Where the box stops the target, it stops moving that way.
        Vector2 clamped = target.Clamp(area.Min, area.Max);
        _velocity = new Vector2(clamped.X == target.X ? _velocity.X : 0, clamped.Y == target.Y ? _velocity.Y : 0);
        _target = clamped;
        return new Result(clamped, goal);
    }

    /// <summary>
    /// Whether the goal jumped from <paramref name="from"/> to <paramref name="to"/>, rather than sliding: whether the
    /// map is missing at the point halfway between them and at all the pixels next to it. As the wanted framing moves,
    /// the goal moves a little each frame along the map, so halfway is on it; when it jumps across a gap or a corner,
    /// halfway is off it.
    /// </summary>
    private bool IsJump(Vector2 from, Vector2 to)
    {
        Vector2I middle = PixelOf((from + to) / 2);
        for (int y = middle.Y - 1; y <= middle.Y + 1; y++)
        {
            for (int x = middle.X - 1; x <= middle.X + 1; x++)
            {
                if (Map.IsValid(x, y))
                {
                    return false;
                }
            }
        }
        return true;
    }

    /// <summary>
    /// Whether the line from <paramref name="from"/> to <paramref name="to"/> crosses a tile of the camera boundary,
    /// not counting the tiles they're in, since the map's margin lets a point be on the boundary's edge.
    /// </summary>
    private bool CrossesBoundary(Vector2 from, Vector2 to)
    {
        var end = (Vector2I)to.Floor();
        return GridSpace.CastRay(from, to, (x, y) => new Vector2I(x, y) != end && Map.IsBoundary(x, y)) is not null;
    }

    /// <summary>
    /// The point nearest <paramref name="point"/> that the view's centre may be at, within the box; if
    /// <paramref name="from"/> is given, only among those that can be seen from it without crossing the camera
    /// boundary. Null if there's none.
    /// <para>
    /// The map is a grid of pixels, each on it or not, and each pixel stands for its whole cell, a rectangle a third of
    /// a tile wide and a quarter high. So the points the centre may be at are the union of those rectangles, cut to the
    /// box. The nearest point of one rectangle is <paramref name="point"/> clamped into it: itself if it's inside, and
    /// otherwise the nearest point on its edge. The nearest of the union is the nearest of those, over every pixel on the
    /// map in the box.
    /// </para>
    /// </summary>
    private Vector2? Nearest(Vector2 point, Box area, Vector2? from)
    {
        Vector2? best = null;
        float bestDistance = float.PositiveInfinity;
        for (int y = area.Y0; y <= area.Y1; y++)
        {
            for (int x = area.X0; x <= area.X1; x++)
            {
                if (!Map.IsValid(x, y))
                {
                    continue;
                }
                // The pixel's cell, cut to the box; nothing, if the box only touches it.
                Vector2 min = (new Vector2(x, y) * Cell).Max(area.Min);
                Vector2 max = (new Vector2(x + 1, y + 1) * Cell).Min(area.Max);
                if (min.X > max.X || min.Y > max.Y)
                {
                    continue;
                }
                Vector2 nearest = point.Clamp(min, max);
                float distance = nearest.DistanceSquaredTo(point);
                if (distance < bestDistance && (from is not Vector2 seenFrom || !CrossesBoundary(seenFrom, nearest)))
                {
                    best = nearest;
                    bestDistance = distance;
                }
            }
        }
        return best;
    }

    /// <summary>The map's pixel that <paramref name="point"/> is in.</summary>
    private static Vector2I PixelOf(Vector2 point) =>
        new(Mathf.FloorToInt(point.X * Scale.X), Mathf.FloorToInt(point.Y * Scale.Y));

    /// <summary>The box round the player, in tiles, and the map's pixels whose cells it overlaps.</summary>
    private readonly record struct Box(Vector2 Min, Vector2 Max)
    {
        public int X0 => Mathf.FloorToInt(Min.X * Scale.X);
        public int Y0 => Mathf.FloorToInt(Min.Y * Scale.Y);
        public int X1 => Mathf.FloorToInt(Max.X * Scale.X);
        public int Y1 => Mathf.FloorToInt(Max.Y * Scale.Y);
    }
}
