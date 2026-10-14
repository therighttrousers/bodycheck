using BodyCheck.Core;

using Godot;

namespace BodyCheck.Probes;

/// <summary>
/// In the "Corner" room: runs right into a two-tile wall until stopped, then does a full jump straight up,
/// and reports whether the top corner of the wall disturbs the body on the way down.
/// </summary>
public sealed class CornerProbe : IProbe
{
    private const int JumpTick = 120;
    private const int EndTick = 240;

    // The x of the wall's left face.
    private const float WallFace = 9;

    private readonly Entity _body;
    private readonly bool _holdRight;
    private int _tick;

    private float _restX;
    private float _apex;
    private float _minX = float.MaxValue;
    private float _maxLeftSpeed;

    /// <param name="holdRight">Whether to keep pushing into the wall during the jump.</param>
    public CornerProbe(Entity body, bool holdRight)
    {
        _body = body;
        _holdRight = holdRight;
    }

    public bool Done => _tick > EndTick;

    public PlayerInput Input { get; private set; }

    public void Tick(float dt) => Input = NextInput();

    /// <summary>Called once per physics tick, before the controller. Returns the input for this tick.</summary>
    private PlayerInput NextInput()
    {
        int t = _tick++;
        Vector3 p = _body.Position;
        Vector3 v = _body.LinearVelocity;

        if (t < JumpTick)
        {
            return new PlayerInput(t < 30 ? 0 : 1, false);
        }

        if (t == JumpTick)
        {
            _restX = p.X;
            GD.Print($"  stopped at the wall: right edge {p.X + _body.HalfSize.X - WallFace:F4} past its face, contacts {_body.Contacts}");
        }

        _apex = Mathf.Max(_apex, p.Y);
        _minX = Mathf.Min(_minX, p.X);
        if (-v.X > _maxLeftSpeed)
        {
            _maxLeftSpeed = -v.X;
            GD.Print($"  tick {t - JumpTick}: pushed left, vx {v.X:F3}, vy {v.Y:F3}, "
                + $"bottom {p.Y - _body.HalfSize.Y:F3}, right edge {p.X + _body.HalfSize.X - WallFace:F4} past the face");
        }

        if (t == EndTick)
        {
            GD.Print($"  apex {_apex - _body.HalfSize.Y - 1:F3} tiles above the floor; moved left by at most {_restX - _minX:F4}; "
                + $"ends at x {p.X:F4}, bottom {p.Y - _body.HalfSize.Y:F3}");
        }
        return new PlayerInput(_holdRight ? 1 : 0, t < JumpTick + 40);
    }
}
