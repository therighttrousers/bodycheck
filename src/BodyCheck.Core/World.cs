using System.Numerics;

namespace BodyCheck.Core;

/// <summary>
/// The simulation: bodies moving through a tile grid on a fixed tick.
/// </summary>
public sealed class World
{
    // Keeps a box that rests against a tile face from counting as overlapping the tiles beside it.
    private const float Epsilon = 1e-4f;

    // How many times a step goes over the traction of every grounded body. In a stack, each pass
    // roughly halves the error left in the bodies below the top one.
    private const int TractionPasses = 8;

    private readonly List<Body> _gripping = [];
    private float[] _tractionApplied = [];

    public World(TileGrid tiles)
    {
        Tiles = tiles;
    }

    public TileGrid Tiles { get; }
    public List<Body> Bodies { get; } = [];
    public Vector2 Gravity { get; set; } = new(0, -50);

    public void Step(float dt)
    {
        foreach (Body body in Bodies)
        {
            body.ApplyForce(body.Mass * Gravity);
            body.ApplyForce(-body.Drag * body.Velocity);
            body.IntegrateForces(dt);
        }

        // Traction comes after every other force is in the velocities, so that it can cancel them
        // exactly, and before contacts are cleared, because it acts through last step's contacts.
        ApplyTraction(dt);

        foreach (Body body in Bodies)
        {
            body.Contacts = Contacts.None;
            body.Support = null;
            body.TransitivelyOnTiles = null;
        }

        // Bodies move one at a time, in list order. A body that pushes one later in the list is held
        // back until that one has moved, so it can trail by up to a tick's travel.
        foreach (Body body in Bodies)
        {
            MoveAndCollide(body, 0, dt);
            MoveAndCollide(body, 1, dt);
        }

        foreach (Body body in Bodies)
        {
            if (body.IsTransitivelyOnTiles() && body.Velocity.Y != 0)
            {
                body.Velocity = new Vector2(body.Velocity.X, 0);
            }
        }
    }

    /// <summary>
    /// Applies each grounded body's <see cref="Traction"/>: a horizontal impulse that takes the body's
    /// speed relative to its ground as close to its target as its maximum force allows, with the
    /// opposite impulse on the body it stands on. Tiles take the impulse without moving.
    /// </summary>
    private void ApplyTraction(float dt)
    {
        _gripping.Clear();
        foreach (Body body in Bodies)
        {
            if (body.OnGround && body.Traction.MaxForce > 0)
            {
                _gripping.Add(body);
            }
        }

        // Bottom of each stack first, so that a pass ends with the bodies on top matched to what carries them.
        _gripping.Sort(static (a, b) => StackDepth(a).CompareTo(StackDepth(b)));

        if (_tractionApplied.Length < _gripping.Count)
        {
            _tractionApplied = new float[_gripping.Count];
        }
        Array.Clear(_tractionApplied);

        // One body's traction changes the velocity of the body under it, which that body's own traction
        // then has to answer. Repeating the pass lets a stack settle. Each body's limit applies to its
        // total over all passes.
        for (int pass = 0; pass < TractionPasses; pass++)
        {
            for (int i = 0; i < _gripping.Count; i++)
            {
                Body body = _gripping[i];
                Body? support = body.Support;

                // The mass that a force between the two has to accelerate to change their relative speed.
                float reducedMass = support is null
                    ? body.Mass
                    : body.Mass * support.Mass / (body.Mass + support.Mass);
                float relativeSpeed = body.Velocity.X - body.GroundVelocity.X;
                float wanted = (body.Traction.TargetSpeed - relativeSpeed) * reducedMass;

                float limit = body.Traction.MaxForce * dt;
                float total = Math.Clamp(_tractionApplied[i] + wanted, -limit, limit);
                var impulse = new Vector2(total - _tractionApplied[i], 0);
                _tractionApplied[i] = total;

                body.ApplyImpulse(impulse);
                support?.ApplyImpulse(-impulse);
            }
        }
    }

