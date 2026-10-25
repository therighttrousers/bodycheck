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
/// Something that happened to an entity, for the scene that draws it to react to: for now, by playing the sound named
/// after it, if it has one. A controller raises it on its entity with <see cref="Entity.RaiseCue"/> (decision record
/// 021).
/// </summary>
public enum EntityCue
{
    /// <summary>The player jumped.</summary>
    Jumped,

    /// <summary>The player began aiming a phase.</summary>
    PhaseBegan,

    /// <summary>The player phased: released it with a direction held.</summary>
    PhaseThrown,

    /// <summary>The player cancelled a phase: released it with no direction held.</summary>
    PhaseCancelled,

    /// <summary>The player spawned this block under itself.</summary>
    Spawned,

    /// <summary>The player pressed spawn where its block doesn't fit, or where there is no room to rise onto it.</summary>
    SpawnCancelled,
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
/// Friction is the engine's. At the end of each tick the entity sets its coefficient for this tick's step, from what it
/// stands on: what a carrier holds takes the carrier's coefficient, and a walker has none off the ground, or while
/// its controller lets it slip (027). A walker's traction is the motor of a joint between it and what it stands on,
/// along X. What carries an entity may also steady it with a second joint's motor.
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
    // How far every entity's collider extends along Z, which gameplay doesn't use.
    private const float Depth = 0.8f;

    /// <summary>
    /// How far, in tiles, a box may overlap a collider and still count as only touching it: what <see cref="RoomToMove"/>
    /// ignores where a box starts, and so how far short of a fit its answers may come.
    /// <para>
    /// It must be above zero, since the engine's cast misses everything when the box starts touching something, and
    /// above how far short the cast's answers come, about 0.002 (measured under Jolt). It isn't Jolt's collision margin
    /// (the project setting <c>collision_margin_fraction</c>), which rounds a box's corners by a fraction of its
    /// shortest side, 0.005 on a tile, without moving its faces, so it doesn't change where boxes touch. Changing one
    /// doesn't call for changing the other.
    /// </para>
    /// </summary>
    public const float ContactTolerance = 0.01f;

    // How many contacts the engine reports for an entity. Past it, it drops contacts, which may be all the floor's
    // (024 §3.1). Every collider an entity touches gives 4, at the corners of the face, so this allows for 16: a
    // player against several tile boxes on each side, the floor and the ceiling, and entities, with room to spare.
    private const int MaxContacts = 64;

    // How closely a contact normal must line up with an axis to count as a contact on that side.
    private const float SideThreshold = 0.7f;

    // How many times a move through entities may slide along a tile before it stops short.
    private const int MaxSlides = 4;

    // How far the grip's joint may be above or below where it should act before it is anchored again there.
    private const float GripDrift = 0.05f;

    // How fast what steadies an entity turns it towards level, in radians per second for each radian it is off.
    private const float LevelingRate = 10;

    // The joint whose motor grips what the entity stands on: friction, and a walker's traction. It constrains
    // nothing: every limit is off, so its only effect is the X motor. The entity is the joint's second body, and
    // the first is what it is anchored to, or nothing for tiles. It has no bodies until it is first anchored.
    private readonly Generic6DofJoint3D _grip;
    private bool _gripAnchored;
    private Entity? _gripAnchor;
    // Where the joint is, in the entity's frame: where the engine's joint acts on it.
    private Vector3 _gripPoint;

    // Where the entity stood on what it stood on during the engine's last step: the middle of the contacts with it.
    private Vector3 _groundContact;

    // Whether the controller lets the entity slip this tick. See Slip.
    private bool _slipAtEndOfTick;

    // The joint whose motor steadies the entity at its center, for what carries it like a hand (019 §5.1). Like the
    // grip's, it constrains nothing, and has no bodies until it is first anchored.
    private readonly Generic6DofJoint3D _steadying;
    private bool _steadyingAnchored;
    private Entity? _steadyingAnchor;

    // Whether the entity has warned that the engine reported as many contacts as it may, once, so as not to every tick.
    private bool _warnedOfMaxContacts;

    /// <param name="visual">What to draw for the entity, centered on the middle of its box.</param>
    public Entity(BodyTuning tuning, Node3D? visual = null, bool axisLockAngularZ = true)
        : this(tuning, visual, continuousCd: false, axisLockAngularZ)
    {
    }

