using System;
using System.Linq;

using BodyCheck.Core.WorldData;

using GdUnit4;

using Godot;

using static GdUnit4.Assertions;

namespace BodyCheck.Tests;

[TestSuite]
[RequireGodotRuntime]
public class CameraFramingTest
{
    private const int Size = 40;
    /// <summary>The bottom of the ceiling over the lower half of <see cref="Split"/>.</summary>
    private const int Ceiling = 18;
    private const int Hole = 10;
    private const int ShaftLeft = 24;
    private const int ShaftRight = 29;
    /// <summary>Where the player's centre is, standing on the floor.</summary>
    private const float Standing = 2;

    private static readonly Vector2 HalfSize = new(10, 7.5f);

    /// <summary>A room walled all round, with <paramref name="solid"/> telling where else is solid, and the player at 2, 1.</summary>
    private static World Walled(int width, int height, Func<int, int, bool> solid)
    {
        string Row(int y) => new([.. Enumerable.Range(0, width).Select(x =>
            x == 2 && y == 1 ? 'P'
            : x == 0 || x == width - 1 || y == 0 || y == height - 1 || solid(x, y) ? '#'
            : '.')]);
        return new World("Test", [Room.FromText("Room", string.Join('\n', Enumerable.Range(0, height).Reverse().Select(Row)))]);
    }

    /// <summary>A 40x40 room split by a ceiling two tiles thick, with a hole one tile wide in it and a shaft six tiles wide.</summary>
    private static World Split() => Walled(Size, Size, (x, y) =>
        y is Ceiling or Ceiling + 1 && x != Hole && x is < ShaftLeft or > ShaftRight);

    /// <summary>A 40x40 room split by a ceiling two tiles thick, with a hole in it only if <paramref name="hole"/>.</summary>
    private static World Ceilinged(bool hole) => Walled(Size, Size, (x, y) =>
        y is Ceiling or Ceiling + 1 && !(hole && x == Hole));

    /// <summary>Where the view's centre goes for a player at <paramref name="x"/> who wants to look far up.</summary>
    private static float LookingUp(CameraFraming framing, float x) =>
        framing.Frame(new Vector2(x, Standing), new Vector2(x, 15), HalfSize).Y;

    [TestCase]
    public void Keeps_the_view_from_going_far_past_a_ceiling()
    {
        float top = LookingUp(new CameraFraming(Ceilinged(hole: false)), Hole + 0.5f) + HalfSize.Y;

        // The ceiling's face shows, and not much of the rock behind it.
        AssertThat(top).IsLessEqual(Ceiling + 2.5f);
    }

    [TestCase]
    public void Lets_the_view_through_a_hole_near_the_player()
    {
        var framing = new CameraFraming(Ceilinged(hole: true));

        // Far from the hole, the view stays under the ceiling.
        AssertThat(LookingUp(framing, Hole + 0.5f)).IsGreater(LookingUp(framing, Hole + 20.5f) + 1);
    }

    [TestCase]
    public void Moves_the_view_smoothly_as_the_player_walks_under_a_shaft()
    {
        var framing = new CameraFraming(Split());
        const float step = 0.1f;

        float previous = LookingUp(framing, 15);
        float largest = 0;
        for (float x = 15 + step; x <= 35; x += step)
        {
            float y = LookingUp(framing, x);
            largest = Math.Max(largest, Math.Abs(y - previous));
            previous = y;
        }

        // The view rises into the shaft, but no faster than the player walks.
        AssertThat(largest).IsLessEqual(1.5f * step);
    }

    [TestCase]
    public void Keeps_the_view_level_over_a_bumpy_floor_under_a_bumpy_ceiling()
    {
        var framing = new CameraFraming(Walled(Size, Size, (x, y) =>
            (y == 1 && x % 4 == 0) || (y == 15 && x % 5 == 0) || y is 16 or 17));

        float lowest = float.MaxValue;
        float highest = float.MinValue;
        for (float x = 12; x <= 28; x += 0.1f)
        {
            float y = framing.Frame(new Vector2(x, Standing), new Vector2(x, Standing + 3), HalfSize).Y;
            lowest = Math.Min(lowest, y);
            highest = Math.Max(highest, y);
        }

        AssertThat(highest - lowest).IsLessEqual(0.25f);
    }

    [TestCase]
    public void Ignores_what_the_view_would_not_show()
    {
        // An overhang 15 tiles to the right of the player, at the height of a jump.
        World open = Walled(60, 30, (x, y) => false);
        World overhang = Walled(60, 30, (x, y) => x is >= 25 and < 30 && y is >= 5 and < 7);
        var player = new Vector2(10.5f, 4);
        Vector2 wanted = player + new Vector2(0, 3);

        Vector2 expected = new CameraFraming(open).Frame(player, wanted, HalfSize);
        Vector2 actual = new CameraFraming(overhang).Frame(player, wanted, HalfSize);

        AssertThat(actual.DistanceTo(expected)).IsLess(1e-3f);
    }
}
