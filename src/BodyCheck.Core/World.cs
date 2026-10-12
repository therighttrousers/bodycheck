using Godot;

namespace BodyCheck.Core;

/// <summary>
/// What the rules add to the physics engine each tick. It turns every body's forces, impulses, drag and
/// traction into one impulse per body for the engine to apply. The engine then adds gravity, resolves
/// contacts and moves the bodies.
/// </summary>
public sealed class World
{
    // How many times a step goes over the traction of every grounded body. In a stack, each pass
    // roughly halves the error left in the bodies below the top one.
    private const int TractionPasses = 8;

    private readonly List<Body> _gripping = [];
    private float[] _tractionApplied = [];

    public List<Body> Bodies { get; } = [];

    /// <summary>The downward acceleration that the engine applies to every body.</summary>
    public float Gravity { get; init; } = 50;

    /// <summary>Call once per tick, after every controller has run and before the engine's step.</summary>
    public void Step(float dt)
    {
        // Work out where the rules' own forces and impulses leave each body's velocity. The engine's
        // velocity doesn't change until its step, so a working copy is kept.
        foreach (Body body in Bodies)
        {
            body.AddForce(-body.Drag * body.Velocity);
            body.StepVelocity = body.Velocity + body.TakePendingImpulse(dt) / body.Mass;
        }

        // Traction comes after every other force is in the velocities, so that it can cancel them exactly.
        ApplyTraction(dt);

        foreach (Body body in Bodies)
        {
            body.SetEngineImpulse(body.Mass * (body.StepVelocity - body.Velocity));
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
                float relativeSpeed = body.StepVelocity.X - (support?.StepVelocity.X ?? 0);
                float wanted = (body.Traction.TargetSpeed - relativeSpeed) * reducedMass;

                float limit = body.Traction.MaxForce * dt;
                float total = Math.Clamp(_tractionApplied[i] + wanted, -limit, limit);
                float impulse = total - _tractionApplied[i];
                _tractionApplied[i] = total;

                body.StepVelocity += new Vector2(impulse / body.Mass, 0);
                if (support is not null)
                {
                    support.StepVelocity -= new Vector2(impulse / support.Mass, 0);
                }
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
}
