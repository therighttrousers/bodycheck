using System.Numerics;

namespace BodyCheck.Core;

[Flags]
public enum Contacts
{
    None = 0,
    Floor = 1,
    Ceiling = 2,
    Left = 4,
    Right = 8,
}

/// <summary>
/// An axis-aligned box with mass. Velocity changes only through forces and impulses,
/// and through contact with solid tiles and other bodies.
/// </summary>
public sealed class Body
{
    private Vector2 _force;

    public Body(Vector2 position, Vector2 halfSize, float mass)
    {
        Position = position;
        HalfSize = halfSize;
        Mass = mass;
    }

    /// <summary>The center of the box.</summary>
    public Vector2 Position { get; set; }
    public Vector2 HalfSize { get; }
    public float Mass { get; }
    public Vector2 Velocity { get; internal set; }

    /// <summary>Linear drag coefficient: drag force is -Drag × Velocity.</summary>
    public float Drag { get; set; }

    /// <summary>
    /// Which sides met solid tiles or other bodies during the last step. A body at rest against a wall
    /// isn't moving into it, so only the floor contact persists (gravity renews it every step).
    /// </summary>
    public Contacts Contacts { get; internal set; }

    /// <summary>
    /// The body this one stood on during the last step, or null if it stood on tiles or on nothing.
    /// </summary>
    public Body? Support { get; internal set; }

    public Vector2 Momentum => Mass * Velocity;
    public bool OnGround => Contacts.HasFlag(Contacts.Floor);
    public bool OnTiles => Contacts.HasFlag(Contacts.Floor) && Support == null;

    /// <summary>Adds a force that acts for the duration of the next step.</summary>
    public void ApplyForce(Vector2 force) => _force += force;

    public void ApplyImpulse(Vector2 impulse) => Velocity += impulse / Mass;

    /// <summary>
    /// If Body.Support is non-null, applies forces to Body and Body.Support due to friction.
    /// </summary>
    public void ApplySupportFriction(float kineticFriction, float weight, float dt)
    {
        if (Support == null) return;

        // var relVel = Velocity - Support.Velocity;
        var relVel = new Vector2(Velocity.X - Support.Velocity.X, 0);
        var relSpeed = relVel.Length();
        if (relSpeed < 0.0001f) return;

        var relDir = relVel / relSpeed;

        // Cap force magnitude by the amount needed to perfectly match speeds this frame if there were no other
        // forces applied to this and Support. Intended to correct overshoot, which causes jitter.
        var force = -relDir * Math.Min(
            kineticFriction * weight,
            relSpeed * Mass * Support.Mass / (Mass + Support.Mass) / dt
        );
        ApplyForce(force);
        Support.ApplyForce(-force);
    }

    public void Teleport(Vector2 position, Vector2 velocity)
    {
        Position = position;
        Velocity = velocity;
        Contacts = Contacts.None;
        Support = null;
    }

    internal void IntegrateForces(float dt)
    {
        Velocity += _force / Mass * dt;
        _force = Vector2.Zero;
    }
}
