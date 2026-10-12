using System;
using System.Collections.Generic;

using Godot;

namespace BodyCheck.Spike;

/// <summary>
/// The part of Core's <see cref="Core.World"/> that the engine can't do for us: it turns each body's
/// forces, impulses and traction for the tick into one impulse, and hands that to the engine. The engine
/// then adds gravity, resolves contacts and moves the bodies.
/// </summary>
public sealed class JoltWorld
{
    // How many times a step goes over the traction of every grounded body. In a stack, each pass
    // roughly halves the error left in the bodies below the top one.
    private const int TractionPasses = 8;

    private readonly List<JoltBody> _gripping = [];
    private float[] _tractionApplied = [];

    public List<JoltBody> Bodies { get; } = [];

    /// <summary>Call once per physics tick, after every controller has run.</summary>
    public void Step(float dt)
    {
        // Work out where our own forces and impulses leave each body's velocity, as Core does before
        // traction. The engine's velocity doesn't change until its step, so the working copy is kept here.
        foreach (JoltBody body in Bodies)
        {
            Vector3 velocity = body.LinearVelocity;
            body.AddForce(-body.Drag * velocity);
            body.StepVelocity = velocity + body.TakePendingImpulse(dt) / body.Mass;
        }

        // Traction comes after every other force is in the velocities, so that it can cancel them exactly.
        ApplyTraction(dt);

        foreach (JoltBody body in Bodies)
        {
            body.ApplyCentralImpulse(body.Mass * (body.StepVelocity - body.LinearVelocity));
        }
    }

    /// <summary>
    /// Applies each grounded body's <see cref="Core.Traction"/>: a horizontal impulse that takes the body's
    /// speed relative to its ground as close to its target as its maximum force allows, with the
    /// opposite impulse on the body it stands on. Tiles take the impulse without moving.
    /// </summary>
    private void ApplyTraction(float dt)
    {
        _gripping.Clear();
        foreach (JoltBody body in Bodies)
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
                JoltBody body = _gripping[i];
                JoltBody? support = body.Support;

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

                body.StepVelocity += new Vector3(impulse / body.Mass, 0, 0);
                if (support is not null)
                {
                    support.StepVelocity -= new Vector3(impulse / support.Mass, 0, 0);
                }
            }
        }
    }

    private static int StackDepth(JoltBody body)
    {
        int depth = 0;
        for (JoltBody? support = body.Support; support is not null; support = support.Support)
        {
            depth++;
        }
        return depth;
    }
}
