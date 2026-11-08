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
    internal static World Painted(int width, int height, Func<int, int, bool> boundary)
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
    public void Keeps_to_the_wanted_framing_where_the_map_allows()
    {
        var framing = new CameraFraming(Painted(60, 40, (x, y) => false));
        var player = new Vector2(30, 17);
        Vector2 wanted = player + new Vector2(0.37f, 3);

        AssertThat(framing.Frame(player, wanted, HalfSize, 0).DistanceTo(wanted)).IsLess(1e-4f);
    }

    [TestCase]
    public void Shows_no_more_than_the_margin_of_a_painted_ceiling()
    {
        const int ceiling = 25;
        var framing = new CameraFraming(Painted(60, 40, (x, y) => y >= ceiling));

        // Wanted where the view would show 4.5 rows of the ceiling.
        float top = framing.Frame(new Vector2(30, 19), new Vector2(30, 22), HalfSize, 0).Y + HalfSize.Y;

        AssertThat(top).IsLessEqual(ceiling + CameraMap.MarginTiles + 0.01f);
        AssertThat(top).IsGreaterEqual(ceiling);
    }

    [TestCase]
    public void Keeps_a_corridor_narrower_than_the_view_in_view()
    {
        // A corridor 4 tiles high, from 18 to 22, painted above and below.
        var framing = new CameraFraming(Painted(60, 40, (x, y) => y is < 18 or >= 22));

        // Wanted 5.6 tiles above the player in the corridor, as if looking up.
        float bottom = framing.Frame(new Vector2(30, 20), new Vector2(30, 25.6f), HalfSize, 0).Y - HalfSize.Y;

        AssertThat(bottom).IsLessEqual(18 - CameraMap.MarginTiles + 0.01f);
    }

    [TestCase]
    public void Keeps_the_player_in_view_under_a_thin_band_between_rooms()
    {
        // A space 11 tiles high, lower than the view, under a band 2 tiles thick with open space above it.
        var framing = new CameraFraming(Painted(60, 45, (x, y) => y is < 10 or 21 or 22 or 44));
        var player = new Vector2(30, 19);

        Vector2 centre = framing.Frame(player, player + new Vector2(0, 3), HalfSize, 0);

        AssertThat(Math.Abs(centre.Y - player.Y)).IsLessEqual(HalfSize.Y - CameraFraming.PlayerMargin);
        // And the view stays on the space's line: centred on the space, it would show all of the band, and the map lets
        // it move by the margin, and by the line's thickness, a pixel.
        AssertThat(centre.Y + HalfSize.Y).IsLessEqual(23 + CameraMap.MarginTiles + 1f / CameraMap.PixelsPerTile.Y);
    }

    [TestCase]
    public void Moves_no_faster_than_its_speed_past_a_shaft_in_a_painted_ceiling()
    {
        // A ceiling from 20 up, with a shaft 5 tiles wide through it.
        var framing = new CameraFraming(Painted(80, 40, (x, y) => y >= 20 && x is < 38 or >= 43));
        const float delta = 1 / 60f;

        // Walking at 6 tiles a second, wanted 3 tiles above the player.
        Vector2 previous = framing.Frame(new Vector2(20, 13), new Vector2(20, 16), HalfSize, 0);
        float largest = 0;
        for (float x = 20.1f; x <= 60; x += 0.1f)
        {
            Vector2 centre = framing.Frame(new Vector2(x, 13), new Vector2(x, 16), HalfSize, delta);
            largest = Math.Max(largest, centre.DistanceTo(previous));
            previous = centre;
        }

        // The box round the player may push it by as far as the player moves, besides.
        AssertThat(largest).IsLessEqual(CameraFraming.Speed * delta + 0.1f + 1e-4f);
    }

    [TestCase]
    public void Stays_on_its_side_of_a_band_when_looking_up()
    {
        // Two corridors 4 tiles high, from 18 to 22 and from 24 to 28, with a solid band between them.
        var framing = new CameraFraming(Painted(60, 40, (x, y) => y is < 18 or 22 or 23 or >= 28));
        var player = new Vector2(30, 20);
        const float delta = 1 / 60f;

        // Placed with the wanted framing 3 tiles above the player, then looking up over a second.
        framing.Frame(player, player + new Vector2(0, 3), HalfSize, 0);
        Vector2 centre = Vector2.Zero;
        for (int frame = 1; frame <= 60; frame++)
        {
            centre = framing.Frame(player, player + new Vector2(0, Mathf.Lerp(3, 5.6f, frame / 60f)), HalfSize, delta);
        }

        // Still on the lower corridor's line, which the margin thickens by a tile.
        AssertThat(centre.Y).IsLess(22);
    }
}
