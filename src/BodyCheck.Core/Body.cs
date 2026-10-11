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
/// How a body uses its grip on whatever it stands on. It pushes against the ground to bring its
/// horizontal speed, relative to the ground, towards <paramref name="TargetSpeed"/>, using at most
/// <paramref name="MaxForce"/>. A target of zero is braking. A maximum of zero is no grip at all.
/// </summary>
public readonly record struct Traction(float TargetSpeed, float MaxForce);

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

    internal bool? TransitivelyOnTiles { get; set; }

    /// <summary>
    /// Returns true whenever this body is blocked from falling by a tile below it, transitively through its Support.
    /// It is NOT safe to call this during a call to World.Step (except from World.Step itself).
    /// </summary>
    public bool IsTransitivelyOnTiles() => TransitivelyOnTiles ??= Support is not null && Support.IsTransitivelyOnTiles();

    /// <summary>
    /// The grip this body applies while it stands on something. <see cref="World.Step"/> turns it into
    /// equal and opposite impulses on this body and its <see cref="Support"/>. It has no effect in the air.
    /// </summary>
    public Traction Traction { get; set; }

    /// <summary>The velocity of whatever this body stands on: zero for tiles, and in the air.</summary>
    public Vector2 GroundVelocity => Support?.Velocity ?? Vector2.Zero;

    public Vector2 Momentum => Mass * Velocity;
    public bool OnGround => Contacts.HasFlag(Contacts.Floor);
    public bool OnTiles => Contacts.HasFlag(Contacts.Floor) && Support == null;

    /// <summary>Adds a force that acts for the duration of the next step.</summary>
    public void ApplyForce(Vector2 force) => _force += force;

    public void ApplyImpulse(Vector2 impulse) => Velocity += impulse / Mass;

    public void Teleport(Vector2 position, Vector2 velocity)
    {
        Position = position;
        Velocity = velocity;
        Contacts = Contacts.None;
        Support = null;
        TransitivelyOnTiles = null;
    }

    internal void IntegrateForces(float dt)
    {
        Velocity += _force / Mass * dt;
        _force = Vector2.Zero;
    }
}
