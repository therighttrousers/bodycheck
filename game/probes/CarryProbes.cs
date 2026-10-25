using System;
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
/// In the "Carrying" room: a block falls onto the idle player's head and settles. Then the player runs right for a
/// short way and stops ("wobble"); or the block is dropped again, offset to one side of the head ("offset-landing");
/// or the block is put just above the head, tilted, and the player waits for a second, then runs and stops ("tilted").
/// Prints a trace of the block on the player's head, tick by tick.
/// </summary>
public sealed class WobbleProbe : IProbe
{
    public enum Case
    {
        Wobble,
        OffsetLanding,
        Tilted,
    }

    private const int SettleTicks = 120;
    private const int TraceTicks = 120;
    private const float RunDistance = 2.5f;
    private const float Offset = 0.5f;
    private const float Tilt = 0.1f;
    private const int TiltedWaitTicks = 60;

    private readonly WalkerEntity _player;
    private readonly Entity _block;
    private readonly Case _case;
    private int _tick;
    private float _runStart;
    private bool _running;

    public WobbleProbe(WalkerEntity player, Entity block, Case @case)
    {
        _player = player;
        _block = block;
        _case = @case;
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
            _running = _case == Case.Wobble;
            if (_case == Case.OffsetLanding)
            {
                // From 1.2 tiles above the head.
                _block.Position = _player.Position + new Vector3(Offset, _player.HalfSize.Y + 1.2f + _block.HalfSize.Y, 0);
                _block.Rotation = Vector3.Zero;
                _block.LinearVelocity = Vector3.Zero;
                _block.AngularVelocity = Vector3.Zero;
            }
            else if (_case == Case.Tilted)
            {
                // Just clear of the head, at its lowest corner.
                float lift = _block.HalfSize.X * MathF.Sin(Tilt) + _block.HalfSize.Y * (MathF.Cos(Tilt) - 1) + 0.02f;
                _block.Position = _player.Position + new Vector3(0, _player.HalfSize.Y + _block.HalfSize.Y + lift, 0);
                _block.Rotation = new Vector3(0, 0, Tilt);
                _block.LinearVelocity = Vector3.Zero;
                _block.AngularVelocity = Vector3.Zero;
            }
        }
        if (_case == Case.Tilted && t == TiltedWaitTicks)
        {
            _runStart = _player.Position.X;
            _running = true;
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
            + $"landed {(_block.Landed ? 1 : 0)} friction {_block.PhysicsMaterialOverride.Friction,4:F1}");
    }
}

/// <summary>
/// In the "Carrying" room, checks for decision record 019 §3.2, §3.3 and §5.4, one after another. The player runs
/// with the block on its head, stops, and stands still for 10 seconds: how far the two drift. The player is moved 3
/// tiles through entities, as a phase moves it, with the block anchored to it: how the block follows. The block stacked
/// on the floor is put onto a spare walker, which is then freed under it. And how long Godot takes to build a joint
/// again when its node A changes.
/// </summary>
public sealed class CarryChecksProbe : IProbe
{
    private const int SettleTicks = 120;
    private const float RunDistance = 2.5f;
    private const int StillTicks = 600;
    private const int MoveTicks = 60;
    private const int SpareTicks = 120;
    private const int FreedTicks = 60;
    private const int Rebuilds = 1000;

    private readonly WalkerEntity _player;
    private readonly Entity _carried;
    private readonly Entity _stacked;
    private WalkerEntity? _spare;
    private int _tick;
    private int _phaseStart;
    private int _phase;
    private float _runStart;
    private float _stillPlayerX;
    private float _stillBlockOffset;
    private float _fastestStill;

    public CarryChecksProbe(WalkerEntity player, Entity carried, Entity stacked)
    {
        _player = player;
        _carried = carried;
        _stacked = stacked;
    }

    public bool Done { get; private set; }

    public PlayerInput Input { get; private set; }

