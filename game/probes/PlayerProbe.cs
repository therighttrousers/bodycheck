using BodyCheck.Core;

using Godot;

namespace BodyCheck.Probes;

/// <summary>
/// Drives the player controller with scripted input in the "Seams" room and prints what the body does:
/// jumps beside a wall with and without pushing into it, running to top speed, a jump at top speed, and
/// a reversal.
/// </summary>
public sealed class PlayerProbe : IProbe
{
    private static readonly PlayerInput Idle = new(0, false);
    private static readonly PlayerInput Left = new(-1, false);
    private static readonly PlayerInput Right = new(1, false);

    private readonly BodyNode _body;
    private int _tick;

    private float _restY;
    private float _apex;
    private int _airTicks;

    public PlayerProbe(BodyNode body)
    {
        _body = body;
    }

    public bool Done => _tick > 520;

    public PlayerInput Input { get; private set; }

    public void Tick(float dt) => Input = NextInput();

    /// <summary>Called once per physics tick, before the controller. Returns the input for this tick.</summary>
    private PlayerInput NextInput()
    {
        int t = _tick++;
        Vector3 v = _body.LinearVelocity;
        Vector3 p = _body.Position;

        // Walk into the left wall, then jump while still pushing into it.
        if (t < 60)
        {
            return t < 30 ? Idle : Left;
        }
        if (t < 180)
        {
            TrackJump(t, 60, "jump pushing into a wall");
            return new PlayerInput(-1, t == 60);
        }

        // The same jump beside the wall, without pushing.
        if (t < 300)
        {
            TrackJump(t, 180, "jump beside a wall, no push");
            return new PlayerInput(0, t == 180);
        }

        // Run right to top speed, then tap jump while running.
        if (t < 390)
        {
            if (t == 389)
            {
                GD.Print($"  running: speed after 90 ticks {v.X:F3}");
            }
            return Right;
        }
        if (t < 396)
        {
            GD.Print($"  jump at top speed, tick {t - 390}: vx {v.X:F3}, vy {v.Y:F3}, on ground {_body.Body.OnGround}");
            return new PlayerInput(1, t == 390);
        }
        if (t < 450)
        {
            return Idle;
        }

        // Run right again, then reverse.
        if (t < 510)
        {
            return Right;
        }
        if (t < 516)
        {
            GD.Print($"  reversing, tick {t - 510}: vx {v.X:F3}");
            return Left;
        }
        return Idle;
    }

    private void TrackJump(int t, int start, string name)
    {
        if (t == start)
        {
            _restY = _body.Position.Y;
            _apex = _restY;
            _airTicks = 0;
            return;
        }

        _apex = Mathf.Max(_apex, _body.Position.Y);
        if (!_body.Body.OnGround)
        {
            _airTicks++;
        }
        if (t == start + 119)
        {
            GD.Print($"  {name}: apex {_apex - _restY:F3} tiles, {_airTicks} ticks in the air, "
                + $"ends {_body.Position.Y - _restY:F3} above the floor");
        }
    }
}
