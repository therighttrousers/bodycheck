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
}