    public void Tick(float gameDt)
    {
        int t = _tick++ - _phaseStart;
        switch (_phase)
        {
            case 0:
                // Settle, then run.
                if (t == SettleTicks)
                {
                    _runStart = _player.Position.X;
                    Next();
                }
                break;
            case 1:
                Input = new PlayerInput(_player.Position.X - _runStart < RunDistance ? 1 : 0, 0, false);
                if (Input.XAxis == 0 && MathF.Abs(_player.LinearVelocity.X) < 0.001f)
                {
                    _stillPlayerX = _player.Position.X;
                    _stillBlockOffset = _carried.Position.X - _player.Position.X;
                    Next();
                }
                break;
            case 2:
                _fastestStill = MathF.Max(_fastestStill,
                    MathF.Max(MathF.Abs(_player.LinearVelocity.X), MathF.Abs(_carried.LinearVelocity.X)));
                if (t < 10 || t % 60 == 0)
                {
                    GD.Print($"    still, tick {t,3}: player vx {_player.LinearVelocity.X:F4}; block {_carried.Position.X - _player.Position.X - _stillBlockOffset:F4} over the head, "
                        + $"vx {_carried.LinearVelocity.X:F4} rot {_carried.Rotation.Z:F4}");
                }
                if (t == StillTicks)
                {
                    GD.Print($"  standing still for {StillTicks} ticks after a stop: player moved {_player.Position.X - _stillPlayerX:F5} tiles, "
                        + $"block moved {_carried.Position.X - _player.Position.X - _stillBlockOffset:F5} over the head, "
                        + $"fastest {_fastestStill:F5} tiles per second");
                    _player.MoveThroughEntities(_player.Position + new Vector3(3, 0, 0));
                    GD.Print($"  moved the player 3 tiles through entities, block {_carried.Position.X - _player.Position.X:F3} over it");
                    Next();
                }
                break;
            case 3:
                if (t < 6 || t % 10 == 0)
                {
                    GD.Print($"    tick {t,2}: player x {_player.Position.X:F3} vx {_player.LinearVelocity.X:F3}; block dx {_carried.Position.X - _player.Position.X:F3} "
                        + $"vx {_carried.LinearVelocity.X:F3} vy {_carried.LinearVelocity.Y:F3} rot {_carried.Rotation.Z:F3} on {(_carried.Support == _player ? "player" : _carried.OnGround ? "other" : "air")}");
                }
                if (t == MoveTicks)
                {
                    // A walker without a controller, which the room doesn't step, at the far end of the room.
                    _spare = new WalkerEntity(BodyTuning.Robot) { Position = new Vector3(30, 1 + BodyTuning.Robot.HalfSize.Y, 0) };
                    _player.GetParent().AddChild(_spare);
                    _stacked.Position = _spare.Position + new Vector3(0, _spare.HalfSize.Y + _stacked.HalfSize.Y + 0.05f, 0);
                    _stacked.Rotation = Vector3.Zero;
                    _stacked.LinearVelocity = Vector3.Zero;
                    _stacked.AngularVelocity = Vector3.Zero;
                    Next();
                }
                break;
            case 4:
                if (t == SpareTicks)
                {
                    GD.Print($"  block on the spare walker: on {(_stacked.Support == _spare ? "it" : "something else")}, friction {_stacked.PhysicsMaterialOverride.Friction:F1}; freeing the walker");
                    _spare!.QueueFree();
                    Next();
                }
                break;
            case 5:
                if (t < 4 || t % 10 == 0)
                {
                    GD.Print($"    tick {t,2}: block y {_stacked.Position.Y:F3} vy {_stacked.LinearVelocity.Y:F3} on {(_stacked.OnGround ? _stacked.Support is null ? "tiles" : "entity" : "air")}, friction {_stacked.PhysicsMaterialOverride.Friction:F1}");
                }
                if (t == FreedTicks)
                {
                    TimeRebuilds();
                    Done = true;
                }
                break;
        }
    }

    private void Next()
    {
        _phase++;
        _phaseStart = _tick;
    }

    private void TimeRebuilds()
    {
        var joint = new Generic6DofJoint3D { ExcludeNodesFromCollision = false };
        _stacked.AddChild(joint);
        joint.NodeB = new NodePath("..");
        NodePath toPlayer = joint.GetPathTo(_player);
        var watch = System.Diagnostics.Stopwatch.StartNew();
        for (int i = 0; i < Rebuilds; i++)
        {
            joint.NodeA = i % 2 == 0 ? toPlayer : new NodePath();
        }
        watch.Stop();
        GD.Print($"  {Rebuilds} rebuilds of a joint by changing its node A: {watch.Elapsed.TotalMilliseconds * 1000 / Rebuilds:F1} microseconds each");
        joint.QueueFree();
    }
}
