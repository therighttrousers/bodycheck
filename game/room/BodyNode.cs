using System;

using BodyCheck.Core;

using Godot;

namespace BodyCheck;

/// <summary>
/// The engine's side of a <see cref="Core.Body"/>: a box that can only move in the XY plane and can't
/// rotate. The engine moves it and resolves its contacts. Bounce and damping are off in the engine, and so
/// is friction for a walker. Drag is the rules' own, worked out by <see cref="World.Step"/>. A walker's
/// grip is its <see cref="Traction"/>, which the engine applies as the motor of a joint between the
/// walker and whatever it stands on.
/// <para>
/// Each tick, <see cref="RoomNode"/> calls <see cref="PullMotion"/> before the controllers run, and
/// <see cref="PushImpulse"/> and <see cref="PushTraction"/> after the world has stepped.
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

        // Never rough: where two bodies touch, the smaller friction has to win, so that a walker's zero does.
        PhysicsMaterialOverride = new PhysicsMaterial { Friction = body.EngineFriction, Bounce = 0, Rough = false };
        LinearDampMode = DampMode.Replace;
        LinearDamp = 0;
        CanSleep = false;
        ContactMonitor = true;
        MaxContactsReported = 8;

        // Continuous detection keeps a walker's contacts exact, which its movement depends on.
        ContinuousCd = body.IsWalker;

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

        if (body.IsWalker)
        {
            // A joint that constrains nothing: every limit is off, so its only effect is the X motor. The
            // walker is the joint's second body. The first is what it stands on, or nothing for tiles.
            _motor = new Generic6DofJoint3D { NodeB = new NodePath(".."), ExcludeNodesFromCollision = false };
            foreach (var flag in (ReadOnlySpan<Generic6DofJoint3D.Flag>)
                [Generic6DofJoint3D.Flag.EnableLinearLimit, Generic6DofJoint3D.Flag.EnableAngularLimit])
            {
                _motor.SetFlagX(flag, false);
                _motor.SetFlagY(flag, false);
                _motor.SetFlagZ(flag, false);
            }
            AddChild(_motor);
        }
    }

    private readonly Generic6DofJoint3D? _motor;
    private BodyNode? _motorAnchor;
    private bool _motorOn;

    public Body Body { get; }

    /// <summary>The node of the body this one stood on during the engine's last step, if any.</summary>
    public BodyNode? SupportNode { get; private set; }

    /// <summary>
    /// Gives the engine a walker's traction, as the motor's target speed and force limit. The motor is off
    /// in the air, and for a body with no grip.
    /// </summary>
    public void PushTraction()
    {
        if (_motor is null)
        {
            return;
        }

        Traction traction = Body.Traction;
        bool on = Body.OnGround && traction.MaxForce > 0;
        if (on)
        {
            if (SupportNode != _motorAnchor)
            {
                _motorAnchor = SupportNode;
                _motor.NodeA = _motorAnchor is null ? new NodePath() : _motor.GetPathTo(_motorAnchor);
            }
            _motor.SetParamX(Generic6DofJoint3D.Param.LinearMotorTargetVelocity, traction.TargetSpeed);
            _motor.SetParamX(Generic6DofJoint3D.Param.LinearMotorForceLimit, traction.MaxForce);
        }
        if (on != _motorOn)
        {
            _motorOn = on;
            _motor.SetFlagX(Generic6DofJoint3D.Flag.EnableLinearMotor, on);
        }
    }

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
        BodyNode? supportNode = null;
        for (int i = 0; i < state.GetContactCount(); i++)
        {
            // The normal points away from the other collider, towards this body.
            Vector3 normal = state.GetContactLocalNormal(i);
            if (normal.Y > SideThreshold)
            {
                contacts |= Contacts.Floor;
                supportNode ??= state.GetContactColliderObject(i) as BodyNode;
                support ??= supportNode?.Body;
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
        SupportNode = supportNode;
    }
}