    private static int StackDepth(Body body)
    {
        int depth = 0;
        for (Body? support = body.Support; support is not null; support = support.Support)
        {
            depth++;
        }
        return depth;
    }

    private void MoveAndCollide(Body body, int axis, float dt)
    {
        float speed = body.Velocity[axis];
        int sign = MathF.Sign(speed);
        (bool hitTile, Body? hitBody) = MoveAlongAxis(body, axis, speed * dt);

        if (hitTile)
        {
            Vector2 velocity = body.Velocity;
            velocity[axis] = 0;
            body.Velocity = velocity;
            var contact = Side(axis, sign);
            body.Contacts |= contact;
            if (contact == Contacts.Floor)
            {
                body.TransitivelyOnTiles = true;
            }
        }
        else if (hitBody is not null && (speed - hitBody.Velocity[axis]) * sign >= 0)
        {
            // The two are closing: share their momentum along this axis, with no bounce.
            float shared = (body.Momentum[axis] + hitBody.Momentum[axis]) / (body.Mass + hitBody.Mass);
            Vector2 impulse = (axis == 0 ? Vector2.UnitX : Vector2.UnitY) * body.Mass * (shared - speed);
            body.ApplyImpulse(impulse);
            hitBody.ApplyImpulse(-impulse);

            body.Contacts |= Side(axis, sign);
            hitBody.Contacts |= Side(axis, -sign);
            if (axis == 1)
            {
                (Body upper, Body lower) = sign < 0 ? (body, hitBody) : (hitBody, body);
                upper.Support = lower;
            }
        }
    }

    private static Contacts Side(int axis, int sign) => (axis, sign > 0) switch
    {
        (0, true) => Contacts.Right,
        (0, false) => Contacts.Left,
        (_, true) => Contacts.Ceiling,
        (_, false) => Contacts.Floor,
    };

    /// <summary>
    /// Moves a body along one axis, stopping flush against the first solid tile or other body in the way.
    /// Returns what stopped it, if anything. A tile wins a tie.
    /// </summary>
    private (bool HitTile, Body? HitBody) MoveAlongAxis(Body body, int axis, float delta)
    {
        if (delta == 0)
        {
            return (false, null);
        }

        int other = 1 - axis;
        Vector2 position = body.Position;
        Vector2 half = body.HalfSize;
        float lo = position[other] - half[other] + Epsilon;
        float hi = position[other] + half[other] - Epsilon;

        int sign = MathF.Sign(delta);
        float edge = position[axis] + sign * half[axis];
        float target = edge + delta;

        // How far the leading edge can travel, and the face that stops it.
        float travel = MathF.Abs(delta);
        float? stopFace = null;
        Body? hitBody = null;

        // Walk the rows or columns the leading edge would enter, nearest first.
        int line = sign > 0
            ? (int)MathF.Ceiling(edge - Epsilon)
            : (int)MathF.Floor(edge + Epsilon) - 1;
        for (; sign > 0 ? line < target : line + 1 > target; line += sign)
        {
            if (AnySolid(axis, line, (int)MathF.Floor(lo), (int)MathF.Floor(hi)))
            {
                stopFace = sign > 0 ? line : line + 1;
                travel = MathF.Max(0, (stopFace.Value - edge) * sign);
                break;
            }
        }

        foreach (Body candidate in Bodies)
        {
            if (candidate == body
                || lo >= candidate.Position[other] + candidate.HalfSize[other]
                || hi <= candidate.Position[other] - candidate.HalfSize[other])
            {
                continue;
            }

            // Bodies that already overlap by more than rounding error are left free to separate.
            float face = candidate.Position[axis] - sign * candidate.HalfSize[axis];
            float gap = (face - edge) * sign;
            if (gap > -Epsilon && gap < travel)
            {
                stopFace = face;
                travel = MathF.Max(0, gap);
                hitBody = candidate;
            }
        }

        position[axis] = stopFace is float stop ? stop - sign * half[axis] : position[axis] + delta;
        body.Position = position;
        return (stopFace is not null && hitBody is null, hitBody);
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
