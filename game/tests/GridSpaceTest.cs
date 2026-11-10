using System.Collections.Generic;

using BodyCheck.Core.Maths;

using GdUnit4;

using Godot;

using static GdUnit4.Assertions;

namespace BodyCheck.Tests;

[TestSuite]
[RequireGodotRuntime]
public class GridSpaceTest
{
    [TestCase]
    public void CastRay_returns_where_the_line_enters_the_cell_it_hits()
    {
        Vector2? hit = GridSpace.CastRay(new Vector2(0.5f, 0.5f), new Vector2(4.5f, 2.5f), (x, y) => x == 2);

        AssertThat(hit is null).IsFalse();
        AssertThat(hit!.Value.X).IsEqualApprox(2f, 0.0001f);
        AssertThat(hit.Value.Y).IsEqualApprox(1.25f, 0.0001f);
    }

    [TestCase]
    public void CastRay_counts_the_end_cell_but_not_the_start_cell()
    {
        var from = new Vector2(0.5f, 0.5f);
        var to = new Vector2(2.5f, 0.5f);

        AssertThat(GridSpace.CastRay(from, to, (x, y) => x == 0) is null).IsTrue();
        AssertThat(GridSpace.CastRay(from, to, (x, y) => x == 2) is null).IsFalse();
    }

    [TestCase]
    public void CastRay_steps_to_a_side_at_a_time_and_stops_in_the_end_cell()
    {
        // Exactly diagonal, so that every step is a tie, and rounding decides which way it goes.
        var cells = new List<Vector2I>();
        Vector2? hit = GridSpace.CastRay(new Vector2(0.1f, 0.1f), new Vector2(10.1f, 10.1f), (x, y) =>
        {
            cells.Add(new Vector2I(x, y));
            return false;
        });

        AssertThat(hit is null).IsTrue();
        AssertThat(cells.Count).IsEqual(20);
        AssertThat(cells[^1]).IsEqual(new Vector2I(10, 10));
        Vector2I last = Vector2I.Zero;
        foreach (Vector2I cell in cells)
        {
            AssertThat(Mathf.Abs(cell.X - last.X) + Mathf.Abs(cell.Y - last.Y)).IsEqual(1);
            AssertThat(cell.X <= 10 && cell.Y <= 10).IsTrue();
            last = cell;
        }
    }
}