    /// <param name="continuousCd">Whether the engine uses continuous collision detection for it.</param>
    protected Entity(BodyTuning tuning, Node3D? visual, bool continuousCd, bool axisLockAngularZ)
    {
        Tuning = tuning;
        Mass = tuning.Mass;

        AxisLockLinearZ = true;
        AxisLockAngularX = true;
        AxisLockAngularY = true;
        AxisLockAngularZ = axisLockAngularZ;

        // Its own, since SetFrictionAtEndOfTick changes it every tick. Until the first, the tuning's.
        PhysicsMaterialOverride = new PhysicsMaterial { Friction = tuning.EngineFriction, Bounce = 0, Rough = false };
        LinearDampMode = DampMode.Replace;
        LinearDamp = 0;
        CanSleep = false;
        CollisionLayer = (uint)tuning.CollisionLayer;
        CollisionMask = (uint)PhysicsLayer.DefaultCollisionMask;
        ContactMonitor = true;
        MaxContactsReported = MaxContacts;
        ContinuousCd = continuousCd;

        var size = new Vector3(2 * tuning.HalfSize.X, 2 * tuning.HalfSize.Y, Depth);
        AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = size } });

        _grip = FreeJoint();
        AddChild(_grip);
        _steadying = FreeJoint();
        AddChild(_steadying);

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

    /// <summary>Raised by <see cref="RaiseCue"/>, for the scene that draws the entity to react to, such as by playing a sound.</summary>
    public event Action<EntityCue>? CueRaised;

    /// <summary>Says that <paramref name="cue"/> happened to the entity, so that what draws it may react.</summary>
    public void RaiseCue(EntityCue cue) => CueRaised?.Invoke(cue);

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
    /// <para>
    /// Sets this entity's GravityScale after all entity steps, and after the physics engine step - conceptually.
    /// </para>
    /// <para>
    /// Prefer to call this instead of calling SetGravityScale or setting GravityScale, for step-order-independence.
    /// </para>
    /// <para>
    /// This actually sets GravityScale after all entity steps, but *before* the physics engine step. To appear to
    /// set GravityScale after the physics engine step, it adds a compensating force, which is opposite the gravity
    /// change.
    /// </para>
    /// </summary>
    public void SetGravityScaleAfterTick(float scale, IEndOfTick endOfTick) =>
        endOfTick.Enqueue(() =>
        {
            Vector3 before = GetGravity();
            base.GravityScale = scale;
            // Controllers this tick reckoned with the old gravity, so the step gets the difference back.
            ApplyCentralForce(Mass * (before - GetGravity()));
        });

    // The tuning's values for gripping and carrying, from the features in effect now. See BodyTuning.
    private float? GroundFriction => Tuning.GroundFriction?.Invoke(Features.Current);
    private float? CarryFriction => Tuning.CarryFriction?.Invoke(Features.Current);
    private float? CarrySteadying => Tuning.CarrySteadying?.Invoke(Features.Current);
    private float? CarryTorque => Tuning.CarryTorque?.Invoke(Features.Current);

    /// <summary>
    /// Moves the entity toward <paramref name="destination"/> at once, through other entities but not through tiles:
    /// where it meets one, it slides along it, so it may stop short. It keeps its velocity, and the engine pushes it
    /// apart from whatever it ends up overlapping. A controller defers it to the end of the tick
    /// (<see cref="IEndOfTick"/>), so that the controllers stepped after it still see the world as of the engine's last
    /// step.
    /// </summary>
    public void MoveThroughEntities(Vector3 destination)
    {
        // What it stands on is from before the move, so it has no grip in this tick's step.
        ReleaseGrip();

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

    /// <summary>
    /// Moves the entity to <paramref name="destination"/> at once, through everything, for a place already found to be
    /// clear of tiles, such as with <see cref="RoomToMove"/>. Otherwise like <see cref="MoveThroughEntities"/>: it keeps
    /// its velocity, the engine pushes it apart from whatever it ends up overlapping, and a controller defers it to the
    /// end of the tick.
    /// </summary>
    public void MoveTo(Vector3 destination)
    {
        ReleaseGrip();
        GlobalPosition = destination;
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

    /// <summary>
    /// How far a box as deep as an entity's, centered at a place, can move in a direction before it meets a collider in
    /// the given layers, up to a distance, as of the engine's last step. It doesn't meet this entity. What it only
    /// touches where it starts doesn't stop it, but what it starts overlapping by more than that may not either.
    /// </summary>
    /// <param name="direction">A unit vector.</param>
    public float RoomToMove(Vector2 halfSize, Vector3 center, Vector3 direction, float distance, PhysicsLayer layers)
    {
        // The engine's cast misses everything when the box starts touching something, which a box beside a wall or on
        // the floor does. So it casts a box smaller by a little on every side, which starts clear, that much further.
        var shrunk = new Vector3(2 * (halfSize.X - ContactTolerance), 2 * (halfSize.Y - ContactTolerance), Depth);
        var query = new PhysicsShapeQueryParameters3D
        {
            Shape = new BoxShape3D { Size = shrunk },
            Transform = new Transform3D(Basis.Identity, center),
            Motion = (distance + ContactTolerance) * direction,
            CollisionMask = (uint)layers,
            Exclude = [GetRid()],
        };
        // The fraction of the motion it can make without touching anything, then the fraction where it first would.
        float safe = GetWorld3D().DirectSpaceState.CastMotion(query)[0];
        return Math.Clamp(safe * (distance + ContactTolerance) - ContactTolerance, 0, distance);
    }

    /// <summary>Call once per physics tick, before the engine's step.</summary>
    /// <param name="gameDt">The tick's length in game time, which the engine's time scale shortens.</param>
    /// <param name="realDt">The tick's length in real time, which the time scale doesn't change.</param>
    /// <param name="endOfTick">Runs actions once every entity has been stepped this tick.</param>
    /// <param name="controlled">Whether the controller runs. Without it, the entity only coasts, under drag, gravity and friction.</param>
    public void Step(float gameDt, float realDt, IEndOfTick endOfTick, bool controlled = true)
    {
        // A motor's push lasts one tick, like a force: it is off unless steadying or the controller uses it again. So
        // does a controller's slip.
        ReleaseGrip();
        _slipAtEndOfTick = false;

        Steady();

        if (controlled)
        {
            Control(gameDt, realDt, endOfTick);
        }

        ApplyCentralForce(-Tuning.Drag * LinearVelocity);

        // Change the material's friction settings after all controllers run for order-independence (in case a controller inspects materials).
        SetFrictionAtEndOfTick(endOfTick);
    }

    /// <summary>
    /// Call once per physics tick while the game is paused because a controller asked (see
    /// <see cref="RequestsPause"/>), instead of <see cref="Step"/>. It runs the controller's paused step only.
    /// </summary>
    /// <param name="realDt">The tick's length in real time. No game time passes.</param>
    /// <param name="endOfTick">Runs actions once every entity has been stepped this tick.</param>
    public void StepPaused(float realDt, IEndOfTick endOfTick) => ControlPaused(realDt, endOfTick);

    /// <summary>Whether this entity's controller asks for the game to be paused.</summary>
    public virtual bool RequestsPause => false;

    /// <summary>Runs the paused step of the controller of this kind of body, if it has one.</summary>
    protected virtual void ControlPaused(float realDt, IEndOfTick endOfTick)
    {
    }

    /// <summary>
    /// Lets the entity slip on what it stands on in this tick's step: at the end of the tick its friction goes off, so
    /// that only what the controller does with the grip moves it against its support. It lasts one tick.
    /// </summary>
    protected void SlipAtEndOfTick() => _slipAtEndOfTick = true;

    // Sets the engine's friction at the end of the tick, for this tick's step, from what the entity stood on during the
    // last step (027 §1). The engine uses the smaller of two bodies' coefficients, except that a rough one wins, and of
    // two rough ones the larger (Godot passes a rough coefficient to Jolt as negative, and Jolt takes the absolute value
    // of the smaller). So what a carrier holds is rough, with the carrier's coefficient, which wins over the carrier's
    // own: a walker's is zero while it slips. The engine multiplies it by the normal force, so the grip grows with what
    // is stacked above, and comes with a landing's impact. But a body's coefficient is for all its contacts, so while
    // what is carried touches something at its side, it has its own, not rough, and doesn't grip a wall with its
    // carrier's (027 §4). Something with a ground coefficient is rough too, so that it wins over the tiles' and a
    // block's, but it grips only what it stands on: off the ground it has no friction, so that it slides along walls.
    private void SetFrictionAtEndOfTick(IEndOfTick endOfTick)
    {
        endOfTick.Enqueue(() =>
        {
            float friction = Tuning.EngineFriction;
            bool rough = false;
            if (_slipAtEndOfTick || (GroundFriction is not null && !OnGround))
            {
                friction = 0;
            }
            else if (OnGround && Tuning.CanBeCarried && Support?.CarryFriction is float carryFriction
                && (Contacts & (Contacts.Left | Contacts.Right)) == Contacts.None)
            {
                friction = carryFriction;
                rough = true;
            }
            else if (GroundFriction is float groundFriction)
            {
                friction = groundFriction;
                rough = true;
            }

            // Godot reloads the body's material on every change, even to the same value.
            PhysicsMaterial material = PhysicsMaterialOverride;
            if (material.Friction != friction)
            {
                material.Friction = friction;
            }
            if (material.Rough != rough)
            {
                material.Rough = rough;
            }
        });
    }

    // What carries the entity may steady it, like a hand held up behind its head: the second joint's motor, at the
    // entity's center, holds the center's speed to the carrier's, so that friction at the base needn't, and doesn't tip
    // it (019 §5.1). The engine solves it with everything else, in the same step. At the center, it would also keep the
    // entity from tipping off an edge, so the hand lets go once the center is past the carrier's side.
    private void Steady()
    {
        // Something that can't be carried isn't steadied either.
        if (!OnGround || !Tuning.CanBeCarried)
        {
            return;
        }

        bool steadied = false;
        if (Support?.CarrySteadying is float steadying && MathF.Abs(Position.X - Support.Position.X) <= Support.HalfSize.X)
        {
            steadied = true;
            if (!_steadyingAnchored || Support != _steadyingAnchor)
            {
                _steadyingAnchored = true;
                _steadyingAnchor = Support;
                Anchor(_steadying, Position);
            }
            _steadying.SetParamX(Generic6DofJoint3D.Param.LinearMotorTargetVelocity, 0);
            _steadying.SetParamX(Generic6DofJoint3D.Param.LinearMotorForceLimit, steadying);
            _steadying.SetFlagX(Generic6DofJoint3D.Flag.EnableLinearMotor, true);
        }

        // The hand may also turn the entity towards level, if it can turn: towards the nearest quarter turn from the
        // carrier's own angle, the faster the further off it is. Steadying alone would keep a tilted entity from
        // settling, since settling on a corner carries its center sideways.
        if (steadied && Support!.CarryTorque is float torque && torque > 0 && !AxisLockAngularZ)
        {
            const float Quarter = MathF.PI / 2;
            float tilt = Mathf.Wrap(Rotation.Z - Support.Rotation.Z, -Quarter / 2, Quarter / 2);
            _steadying.SetParamZ(Generic6DofJoint3D.Param.AngularMotorTargetVelocity, LevelingRate * tilt);
            _steadying.SetParamZ(Generic6DofJoint3D.Param.AngularMotorForceLimit, torque);
            _steadying.SetFlagZ(Generic6DofJoint3D.Flag.EnableMotor, true);
        }
    }

    private void ReleaseGrip()
    {
        _grip.SetFlagX(Generic6DofJoint3D.Flag.EnableLinearMotor, false);
        _grip.SetFlagZ(Generic6DofJoint3D.Flag.EnableMotor, false);
        _steadying.SetFlagX(Generic6DofJoint3D.Flag.EnableLinearMotor, false);
        _steadying.SetFlagZ(Generic6DofJoint3D.Flag.EnableMotor, false);
    }

    // A joint that constrains nothing: every limit is off, so its only effects are the motors that are switched on.
    private static Generic6DofJoint3D FreeJoint()
    {
        var joint = new Generic6DofJoint3D { ExcludeNodesFromCollision = false };
        foreach (var flag in (ReadOnlySpan<Generic6DofJoint3D.Flag>)
            [Generic6DofJoint3D.Flag.EnableLinearLimit, Generic6DofJoint3D.Flag.EnableAngularLimit])
        {
            joint.SetFlagX(flag, false);
            joint.SetFlagY(flag, false);
            joint.SetFlagZ(flag, false);
        }
        return joint;
    }

    /// <summary>
    /// Drives the entity's grip on what it stands on, at once, for this tick's step: pushes against the ground to bring
    /// the entity's horizontal speed, relative to the ground, towards <paramref name="targetSpeed"/>, with at most
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

        // The grip acts where the entity touches what it stands on (019 §2.2).
        Vector3 point = _groundContact;
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
        _gripPoint = ToLocal(point);
        Anchor(_grip, point);
    }

    // Anchors one of the entity's joints to what it stands on, at a point.
    private void Anchor(Generic6DofJoint3D joint, Vector3 point)
    {
        joint.GlobalTransform = new Transform3D(Basis.Identity, point);
        joint.NodeB = new NodePath();
        joint.NodeA = Support is null ? new NodePath() : joint.GetPathTo(Support);
        joint.NodeB = new NodePath("..");
    }

    /// <summary>Runs the controller of this kind of body, if it has one.</summary>
    protected virtual void Control(float gameDt, float realDt, IEndOfTick endOfTick)
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
        if (state.GetContactCount() >= MaxContacts && !_warnedOfMaxContacts)
        {
            _warnedOfMaxContacts = true;
            GD.PushWarning($"{Name} has {MaxContacts} contacts, as many as the engine reports, so it may have lost some.");
        }
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
        _groundContact = groundContacts > 0 ? groundContact / groundContacts : Position;
        // Tiles don't move.
        GroundVelocity = support is not null && groundContacts > 0 ? groundVelocity / groundContacts : Vector3.Zero;
    }
}
