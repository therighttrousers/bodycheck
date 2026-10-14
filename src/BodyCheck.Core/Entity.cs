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
/// Something in a room that has a body: the player, a robot, a platform, a block. It is the engine's rigid
/// body itself, a box that can only move in the XY plane and can't rotate, so that what the engine reports
/// about bodies (contacts, what stands on what) is about entities.
/// <para>
/// An entity is a body and its looks. What it does is up to its <see cref="Controller"/>, which pushes
/// it with the engine's own forces and impulses, and with <see cref="ApplyTraction"/>. The room calls
/// <see cref="Step"/> on every entity once per physics tick.
/// </para>
/// <para>
/// Bounce and damping are off in the engine. Drag is the entity's own force. A walker has no engine
/// friction either: its grip is traction, which the engine applies as the motor of a joint between the
/// walker and whatever it stands on.
/// </para>
/// </summary>
public partial class Entity : RigidBody3D
{
    /// <summary>How far every entity extends along Z, which gameplay doesn't use.</summary>
    public const float Depth = 0.8f;

    // How closely a contact normal must line up with an axis to count as a contact on that side.
    private const float SideThreshold = 0.7f;

    private readonly Generic6DofJoint3D? _motor;
    private Entity? _motorAnchor;

    /// <param name="visual">What to draw for the entity, centered on the middle of its box.</param>
    public Entity(BodyTuning tuning, Node3D? visual = null)
    {
        Tuning = tuning;
        Mass = tuning.Mass;

        AxisLockLinearZ = true;
        AxisLockAngularX = true;
        AxisLockAngularY = true;
        AxisLockAngularZ = true;

        // Never rough: where two bodies touch, the smaller friction has to win, so that a walker's zero does.
        PhysicsMaterialOverride = new PhysicsMaterial { Friction = tuning.EngineFriction, Bounce = 0, Rough = false };
        LinearDampMode = DampMode.Replace;
        LinearDamp = 0;
        CanSleep = false;
        ContactMonitor = true;
        MaxContactsReported = 8;

        // Continuous detection keeps a walker's contacts exact, which its movement depends on.
        ContinuousCd = tuning.IsWalker;

        var size = new Vector3(2 * tuning.HalfSize.X, 2 * tuning.HalfSize.Y, Depth);
        AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = size } });

        if (visual is not null)
        {
            AddChild(visual);
        }

        if (tuning.IsWalker)
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

    public BodyTuning Tuning { get; }

    public Vector2 HalfSize => Tuning.HalfSize;

    /// <summary>What decides the entity's actions each tick. An entity without one is only pushed around.</summary>
    public ICharacterController? Controller { get; set; }

    /// <summary>Which sides touched tiles or other entities during the engine's last step.</summary>
    public Contacts Contacts { get; private set; }

    /// <summary>The entity this one stood on during the engine's last step, or null if it stood on tiles or on nothing.</summary>
    public Entity? Support { get; private set; }

    public bool OnGround => Contacts.HasFlag(Contacts.Floor);

    /// <summary>The velocity of whatever this entity stands on: zero for tiles, and in the air.</summary>
    public Vector3 GroundVelocity => Support?.LinearVelocity ?? Vector3.Zero;

    /// <summary>The force that gravity puts on the entity where it is.</summary>
    public float Weight => Mass * -GetGravity().Y;

    /// <summary>
    /// Uses the entity's grip on whatever it stands on, for the next step: pushes against the ground to
    /// bring the entity's horizontal speed, relative to the ground, towards <paramref name="targetSpeed"/>,
    /// with at most <paramref name="maxForce"/>, and pushes the ground the other way equally. A target of
    /// zero is braking. It has no effect in the air, or on an entity that isn't a walker.
    /// <para>
    /// The engine applies it, as the motor's target speed and force limit.
    /// </para>
    /// </summary>
    public void ApplyTraction(float targetSpeed, float maxForce)
    {
        if (_motor is null || !OnGround || maxForce <= 0)
        {
            return;
        }

        if (Support != _motorAnchor)
        {
            _motorAnchor = Support;
            _motor.NodeA = _motorAnchor is null ? new NodePath() : _motor.GetPathTo(_motorAnchor);
        }
        _motor.SetParamX(Generic6DofJoint3D.Param.LinearMotorTargetVelocity, targetSpeed);
        _motor.SetParamX(Generic6DofJoint3D.Param.LinearMotorForceLimit, maxForce);
        _motor.SetFlagX(Generic6DofJoint3D.Flag.EnableLinearMotor, true);
    }

    /// <summary>Call once per physics tick, before the engine's step.</summary>
    /// <param name="controlled">Whether the controller runs. Without it, the entity only coasts, under drag and gravity.</param>
    public void Step(float dt, bool controlled = true)
    {
        // Traction lasts one tick, like a force: the motor is off unless the controller applies it again.
        _motor?.SetFlagX(Generic6DofJoint3D.Flag.EnableLinearMotor, false);

        if (controlled)
        {
            Controller?.Apply(this, dt);
        }

        ApplyCentralForce(-Tuning.Drag * LinearVelocity);
    }

    public override void _IntegrateForces(PhysicsDirectBodyState3D state)
    {
        var contacts = Contacts.None;
        Entity? support = null;
        for (int i = 0; i < state.GetContactCount(); i++)
        {
            // The normal points away from the other collider, towards this body.
            Vector3 normal = state.GetContactLocalNormal(i);
            if (normal.Y > SideThreshold)
            {
                contacts |= Contacts.Floor;
                support ??= state.GetContactColliderObject(i) as Entity;
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
        Contacts = contacts;
        Support = support;
    }
}
