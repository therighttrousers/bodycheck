using System.Linq;

using BodyCheck.Core.Controllers;
using BodyCheck.Core.Entities;

using Godot;

namespace BodyCheck.Probes;

/// <summary>
/// In the "Carrying" room: a block falls onto the idle player's head, and the player then runs right. Prints, per
/// tick, the normal force that the engine's contact impulses imply on the block's floor contacts, against its
/// weight, and how fast it slides on what it stands on. A second block rests on the floor, for comparison.
/// </summary>
public sealed class CarryProbe : IProbe
{
    private const int SettleTicks = 120;
    private const int RunTicks = 60;

    private readonly Entity _player;
    private readonly Entity _carried;
    private readonly Entity _resting;
    private readonly Entity _stacked;
    private int _tick;
    private bool _landed;
    private int _landedTick;
    private float _lastVy;

    public CarryProbe(Entity player, Entity carried, Entity resting, Entity stacked)
    {
        _stacked = stacked;
        _player = player;
        _carried = carried;
        _resting = resting;
    }

    public bool Done => _tick > SettleTicks + RunTicks;

    public PlayerInput Input { get; private set; }

    public void Tick(float gameDt)
    {
        int t = _tick++;
        Input = new PlayerInput(t < SettleTicks ? 0 : 1, 0, false);

        (int count, float normalForce) = FloorContacts(_carried, gameDt);
        if (!_landed && count > 0)
        {
            _landed = true;
            _landedTick = t;
            // What the contact must have pushed with in the landing step, from the change in vertical velocity, net
            // of gravity.
            float impact = _carried.Mass * (_carried.LinearVelocity.Y - _lastVy) / gameDt + _carried.Weight;
            GD.Print($"  block lands at tick {t}, on {Describe(_carried.Support)}; weight {_carried.Weight:F3}; "
                + $"vy {_lastVy:F3} before, so the contact pushed {impact:F3} ({impact / _carried.Weight:F3} weights)");
        }
        _lastVy = _carried.LinearVelocity.Y;

        bool landing = _landed && t - _landedTick < 10;
        bool running = t >= SettleTicks && (t - SettleTicks) % 5 == 0;
        if (landing || running || t == SettleTicks - 1)
        {
            float slip = _carried.LinearVelocity.X - _carried.GroundVelocity.X;
            (int headCount, float headForce) = Contacts(_player, gameDt, normal => normal.Y < -0.7f);
            GD.Print($"    tick {t,3}{(t >= SettleTicks ? " running" : "")}: on {Describe(_carried.Support),-6} "
                + $"contacts {count}, normal force {normalForce,8:F3} ({normalForce / _carried.Weight,6:F3} weights), "
                + $"player's head: contacts {headCount}, force {headForce,8:F3}, "
                + $"slip {slip,7:F3}, block x - player x {_carried.Position.X - _player.Position.X,7:F3}, "
                + $"block vy {_carried.LinearVelocity.Y,7:F3}");
        }

        if (t == SettleTicks - 1)
        {
            (int restingCount, float restingForce) = FloorContacts(_resting, gameDt);
            GD.Print($"  block resting on tiles: contacts {restingCount}, normal force {restingForce:F3} "
                + $"({restingForce / _resting.Weight:F3} weights)");
            (int stackedCount, float stackedForce) = FloorContacts(_stacked, gameDt);
            GD.Print($"  block stacked on it: contacts {stackedCount}, normal force {stackedForce:F3} "
                + $"({stackedForce / _stacked.Weight:F3} weights)");
        }
    }

    private static string Describe(Entity? support) => support is null ? "tiles" : support is WalkerEntity ? "player" : "entity";

    /// <summary>
    /// The number of the entity's floor contacts in the engine's last step, and the normal force they imply: the sum
    /// of their impulses along the normal, over the tick's length.
    /// </summary>
    private static (int Count, float NormalForce) FloorContacts(Entity entity, float dt) =>
        Contacts(entity, dt, normal => normal.Y > 0.7f);

    private static (int Count, float NormalForce) Contacts(Entity entity, float dt, System.Func<Vector3, bool> side)
    {
        PhysicsDirectBodyState3D state = PhysicsServer3D.BodyGetDirectState(entity.GetRid());
        int count = 0;
        float impulse = 0;
        for (int i = 0; i < state.GetContactCount(); i++)
        {
            Vector3 normal = state.GetContactLocalNormal(i);
            if (side(normal))
            {
                count++;
                impulse += state.GetContactImpulse(i).Dot(normal);
            }
        }
        return (count, impulse / dt);
    }
}

/// <summary>
/// In the "Floating block" room: a joint motor pushes a weightless block along X, and the block's spin shows where
/// the push acts. At its center it doesn't spin; below it, it spins counterclockwise, positive about Z. Checks
/// whether moving the joint, and then changing its node A, changes where it pushes.
/// </summary>
public sealed class JointPlacementProbe : IProbe
{
    private const int TicksPerCase = 30;
    private const int MeasureTicks = 5;

    private static readonly string[] Cases =
    [
        "joint at center, anchored to the world",
        "joint moved to the base, node A unchanged",
        "then node A set to the player",
        "joint moved to the center, node A set to a new path to the player",
        "joint at the center, node A set to the same path object as before",
        "joint at the center, node A set to its own value",
        "joint at the center, node A cleared and set to the player in one tick",
        "joint moved to the base, node A set back to the world",
    ];

    private NodePath? _playerPath;

