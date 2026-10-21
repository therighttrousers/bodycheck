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
/// block: it is only pushed, carried and stood on. A subclass is a kind of body that has a controller, and
/// may give it ways to use the entity's grip. The room calls <see cref="Step"/> on every entity once per
/// physics tick.
/// </para>
/// <para>
/// Bounce and damping are off in the engine. Drag is the entity's own force.
/// </para>
/// <para>
/// Every entity grips what it stands on with the motor of a joint between the two, along X. On its own, the
/// motor is friction, where the engine's friction falls short: on a walker, whose engine friction is 0. A
/// walker's controller adds traction to it.
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

    // How many times a move through entities may slide along a tile before it stops short.
    private const int MaxSlides = 4;

    // How far the grip's joint may be above or below where it should act before it is anchored again there.
    private const float GripDrift = 0.05f;

    // Where to move to through entities before the engine's next step, if anywhere.
    private Vector3? _destination;

    // The joint whose motor grips what the entity stands on: friction, and a walker's traction. It constrains
    // nothing: every limit is off, so its only effect is the X motor. The entity is the joint's second body, and
    // the first is what it is anchored to, or nothing for tiles. It has no bodies until it is first anchored.
    private readonly Generic6DofJoint3D _grip;
    private bool _gripAnchored;
    private Entity? _gripAnchor;
    // Where the joint is, in the entity's frame: where the engine's joint acts on it.
    private Vector3 _gripPoint;

    // What the entity stood on during the engine's last step: the collider, and the middle of the contacts with it.
    private Rid _groundCollider;
    private Vector3 _groundContact;

    // The entities that touched this one from above during the engine's last step.
    private readonly List<Entity> _above = [];

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
        CollisionLayer = (uint)tuning.CollisionLayer;
        CollisionMask = (uint)PhysicsLayer.DefaultCollisionMask;
        ContactMonitor = true;
        MaxContactsReported = 8;
        ContinuousCd = continuousCd;

        var size = new Vector3(2 * tuning.HalfSize.X, 2 * tuning.HalfSize.Y, Depth);
        AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = size } });

        _grip = new Generic6DofJoint3D { ExcludeNodesFromCollision = false };
        foreach (var flag in (ReadOnlySpan<Generic6DofJoint3D.Flag>)
            [Generic6DofJoint3D.Flag.EnableLinearLimit, Generic6DofJoint3D.Flag.EnableAngularLimit])
        {
            _grip.SetFlagX(flag, false);
            _grip.SetFlagY(flag, false);
            _grip.SetFlagZ(flag, false);
        }
        AddChild(_grip);

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

    /// <summary>Whether the engine's last step brought the entity onto the ground from the air.</summary>
    public bool Landed { get; private set; }

    /// <summary>
    /// The velocity of the surface this entity stood on during the engine's last step, where it touched: zero
    /// for tiles, and in the air. On a support that spins, it differs from the support's own velocity.
    /// </summary>
    public Vector3 GroundVelocity { get; private set; }

    /// <summary>The force that gravity puts on the entity where it is.</summary>
    public float Weight => Mass * -GetGravity().Y;

    /// <summary>
    /// The entity's weight and the loads of the entities that stand on it, and so on up the stack, as of the engine's
    /// last step: how hard it presses on what it stands on, at rest.
    /// </summary>
    public float ComputeLoad()
    {
        float load = Weight;
        foreach (Entity other in _above)
        {
            if (other.Support == this)
            {
                load += other.ComputeLoad();
            }
        }
        return load;
    }

    /// <summary>
    /// The most force that the entity's friction with what it stands on uses this tick, along X. Zero in the air, and
    /// where the engine's own friction is enough. Traction adds to it.
    /// </summary>
    public float Friction { get; private set; }

    /// <summary>
    /// The coefficient of friction with which this kind of body grips what it stands on. The motor makes up what the
    /// engine's friction falls short of it.
    /// </summary>
    protected virtual float GroundFriction => Tuning.EngineFriction;

    /// <summary>
    /// The coefficient of friction with which this kind of body grips what stands on it, which wins over that body's
    /// own: a walker grabs what it carries (019 §1.4). Null for a body that leaves what stands on it to its own.
    /// </summary>
    public virtual float? CarryFriction => null;

    /// <summary>
    /// How firmly this kind of body holds the rotation of what stands on it: the most torque it uses, as a multiple of
    /// that body's load, so a length. A hand holds what it carries this way, where friction alone would let it tip
    /// (019 §4.10). Null for a body that doesn't.
    /// </summary>
    public virtual float? CarryHold => null;

    /// <summary>
    /// Moves the entity toward <paramref name="destination"/> before the engine's next step, through other entities
    /// but not through tiles: where it meets one, it slides along it, so it may stop short. It keeps its velocity, and
    /// the engine pushes it apart from whatever it ends up overlapping. Until then it stays where it is, so that
    /// controllers stepped after this one in the same tick still see the world as of the engine's last step.
    /// </summary>
    public void MoveThroughEntities(Vector3 destination)
    {
        _destination = destination;
        // What it stands on is from before the move, so it has no grip until the engine's next step.
        ReleaseGrip();
    }

    // Every entity's step runs before this, because the room that steps them is their parent, and a parent is
    // processed before its children.
    public override void _PhysicsProcess(double delta)
    {
        if (_destination is not Vector3 destination)
        {
            return;
        }
        _destination = null;

        // Only for this move, which is over within the call: the engine's step sees the mask as it was.
        uint mask = CollisionMask;
        CollisionMask = (uint)PhysicsLayer.Static;
        Vector3 motion = destination - Position;
        for (int i = 0; i <= MaxSlides && MoveAndCollide(motion) is KinematicCollision3D collision; i++)
        {
            motion = collision.GetRemainder().Slide(collision.GetNormal());
        }
        CollisionMask = mask;

        // Drawn there at once, not interpolated across the room.
        ResetPhysicsInterpolation();
    }

    /// <summary>The colliders, other than this entity, that overlap a shape centered at a place.</summary>
    public IEnumerable<GodotObject> Overlapping(Shape3D shape, Vector3 center)
    {
        var query = new PhysicsShapeQueryParameters3D
        {
            Shape = shape,
            Transform = new Transform3D(Basis.Identity, center),
            Exclude = [GetRid()],
        };
        return GetWorld3D().DirectSpaceState.IntersectShape(query)
            .Select(result => result["collider"].AsGodotObject());
    }

    /// <summary>Call once per physics tick, before the engine's step.</summary>
    /// <param name="gameDt">The tick's length in game time, which the engine's time scale shortens.</param>
    /// <param name="realDt">The tick's length in real time, which the time scale doesn't change.</param>
    /// <param name="controlled">Whether the controller runs. Without it, the entity only coasts, under drag, gravity and friction.</param>
    public void Step(float gameDt, float realDt, bool controlled = true)
    {
        // A motor's push lasts one tick, like a force: it is off unless friction or the controller uses it again.
        ReleaseGrip();

        ApplyFriction(gameDt);

        if (controlled)
        {
            Control(gameDt, realDt);
        }

        ApplyCentralForce(-Tuning.Drag * LinearVelocity);
    }

    /// <summary>
    /// Call once per physics tick while the game is paused because a controller asked (see
    /// <see cref="RequestsPause"/>), instead of <see cref="Step"/>. It runs the controller's paused step only.
    /// </summary>
    /// <param name="realDt">The tick's length in real time. No game time passes.</param>
    public void StepPaused(float realDt) => ControlPaused(realDt);

    /// <summary>Whether this entity's controller asks for the game to be paused.</summary>
    public virtual bool RequestsPause => false;

    /// <summary>Runs the paused step of the controller of this kind of body, if it has one.</summary>
    protected virtual void ControlPaused(float realDt)
    {
    }

    // Grips what the entity stands on, for the next step, as far as the engine's friction falls short (019 §1.5): the
    // coefficient times the load. The coefficient is what the entity stands on carries with, if it has one, or else the
    // entity's own. The engine's bodies aren't rough, so it uses the smaller of their two coefficients.
    private void ApplyFriction(float gameDt)
    {
        // Before a move through entities, what it stands on is from before the move.
        if (!OnGround || _destination is not null)
        {
            return;
        }

        float engineFriction = Mathf.Min(
            PhysicsMaterialOverride.Friction,
            PhysicsServer3D.BodyGetParam(_groundCollider, PhysicsServer3D.BodyParameter.Friction).AsSingle());
        float shortfall = (Support?.CarryFriction ?? GroundFriction) - engineFriction;
        if (shortfall <= 0)
        {
            return;
        }

        Friction = shortfall * ComputeLoad();
        if (Landed)
        {
            // Whatever lands stops slipping at once (019 §2.1): on top, as much force as stops the slip within the tick.
            Friction += Mass * MathF.Abs(LinearVelocity.X - GroundVelocity.X) / gameDt;
        }
        DriveGrip(0, Friction);

        // What carries the entity may also hold its rotation to its own, if it can turn.
        if (Support?.CarryHold is float hold && !AxisLockAngularZ)
        {
            _grip.SetParamZ(Generic6DofJoint3D.Param.AngularMotorTargetVelocity, 0);
            _grip.SetParamZ(Generic6DofJoint3D.Param.AngularMotorForceLimit, hold * ComputeLoad());
            _grip.SetFlagZ(Generic6DofJoint3D.Flag.EnableMotor, true);
        }
    }

    private void ReleaseGrip()
    {
        _grip.SetFlagX(Generic6DofJoint3D.Flag.EnableLinearMotor, false);
        _grip.SetFlagZ(Generic6DofJoint3D.Flag.EnableMotor, false);
        Friction = 0;
    }

    /// <summary>
    /// Drives the entity's grip on what it stands on, for the next step: pushes against the ground to bring the
    /// entity's horizontal speed, relative to the ground, towards <paramref name="targetSpeed"/>, with at most
    /// <paramref name="maxForce"/>, and pushes the ground the other way equally. It replaces what the grip did this
    /// tick, and has no effect in the air. With no force, the grip is off.
    /// </summary>
    protected void DriveGrip(float targetSpeed, float maxForce)
    {
        if (!OnGround || maxForce <= 0)
        {
            _grip.SetFlagX(Generic6DofJoint3D.Flag.EnableLinearMotor, false);
            return;
        }

        // The grip acts where the entity touches what it stands on (019 §2.2), except in a landing's catch on a body
        // that rotates (019 §4.3). A real impact's friction comes with its normal force, which spreads across the face
        // and keeps the friction from tipping the body; the catch has no impact with it, so it acts at the center.
        Vector3 point = Landed && !AxisLockAngularZ ? Position : _groundContact;
        // The motor pushes only along X, so only the joint's height changes what it does.
        if (!_gripAnchored || Support != _gripAnchor || MathF.Abs(ToGlobal(_gripPoint).Y - point.Y) > GripDrift)
        {
            AnchorGrip(point);
        }
        _grip.SetParamX(Generic6DofJoint3D.Param.LinearMotorTargetVelocity, targetSpeed);
        _grip.SetParamX(Generic6DofJoint3D.Param.LinearMotorForceLimit, maxForce);
        _grip.SetFlagX(Generic6DofJoint3D.Flag.EnableLinearMotor, true);
    }

    // Godot builds the engine's joint when a node path changes to a new value, and fixes the joint's place in each body
    // then, from where the joint node is (019 §2.2). So the joint is put where it acts, level, first. Node B is cleared
    // before node A is set, so that setting it again builds the joint even when node A is unchanged.
    private void AnchorGrip(Vector3 point)
    {
        _gripAnchored = true;
        _gripAnchor = Support;
        _grip.GlobalTransform = new Transform3D(Basis.Identity, point);
        _gripPoint = ToLocal(point);
        _grip.NodeB = new NodePath();
        _grip.NodeA = Support is null ? new NodePath() : _grip.GetPathTo(Support);
        _grip.NodeB = new NodePath("..");
    }

    /// <summary>Runs the controller of this kind of body, if it has one.</summary>
    protected virtual void Control(float gameDt, float realDt)
    {
    }

    public override void _IntegrateForces(PhysicsDirectBodyState3D state)
    {
        bool wasOnGround = OnGround;
        var contacts = Contacts.None;
        Entity? support = null;
        Rid groundCollider = default;
        // Summed over the floor contacts with the support, which may be several, e.g. the corners of a box.
        Vector3 groundVelocity = Vector3.Zero;
        Vector3 groundContact = Vector3.Zero;
        int groundContacts = 0;
        _above.Clear();
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
                if (state.GetContactColliderObject(i) is Entity above && !_above.Contains(above))
                {
                    _above.Add(above);
                }
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

        // What the entity stands on: the support, or else the tiles.
        for (int i = 0; i < state.GetContactCount(); i++)
        {
            if (state.GetContactLocalNormal(i).Y <= SideThreshold
                || (support is not null && state.GetContactColliderObject(i) != support))
            {
                continue;
            }
            if (groundContacts == 0)
            {
                groundCollider = state.GetContactCollider(i);
            }
            if (state.GetContactCollider(i) == groundCollider)
            {
                groundVelocity += state.GetContactColliderVelocityAtPosition(i);
                groundContact += state.GetContactLocalPosition(i);
                groundContacts++;
            }
        }

        Contacts = contacts;
        Support = support;
        Landed = !wasOnGround && OnGround;
        _groundCollider = groundCollider;
        _groundContact = groundContacts > 0 ? groundContact / groundContacts : Position;
        // Tiles don't move.
        GroundVelocity = support is not null && groundContacts > 0 ? groundVelocity / groundContacts : Vector3.Zero;
    }
}
