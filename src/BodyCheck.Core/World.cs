namespace BodyCheck.Core;

/// <summary>
/// What the rules add to the physics engine each tick. It turns every body's forces, impulses and drag
/// into one impulse per body for the engine to apply. The engine then adds gravity, applies each walker's
/// traction, resolves contacts and moves the bodies.
/// </summary>
public sealed class World
{
    public List<Body> Bodies { get; } = [];

    /// <summary>The downward acceleration that the engine applies to every body.</summary>
    public float Gravity { get; init; } = 50;

    /// <summary>Call once per tick, after every controller has run and before the engine's step.</summary>
    public void Step(float dt)
    {
        foreach (Body body in Bodies)
        {
            body.AddForce(-body.Drag * body.EngineVelocity);
            body.SetEngineImpulse(body.TakePendingImpulse(dt));
        }
    }
}
