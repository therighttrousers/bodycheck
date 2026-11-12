using Godot;

namespace BodyCheck.Core.Maths;

/// <summary>Axis-aligned boxes, given by their centres and half-sizes.</summary>
public static class Boxes
{
    /// <summary>
    /// How far <paramref name="point"/> is from the nearest point of the box at <paramref name="center"/>, of
    /// <paramref name="halfSize"/> either side of it: 0 inside it. Along each axis, the point is outside the box by how
    /// far it is from the centre less the half-size, or not at all; the distance is the length of that. Two boxes are
    /// as far apart as the centre of one is from a box of their half-sizes added, at the other's centre.
    /// </summary>
    public static float Distance(Vector2 point, Vector2 center, Vector2 halfSize) =>
        ((point - center).Abs() - halfSize).Max(Vector2.Zero).Length();

    /// <summary>
    /// <see cref="Distance"/> outside the box, and inside it, less than 0 by how far <paramref name="point"/> is from
    /// the nearest edge: so 0 on the edge, and continuous across it.
    /// </summary>
    public static float SignedDistance(Vector2 point, Vector2 center, Vector2 halfSize)
    {
        Vector2 outside = (point - center).Abs() - halfSize;
        return outside.Max(Vector2.Zero).Length() + Mathf.Min(Mathf.Max(outside.X, outside.Y), 0);
    }
}
