using Godot;

namespace BodyCheck.Core.Entities;

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
/// body itself, a box that can only move in the XY plane, so that what the engine reports about bodies
/// (contacts, what stands on what) is about entities. Entities may only rotate about Z (default off).
/// <para>
/// An entity is a body and its looks. This class is a body with nothing to move itself by, such as a
/// block: it is only pushed, carried and stood on. A subclass is a kind of body that has motors, and a
/// controller to use them. The room calls <see cref="Step"/> on every entity once per physics tick.
/// </para>
/// <para>
/// Bounce and damping are off in the engine. Drag is the entity's own force.
/// </para>
/// <para>
/// An entity is also the base node for advance expressions in the entity's visual; i.e. in the AnimationTree
/// editor, while writing advance expressions for state machine transition conditions, the entity's public
/// properties and fields whose types Godot understands are all available. For example, an advance expression
/// may refer to `OnGround`.
/// </para>
/// </summary>
public partial class Entity : RigidBody3D
{
    /// <summary>How far every entity extends along Z, which gameplay doesn't use.</summary>
    public const float Depth = 0.8f;

    // How closely a contact normal must line up with an axis to count as a contact on that side.
    private const float SideThreshold = 0.7f;

    /// <param name="visual">What to draw for the entity, centered on the middle of its box.</param>
    public Entity(BodyTuning tuning, Node3D? visual = null, bool axisLockAngularZ = true)
        : this(tuning, visual, tuning.EngineFriction, continuousCd: false, axisLockAngularZ)
    {
    }

    /// <param name="engineFriction">The engine's coefficient of friction for this kind of body.</param>
    /// <param name="continuousCd">Whether the engine uses continuous collision detection for it.</param>
    protected Entity(BodyTuning tuning, Node3D? visual, float engineFriction, bool continuousCd, bool axisLockAngularZ)
    {
        Tuning = tuning;
        Mass = tuning.Mass;

        AxisLockLinearZ = true;
        AxisLockAngularX = true;
        AxisLockAngularY = true;
        AxisLockAngularZ = axisLockAngularZ;

        // Never rough: where two bodies touch, the smaller friction has to win, so that a walker's zero does.
        PhysicsMaterialOverride = new PhysicsMaterial { Friction = engineFriction, Bounce = 0, Rough = false };
        LinearDampMode = DampMode.Replace;
        LinearDamp = 0;
        CanSleep = false;
        ContactMonitor = true;
        MaxContactsReported = 8;
        ContinuousCd = continuousCd;

        var size = new Vector3(2 * tuning.HalfSize.X, 2 * tuning.HalfSize.Y, Depth);
        AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = size } });

        if (visual is not null)
        {
            visual.Position -= new Vector3(0, tuning.HalfSize.Y, 0);
            Visual = visual;
            AddChild(visual);

            // Resolve advancement expression variable references using this instance
            if (visual.HasNode("AnimationTree"))
            {
                AnimationTree = visual.GetNode<AnimationTree>("AnimationTree");
                AnimationTree.AdvanceExpressionBaseNode = AnimationTree.GetPathTo(this);
            }
        }
    }

    protected Node3D? Visual { get; }
    protected AnimationTree? AnimationTree { get; }

    public BodyTuning Tuning { get; }

    public Vector2 HalfSize => Tuning.HalfSize;

    /// <summary>Which sides touched tiles or other entities during the engine's last step.</summary>
    public Contacts Contacts { get; private set; }

    /// <summary>The entity this one stood on during the engine's last step, or null if it stood on tiles or on nothing.</summary>
    public Entity? Support { get; private set; }

    public bool OnGround => Contacts.HasFlag(Contacts.Floor);

    /// <summary>
    /// The velocity of the surface this entity stood on during the engine's last step, where it touched: zero
    /// for tiles, and in the air. On a support that spins, it differs from the support's own velocity.
    /// </summary>
    public Vector3 GroundVelocity { get; private set; }

    /// <summary>The force that gravity puts on the entity where it is.</summary>
    public float Weight => Mass * -GetGravity().Y;

    /// <summary>Call once per physics tick, before the engine's step.</summary>
    /// <param name="controlled">Whether the controller runs. Without it, the entity only coasts, under drag and gravity.</param>
    public void Step(float dt, bool controlled = true)
    {
        // A motor's push lasts one tick, like a force: it is off unless the controller uses it again.
        ReleaseMotors();

        if (controlled)
        {
            Control(dt);
        }

        ApplyCentralForce(-Tuning.Drag * LinearVelocity);
    }

    /// <summary>Switches off every motor this kind of body has.</summary>
    protected virtual void ReleaseMotors()
    {
    }

    /// <summary>Runs the controller of this kind of body, if it has one.</summary>
    protected virtual void Control(float dt)
    {
    }

    public override void _IntegrateForces(PhysicsDirectBodyState3D state)
    {
        var contacts = Contacts.None;
        Entity? support = null;
        // Summed over the floor contacts with the support, which may be several, e.g. the corners of a box.
        Vector3 groundVelocity = Vector3.Zero;
        int groundContacts = 0;
        for (int i = 0; i < state.GetContactCount(); i++)
        {
            // The normal points away from the other collider, towards this body.
            Vector3 normal = state.GetContactLocalNormal(i);
            if (normal.Y > SideThreshold)
            {
                contacts |= Contacts.Floor;
                support ??= state.GetContactColliderObject(i) as Entity;
                if (support is not null && state.GetContactColliderObject(i) == support)
                {
                    groundVelocity += state.GetContactColliderVelocityAtPosition(i);
                    groundContacts++;
                }
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
        GroundVelocity = groundContacts > 0 ? groundVelocity / groundContacts : Vector3.Zero;
    }
}
