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
    /// <summary>The view's half size at the tiles' front faces, at 320x240.</summary>
    private static readonly Vector2 HalfSize = new(10, 7.5f);

    /// <summary>
    /// A world of one room, with no tiles, the player in its bottom left corner, and <paramref name="boundary"/> telling
    /// which tiles are its camera boundary.
    /// </summary>
    private static World Painted(int width, int height, Func<int, int, bool> boundary)
    {
        string[] rows = [.. Enumerable.Range(0, height).Select(row =>
            row == height - 1 ? "P" + new string('.', width - 1) : new string('.', width))];
        var cameraBoundary = new bool[width, height];
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                cameraBoundary[x, y] = boundary(x, y);
            }
        }
        Room text = Room.FromText("Room", string.Join('\n', rows));
        return new World(new Room(text.Name, text.Origin, TileGrid.FromText(rows), text.EntityConfigs, cameraBoundary));
    }

    [TestCase]
    public void Keeps_to_the_wanted_framing_away_from_the_boundary()
    {
        var framing = new CameraFraming(Painted(60, 40, (x, y) => false));
        var player = new Vector2(30, 17);

        AssertThat(framing.Frame(player, player, HalfSize).DistanceTo(player)).IsLess(1e-3f);
    }

    [TestCase]
    public void Shows_no_more_than_the_margin_of_a_painted_ceiling()
    {
        const int ceiling = 25;
        var framing = new CameraFraming(Painted(60, 40, (x, y) => y >= ceiling));

        // Wanted where the view would show 4.5 rows of the ceiling.
        float top = framing.Frame(new Vector2(30, 19), new Vector2(30, 22), HalfSize).Y + HalfSize.Y;

        AssertThat(top).IsLessEqual(ceiling + CameraFraming.Margin + 0.1f);
        AssertThat(top).IsGreaterEqual(ceiling);
    }

    [TestCase]
    public void Keeps_a_corridor_narrower_than_the_view_in_view()
    {
        // A corridor 4 tiles high, from 18 to 22, painted above and below.
        var framing = new CameraFraming(Painted(60, 40, (x, y) => y is < 18 or >= 22));

        // Wanted 5.6 tiles above the player in the corridor, as if looking up.
        float bottom = framing.Frame(new Vector2(30, 20), new Vector2(30, 25.6f), HalfSize).Y - HalfSize.Y;

        AssertThat(bottom).IsLessEqual(18 - CameraFraming.Margin + 0.1f);
    }

    [TestCase]
    public void Keeps_the_player_in_view_under_a_thin_band_between_rooms()
    {
        // A space 11 tiles high, lower than the view, under a band 2 tiles thick with open space above it.
        var framing = new CameraFraming(Painted(60, 45, (x, y) => y is < 10 or 21 or 22 or 44));
        var player = new Vector2(30, 19);

        Vector2 centre = framing.Frame(player, player + new Vector2(0, 3), HalfSize);

        AssertThat(Math.Abs(centre.Y - player.Y)).IsLessEqual(HalfSize.Y - CameraFraming.PlayerMargin);
    }

    [TestCase]
    public void Moves_the_view_smoothly_past_a_shaft_in_a_painted_ceiling()
    {
        // A ceiling from 20 up, with a shaft 5 tiles wide through it.
        var framing = new CameraFraming(Painted(80, 40, (x, y) => y >= 20 && x is < 38 or >= 43));
        const float step = 0.1f;

        // Wanted where the view would show 3.5 rows of the ceiling.
        Vector2 previous = framing.Frame(new Vector2(20, 13), new Vector2(20, 16), HalfSize);
        float largest = 0;
        for (float x = 20 + step; x <= 60; x += step)
        {
            Vector2 centre = framing.Frame(new Vector2(x, 13), new Vector2(x, 16), HalfSize);
            largest = Math.Max(largest, centre.DistanceTo(previous));
            previous = centre;
        }

        // No faster than the wanted framing moves, and a little more for the rise into the shaft.
        AssertThat(largest).IsLessEqual(1.5f * step);
    }
}
