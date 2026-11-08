using System.Linq;

using GdUnit4;

using static GdUnit4.Assertions;

namespace BodyCheck.Tests;

[TestSuite]
[RequireGodotRuntime]
public class CameraMapTest
{
    private static bool ValidAtTile(CameraMap map, float x, float y) =>
        map.IsValid((int)(x * CameraMap.PixelsPerTile.X), (int)(y * CameraMap.PixelsPerTile.Y));

    [TestCase]
    public void Keeps_the_view_less_its_margin_inside_an_open_room()
    {
        var map = new CameraMap(CameraFramingTest.Painted(60, 40, (x, y) => false));

        // A view of 20x15 tiles, less a margin of 1, reaches 9 tiles across and 6.5 up and down from its centre.
        AssertThat(ValidAtTile(map, 30, 20)).IsTrue();
        AssertThat(ValidAtTile(map, 9.5f, 20)).IsTrue();
        AssertThat(ValidAtTile(map, 8.5f, 20)).IsFalse();
        AssertThat(ValidAtTile(map, 50.5f, 20)).IsTrue();
        AssertThat(ValidAtTile(map, 51.5f, 20)).IsFalse();
        AssertThat(ValidAtTile(map, 30, 7)).IsTrue();
        AssertThat(ValidAtTile(map, 30, 6)).IsFalse();
        AssertThat(ValidAtTile(map, 30, 33)).IsTrue();
        AssertThat(ValidAtTile(map, 30, 34)).IsFalse();
    }

    [TestCase]
    public void Leaves_a_line_along_a_corridor_lower_than_the_view()
    {
        // A corridor 4 tiles high, from 18 to 22, painted above and below.
        var map = new CameraMap(CameraFramingTest.Painted(60, 40, (x, y) => y is < 18 or >= 22));

        for (int x = 15 * CameraMap.PixelsPerTile.X; x < 45 * CameraMap.PixelsPerTile.X; x++)
        {
            int[] rows = [.. Enumerable.Range(0, map.Height).Where(y => map.IsValid(x, y))];
            AssertThat(rows.Length).IsGreater(0);
            // Along the middle, thickened by the margin.
            AssertThat(rows.Min()).IsGreaterEqual(19 * CameraMap.PixelsPerTile.Y - 2);
            AssertThat(rows.Max()).IsLess(21 * CameraMap.PixelsPerTile.Y + 2);
        }
    }
}