    private readonly Entity _block;
    private readonly WalkerEntity _player;
    private readonly Vector3 _start;
    private readonly Generic6DofJoint3D _joint;
    private int _tick;

    public JointPlacementProbe(Entity block, WalkerEntity player)
    {
        _block = block;
        _player = player;
        _start = block.Position;
        block.GravityScale = 0;

        _joint = new Generic6DofJoint3D { NodeB = new NodePath(".."), ExcludeNodesFromCollision = false };
        foreach (var flag in new[] { Generic6DofJoint3D.Flag.EnableLinearLimit, Generic6DofJoint3D.Flag.EnableAngularLimit })
        {
            _joint.SetFlagX(flag, false);
            _joint.SetFlagY(flag, false);
            _joint.SetFlagZ(flag, false);
        }
        _joint.SetParamX(Generic6DofJoint3D.Param.LinearMotorTargetVelocity, 1);
        _joint.SetParamX(Generic6DofJoint3D.Param.LinearMotorForceLimit, 5);
        _joint.SetFlagX(Generic6DofJoint3D.Flag.EnableLinearMotor, true);
        block.AddChild(_joint);
    }

    public bool Done => _tick >= TicksPerCase * Cases.Length;

    public void Tick(float gameDt)
    {
        int t = _tick++;
        int @case = t / TicksPerCase;
        int phase = t % TicksPerCase;

        if (phase == 0)
        {
            _block.Position = _start;
            _block.Rotation = Vector3.Zero;
            _block.LinearVelocity = Vector3.Zero;
            _block.AngularVelocity = Vector3.Zero;
            var center = Vector3.Zero;
            var bottom = new Vector3(0, -_block.HalfSize.Y, 0);
            switch (@case)
            {
                case 1:
                    _joint.Position = bottom;
                    break;
                case 2:
                    _playerPath = _joint.GetPathTo(_player);
                    _joint.NodeA = _playerPath;
                    break;
                case 3:
                    _joint.Position = center;
                    _joint.NodeA = _joint.GetPathTo(_player);
                    break;
                case 4:
                    _joint.NodeA = _playerPath;
                    break;
                case 5:
                    _joint.NodeA = _joint.NodeA;
                    break;
                case 6:
                    _joint.NodeA = new NodePath();
                    _joint.NodeA = _joint.GetPathTo(_player);
                    break;
                case 7:
                    _joint.Position = bottom;
                    _joint.NodeA = new NodePath();
                    break;
            }
            return;
        }

        if (phase == MeasureTicks)
        {
            GD.Print($"  {Cases[@case]}: after {MeasureTicks - 1} ticks, vx {_block.LinearVelocity.X:F4}, "
                + $"spin {_block.AngularVelocity.Z:F4}");
        }
    }
}

/// <summary>
/// In the "Carrying" room: a block falls onto the idle player's head and settles. Then either the player runs right
/// for a short way and stops ("wobble"), or the block is dropped again, offset to one side of the head
/// ("offset-landing"). Prints a trace of the block on the player's head, tick by tick.
/// </summary>
public sealed class WobbleProbe : IProbe
{
    private const int SettleTicks = 120;
    private const int TraceTicks = 120;
    private const float RunDistance = 2.5f;
    private const float Offset = 0.5f;

    private readonly WalkerEntity _player;
    private readonly Entity _block;
    private readonly bool _offset;
    private int _tick;
    private float _runStart;
    private bool _running;

    public WobbleProbe(WalkerEntity player, Entity block, bool offset)
    {
        _player = player;
        _block = block;
        _offset = offset;
    }

    public bool Done => _tick > SettleTicks + TraceTicks;

    public PlayerInput Input { get; private set; }

    public void Tick(float gameDt)
    {
        int t = _tick++ - SettleTicks;
        if (t < 0)
        {
            return;
        }

        if (t == 0)
        {
            _runStart = _player.Position.X;
            _running = !_offset;
            if (_offset)
            {
                // From 1.2 tiles above the head.
                _block.Position = _player.Position + new Vector3(Offset, _player.HalfSize.Y + 1.2f + _block.HalfSize.Y, 0);
                _block.Rotation = Vector3.Zero;
                _block.LinearVelocity = Vector3.Zero;
                _block.AngularVelocity = Vector3.Zero;
            }
        }
        if (_running && _player.Position.X - _runStart >= RunDistance)
        {
            _running = false;
            GD.Print($"  stops driving at tick {t}");
        }
        Input = new PlayerInput(_running ? 1 : 0, 0, false);

        int floorContacts = 0;
        PhysicsDirectBodyState3D state = PhysicsServer3D.BodyGetDirectState(_block.GetRid());
        for (int i = 0; i < state.GetContactCount(); i++)
        {
            if (state.GetContactLocalNormal(i).Y > 0.7f)
            {
                floorContacts++;
            }
        }
        string support = !_block.OnGround ? "air" : _block.Support == _player ? "player" : _block.Support is null ? "tiles" : "entity";
        GD.Print($"  tick {t,3}: player vx {_player.LinearVelocity.X,7:F3}; block dx {_block.Position.X - _player.Position.X,6:F3} "
            + $"vx {_block.LinearVelocity.X,7:F3} vy {_block.LinearVelocity.Y,7:F3} rot {_block.Rotation.Z,6:F3} "
            + $"spin {_block.AngularVelocity.Z,7:F3} on {support,-6} contacts {floorContacts} "
            + $"landed {(_block.Landed ? 1 : 0)} friction {_block.Friction,7:F1}");
    }
}
