using Godot;

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
/// The rules' view of a box with mass, in the XY plane. The physics engine moves it and resolves its
/// contacts; the node that owns it copies the engine's state in each tick, with <see cref="SetMotion"/>
/// and <see cref="SetContacts"/>, and passes <see cref="TakeEngineImpulse"/> back to the engine.
/// <para>
/// Controllers change a body's motion only through <see cref="AddForce"/>, <see cref="AddImpulse"/> and
/// <see cref="Traction"/>, so that <see cref="World.Step"/> sees everything that acts on it.
/// </para>
/// </summary>
public sealed class Body
{
    private Vector2 _force;
    private Vector2 _impulse;
    private Vector2 _engineImpulse;

    public Body(Vector2 halfSize, float mass)
    {
        HalfSize = halfSize;
        Mass = mass;
    }

    public Vector2 HalfSize { get; }
    public float Mass { get; }

    /// <summary>Linear drag coefficient: drag force is -Drag × Velocity.</summary>
    public float Drag { get; set; }

    /// <summary>
    /// The engine's coefficient of friction for this body. Leave it at 0 for anything that walks: a walker
    /// grips through <see cref="Traction"/>, and with engine friction it would grip walls too, and lose its
    /// speed on landing. Other bodies, which have no traction, need it to rest on things and be carried.
    /// <para>
    /// Where two bodies touch, the engine uses the smaller of their coefficients, so a walker slides
    /// freely against everything.
    /// </para>
    /// </summary>
    public float EngineFriction { get; init; }

    /// <summary>Whether this body grips through traction rather than through the engine's friction.</summary>
    public bool IsWalker => EngineFriction == 0;

    /// <summary>
    /// The grip this body applies while it stands on something. <see cref="World.Step"/> turns it into
    /// equal and opposite impulses on this body and its <see cref="Support"/>. It has no effect in the air.
    /// </summary>
    public Traction Traction { get; set; }

    /// <summary>The center of the box, as of the engine's last step.</summary>
    public Vector2 Position { get; private set; }

    /// <summary>
    /// The velocity as of the engine's last step, plus the impulses added since. Forces added since then
    /// don't show in it until the next step.
    /// </summary>
    public Vector2 Velocity => EngineVelocity + _impulse / Mass;

    /// <summary>The velocity as of the engine's last step.</summary>
    internal Vector2 EngineVelocity { get; private set; }

    /// <summary>Which sides touched tiles or other bodies during the engine's last step.</summary>
    public Contacts Contacts { get; private set; }

    /// <summary>The body this one stood on during the engine's last step, or null if it stood on tiles or on nothing.</summary>
    public Body? Support { get; private set; }

    /// <summary>The velocity of whatever this body stands on: zero for tiles, and in the air.</summary>
    public Vector2 GroundVelocity => Support?.Velocity ?? Vector2.Zero;

    public bool OnGround => Contacts.HasFlag(Contacts.Floor);

    /// <summary>This body's velocity as <see cref="World.Step"/> works it out, before the engine has it.</summary>
    internal Vector2 StepVelocity { get; set; }

    /// <summary>Adds a force that acts for the duration of the next step.</summary>
    public void AddForce(Vector2 force) => _force += force;

    /// <summary>Adds an impulse that takes effect in the next step.</summary>
    public void AddImpulse(Vector2 impulse) => _impulse += impulse;

    /// <summary>For the owning node: copies in where the engine has the body, and how fast it is moving.</summary>
    public void SetMotion(Vector2 position, Vector2 velocity)
    {
        Position = position;
        EngineVelocity = velocity;
    }

    /// <summary>For the owning node: copies in what the body touched during the engine's last step.</summary>
    public void SetContacts(Contacts contacts, Body? support)
    {
        Contacts = contacts;
        Support = support;
    }

    /// <summary>
    /// For the owning node: returns the impulse that the last <see cref="World.Step"/> worked out for the
    /// engine to apply, and clears it.
    /// </summary>
    public Vector2 TakeEngineImpulse()
    {
        Vector2 impulse = _engineImpulse;
        _engineImpulse = Vector2.Zero;
        return impulse;
    }

    /// <summary>Returns the forces and impulses added since the last step as one impulse, and clears them.</summary>
    internal Vector2 TakePendingImpulse(float dt)
    {
        Vector2 total = _force * dt + _impulse;
        _force = Vector2.Zero;
        _impulse = Vector2.Zero;
        return total;
    }

    internal void SetEngineImpulse(Vector2 impulse) => _engineImpulse = impulse;
}
