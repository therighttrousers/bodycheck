using System.Collections.Generic;

using BodyCheck.Core;

using Godot;

namespace BodyCheck.Probes;

/// <summary>
/// Runs to speed in the "Seams" room and jumps twice: once holding the run
/// through the landing, once letting go at take-off. Prints the horizontal speed around each landing.
/// </summary>
public sealed class LandingProbe : IProbe
{
    private const int RunTicks = 70;

    private readonly BodyNode _body;
    private readonly List<float> _speeds = [];
    private int _tick;
    private int _phaseStart;
    private bool _second;
    private bool _wasInAir;
    private float _lastAirSpeed;

    public LandingProbe(BodyNode body)
    {
        _body = body;
    }

    public bool Done { get; private set; }

    public PlayerInput Input { get; private set; }

    public void Tick(float dt)
    {
        int t = _tick++ - _phaseStart;
        float vx = _body.LinearVelocity.X;
        if (t < RunTicks)
        {
            Input = new PlayerInput(t < 0 ? 0 : 1, false);
            return;
        }

        float held = _second ? 0 : 1;
        Input = new PlayerInput(held, t == RunTicks);
        if (!_body.Body.OnGround)
        {
            _wasInAir = true;
            _lastAirSpeed = vx;
        }
        else if (_wasInAir)
        {
            _speeds.Add(vx);
            if (_speeds.Count == 5)
            {
                GD.Print($"  landing, {(_second ? "idle since take-off" : "holding the run")}: vx {_lastAirSpeed:F3} in the air, "
                    + $"then {string.Join(", ", _speeds.ConvertAll(s => s.ToString("F3")))}");
                _speeds.Clear();
                _wasInAir = false;
                Done = _second;
                _second = true;

                // Stand still for a second before the next run.
                _phaseStart = _tick + 60;
            }
        }
    }
}

/// <summary>
/// A robot patrols into an idle player. Prints how far the player is moved
/// and how deep the two overlap.
/// </summary>
public sealed class PushProbe : IProbe
{
    private const int Ticks = 600;

    private readonly BodyNode _player;
    private readonly BodyNode _robot;
    private readonly float _playerStartX;
    private int _tick;
    private float _largestOverlap;
    private float _largestMove;
    private int _firstTouch = -1;

    public PushProbe(BodyNode player, BodyNode robot)
    {
        _player = player;
        _robot = robot;
        _playerStartX = player.Position.X;
    }

    public bool Done => _tick > Ticks;

    public void Tick(float dt)
    {
        int t = _tick++;
        float overlap = _robot.Position.X + _robot.Body.HalfSize.X - (_player.Position.X - _player.Body.HalfSize.X);
        _largestOverlap = Mathf.Max(_largestOverlap, overlap);
        _largestMove = Mathf.Max(_largestMove, Mathf.Abs(_player.Position.X - _playerStartX));
        if (_firstTouch < 0 && overlap > -0.001f)
        {
            _firstTouch = t;
            GD.Print($"  robot reaches the player at tick {t}, at vx {_robot.LinearVelocity.X:F3}");
        }
        if (_firstTouch >= 0 && t <= _firstTouch + 3)
        {
            GD.Print($"    tick {t - _firstTouch}: robot vx {_robot.LinearVelocity.X:F3}, player vx {_player.LinearVelocity.X:F3}, "
                + $"overlap {overlap:F4}");
        }
        if (t == Ticks)
        {
            GD.Print($"  after {Ticks} ticks: player moved {_player.Position.X - _playerStartX:F4} tiles (at most {_largestMove:F4}), "
                + $"largest overlap {_largestOverlap:F4}");
        }
    }
}

/// <summary>
/// The player lands on a patrolling robot, sprints along it, runs off its front and lands on tiles, so
/// that its grip changes from the robot to nothing to the tiles. Prints a trace of every tick.
/// </summary>
public sealed class RunOffProbe : IProbe
{
    private const int SettleTicks = 120;
    private const int TraceTicks = 50;

    private readonly BodyNode _player;
    private readonly BodyNode _robot;
    private int _tick;

    public RunOffProbe(BodyNode player, BodyNode robot)
    {
        _player = player;
        _robot = robot;
    }

    public bool Done => _tick > SettleTicks + TraceTicks;

    public PlayerInput Input { get; private set; }

    public void Tick(float dt)
    {
        int t = _tick++ - SettleTicks;
        if (t < 0)
        {
            return;
        }

        Input = new PlayerInput(1, false);
        string ground = !_player.Body.OnGround ? "air" : _player.Body.Support is null ? "tiles" : "robot";
        GD.Print($"  tick {t,2}: player vx {_player.LinearVelocity.X,7:F3} vy {_player.LinearVelocity.Y,7:F3} on {ground,-5} "
            + $"robot vx {_robot.LinearVelocity.X,7:F3}");
    }
}
