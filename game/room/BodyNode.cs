using BodyCheck.Core;

using Godot;

namespace BodyCheck;

/// <summary>
/// The engine's side of a <see cref="Core.Body"/>: a box that can only move in the XY plane and can't
/// rotate. The engine moves it and resolves its contacts. Friction, bounce and damping are off in the
/// engine: grip and drag are the rules' own, worked out by <see cref="World.Step"/>.
/// <para>
/// Each tick, <see cref="RoomNode"/> calls <see cref="PullMotion"/> before the controllers run and
/// <see cref="PushImpulse"/> after the world has stepped.
/// </para>
/// </summary>
public partial class BodyNode : RigidBody3D
{
    // How closely a contact normal must line up with an axis to count as a contact on that side.
    private const float SideThreshold = 0.7f;

    // How far every body extends along Z, which gameplay doesn't use.
    private const float Depth = 0.8f;

    public BodyNode(Body body, Color color, bool glows)
    {
        Body = body;
        Mass = body.Mass;

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

        var size = new Vector3(2 * body.HalfSize.X, 2 * body.HalfSize.Y, Depth);
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

    public Body Body { get; }

    /// <summary>Copies the engine's position and velocity into the body.</summary>
    public void PullMotion()
    {
        Vector3 position = Position;
        Vector3 velocity = LinearVelocity;
        Body.SetMotion(new Vector2(position.X, position.Y), new Vector2(velocity.X, velocity.Y));
    }

    /// <summary>Gives the engine the impulse that the world's step worked out for the body.</summary>
    public void PushImpulse()
    {
        Vector2 impulse = Body.TakeEngineImpulse();
        ApplyCentralImpulse(new Vector3(impulse.X, impulse.Y, 0));
    }

    public override void _IntegrateForces(PhysicsDirectBodyState3D state)
    {
        var contacts = Contacts.None;
        Body? support = null;
        for (int i = 0; i < state.GetContactCount(); i++)
        {
            // The normal points away from the other collider, towards this body.
            Vector3 normal = state.GetContactLocalNormal(i);
            if (normal.Y > SideThreshold)
            {
                contacts |= Contacts.Floor;
                support ??= (state.GetContactColliderObject(i) as BodyNode)?.Body;
            }
            else if (normal.Y < -SideThreshold)
            {
                contacts |= Contacts.Ceiling;
            }
            else if (normal.X > SideThreshold)
            {
                contacts |= Contacts.Left;
            }
            else if (normal.X < -SideThreshold)
            {
                contacts |= Contacts.Right;
            }
        }
        Body.SetContacts(contacts, support);
    }
}
