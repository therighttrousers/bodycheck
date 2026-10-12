using BodyCheck.Core;

using Godot;

namespace BodyCheck.Spike;

/// <summary>
/// The Jolt counterpart of Core's <see cref="Body"/>: a box with mass that can only move in the XY plane
/// and can't rotate. The engine moves it and resolves its contacts. Friction, bounce and damping are off
/// in the engine: grip is <see cref="Traction"/> and drag is ours, both applied by <see cref="JoltWorld"/>.
/// <para>
/// Use <see cref="AddForce"/> and <see cref="AddImpulse"/> instead of the engine's ApplyForce and
/// ApplyImpulse, so that traction can take them into account.
/// </para>
/// </summary>
public partial class JoltBody : RigidBody3D
{
    // How closely a contact normal must line up with an axis to count as a contact on that side.
    private const float SideThreshold = 0.7f;

    private Vector3 _force;
    private Vector3 _impulse;

    public JoltBody(Vector3 halfSize, float mass, float drag, Color color, bool glows)
    {
        HalfSize = halfSize;
        Mass = mass;
        Drag = drag;

        AxisLockLinearZ = true;
        AxisLockAngularX = true;
        AxisLockAngularY = true;
        AxisLockAngularZ = true;

        PhysicsMaterialOverride = new PhysicsMaterial { Friction = 0, Bounce = 0 };
        LinearDampMode = DampMode.Replace;
        LinearDamp = 0;
        CanSleep = false;
        ContactMonitor = true;
        MaxContactsReported = 8;
        ContinuousCd = true;

        var size = new Vector3(2 * halfSize.X, 2 * halfSize.Y, 2 * halfSize.Z);
        AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = size } });

        var mesh = new MeshInstance3D
        {
            Mesh = new BoxMesh
            {
                Size = size,
                Material = new StandardMaterial3D
                {
                    AlbedoColor = color,
                    EmissionEnabled = glows,
                    Emission = color,
                    EmissionEnergyMultiplier = 1.5f,
                },
            },
        };
        if (glows)
        {
            mesh.AddChild(new OmniLight3D { LightColor = color, LightEnergy = 2, OmniRange = 6 });
        }
        AddChild(mesh);
    }

    public Vector3 HalfSize { get; }

    /// <summary>Linear drag coefficient: drag force is -Drag × velocity.</summary>
    public float Drag { get; set; }

    /// <summary>
    /// The grip this body applies while it stands on something. <see cref="JoltWorld.Step"/> turns it into
    /// equal and opposite impulses on this body and its <see cref="Support"/>. It has no effect in the air.
    /// </summary>
    public Traction Traction { get; set; }

    /// <summary>This body's velocity as <see cref="JoltWorld.Step"/> works it out, before the engine has it.</summary>
    internal Vector3 StepVelocity { get; set; }

    /// <summary>Which sides touched tiles or other bodies during the last step.</summary>
    public Contacts Contacts { get; private set; }

    /// <summary>The body this one stood on during the last step, or null if it stood on tiles or on nothing.</summary>
    public JoltBody? Support { get; private set; }

    /// <summary>The velocity of whatever this body stands on: zero for tiles, and in the air.</summary>
    public Vector3 GroundVelocity => Support?.LinearVelocity ?? Vector3.Zero;

    public bool OnGround => Contacts.HasFlag(Contacts.Floor);

    /// <summary>Adds a force that acts for the duration of the next step.</summary>
    public void AddForce(Vector3 force) => _force += force;

    /// <summary>Adds an impulse that takes effect in the next step.</summary>
    public void AddImpulse(Vector3 impulse) => _impulse += impulse;

    /// <summary>Returns the forces and impulses added since the last step as one impulse, and clears them.</summary>
    internal Vector3 TakePendingImpulse(float dt)
    {
        Vector3 total = _force * dt + _impulse;
        _force = Vector3.Zero;
        _impulse = Vector3.Zero;
        return total;
    }

    public override void _IntegrateForces(PhysicsDirectBodyState3D state)
    {
        Contacts = Contacts.None;
        Support = null;
        for (int i = 0; i < state.GetContactCount(); i++)
        {
            // The normal points away from the other collider, towards this body.
            Vector3 normal = state.GetContactLocalNormal(i);
            if (normal.Y > SideThreshold)
            {
                Contacts |= Contacts.Floor;
                Support ??= state.GetContactColliderObject(i) as JoltBody;
            }
            else if (normal.Y < -SideThreshold)
            {
                Contacts |= Contacts.Ceiling;
            }
            else if (normal.X > SideThreshold)
            {
                Contacts |= Contacts.Left;
            }
            else if (normal.X < -SideThreshold)
            {
                Contacts |= Contacts.Right;
            }
        }
    }
}
