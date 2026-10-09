using System.Numerics;

namespace BodyCheck.Core;

/// <summary>
/// The simulation: bodies moving through a tile grid on a fixed tick.
/// </summary>
public sealed class World
{
    // Keeps a box that rests against a tile face from counting as overlapping the tiles beside it.
    private const float Epsilon = 1e-4f;

    public World(TileGrid tiles)
    {
        Tiles = tiles;
    }

    public TileGrid Tiles { get; }
    public List<Body> Bodies { get; } = [];
    public Vector2 Gravity { get; set; } = new(0, -30);

    public void Step(float dt)
    {
        foreach (Body body in Bodies)
        {
            body.ApplyForce(body.Mass * Gravity);
            body.ApplyForce(-body.Drag * body.Velocity);
            body.IntegrateForces(dt);

            body.Contacts = Contacts.None;
            Vector2 velocity = body.Velocity;
            if (MoveAlongAxis(body, 0, velocity.X * dt))
            {
                body.Contacts |= velocity.X > 0 ? Contacts.Right : Contacts.Left;
                velocity.X = 0;
            }
            if (MoveAlongAxis(body, 1, velocity.Y * dt))
            {
                body.Contacts |= velocity.Y > 0 ? Contacts.Ceiling : Contacts.Floor;
                velocity.Y = 0;
            }
            body.Velocity = velocity;
        }
    }

    /// <summary>
    /// Moves a body along one axis, stopping flush against the first solid tile in the way.
    /// Returns true if it was stopped.
    /// </summary>
    private bool MoveAlongAxis(Body body, int axis, float delta)
    {
        if (delta == 0)
        {
            return false;
        }

        int other = 1 - axis;
        Vector2 position = body.Position;
        Vector2 half = body.HalfSize;
        int lo = (int)MathF.Floor(position[other] - half[other] + Epsilon);
        int hi = (int)MathF.Floor(position[other] + half[other] - Epsilon);

        int sign = MathF.Sign(delta);
        float edge = position[axis] + sign * half[axis];
        float target = edge + delta;

        // Walk the rows or columns the leading edge would enter, nearest first.
        int line = sign > 0
            ? (int)MathF.Ceiling(edge - Epsilon)
            : (int)MathF.Floor(edge + Epsilon) - 1;
        for (; sign > 0 ? line < target : line + 1 > target; line += sign)
        {
            if (AnySolid(axis, line, lo, hi))
            {
                float face = sign > 0 ? line : line + 1;
                position[axis] = face - sign * half[axis];
                body.Position = position;
                return true;
            }
        }

        position[axis] += delta;
        body.Position = position;
        return false;
    }

    private bool AnySolid(int axis, int line, int lo, int hi)
    {
        for (int i = lo; i <= hi; i++)
        {
            if (axis == 0 ? Tiles.IsSolid(line, i) : Tiles.IsSolid(i, line))
            {
                return true;
            }
        }
        return false;
    }
}
