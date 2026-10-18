using BodyCheck.Core.Controllers;
using BodyCheck.Core.Entities;

using Godot;

namespace BodyCheck.Probes;

/// <summary>
/// Drives the player in the "Phase" room: onto the bump beside the left robot's pen, then, when the robot
/// comes near, a phase aimed straight up through it, drifting left to the ledge that only a phase reaches.
/// Prints where the player and the robot go.
/// </summary>
public sealed class PhaseProbe : IProbe
{
    private readonly Entity _player;
    private readonly Entity _robot;
    private int _tick;
    private int _phaseTick = -1;

    public PhaseProbe(Entity player, Entity robot)
    {
        _player = player;
        _robot = robot;
    }

    public bool Done => _tick > 600 || (_phaseTick >= 0 && _tick > _phaseTick + 120);

    public PlayerInput Input { get; private set; }

    public void Tick(float dt) => Input = NextInput();

    private PlayerInput NextInput()
    {
        int t = _tick++;
        Vector3 p = _player.Position;

        if (_phaseTick >= 0)
        {
            if ((t - _phaseTick) % 10 == 0 || t == _phaseTick + 1)
            {
                GD.Print($"  {t - _phaseTick,3} ticks after: player {p.X:F2}, {p.Y - _player.HalfSize.Y:F2} (feet), " +
                    $"velocity {_player.LinearVelocity.X:F2}, {_player.LinearVelocity.Y:F2}, on ground {_player.OnGround}; " +
                    $"robot {_robot.Position.X:F2}, {_robot.Position.Y:F2}, velocity {_robot.LinearVelocity.X:F2}, {_robot.LinearVelocity.Y:F2}");
            }
            // Released aiming up, then drifting left.
            return t == _phaseTick + 1 ? new PlayerInput(0, 1, false) : new PlayerInput(-1, 0, false);
        }

        if (t < 30)
        {
            return default;
        }

        // Onto the bump: walk left to it, then hop.
        if (!(_player.OnGround && p.Y > 4))
        {
            return new PlayerInput(-1, 0, JumpHeld: p.X < 10.7f && _player.OnGround);
        }

        // Wait on the bump for the robot.
        float gap = _player.Position.X - _player.HalfSize.X - (_robot.Position.X + _robot.HalfSize.X);
        if (gap < 0.2f)
        {
            _phaseTick = t;
            GD.Print($"Phase at tick {t}: player {p.X:F2}, {p.Y:F2}; robot {_robot.Position.X:F2}, {_robot.Position.Y:F2}");
            return new PlayerInput(0, 1, false, PhaseHeld: true);
        }
        return default;
    }
}
