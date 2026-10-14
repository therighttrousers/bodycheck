using BodyCheck.Core;

using Godot;

namespace BodyCheck.Probes;

/// <summary>
/// Slides a resting body along a flat floor and reports whether tile seams disturb it. With no friction,
/// drag is the only thing that should slow it, and nothing should move it vertically.
/// </summary>
public sealed class SeamProbe : IProbe
{
    private const int SettleTicks = 30;
    private const int SlideTicks = 180;

    private readonly Entity _body;
    private readonly float _speed;

    private int _tick;
    private float _restY;
    private float _startX;
    private float _expectedSpeed;
    private float _maxRise;
    private float _maxVerticalSpeed;
    private float _maxSpeedLoss;
    private int _ticksOffGround;

    public SeamProbe(Entity body, float speed)
    {
        _body = body;
        _speed = speed;
    }

    /// <summary>Call once per physics tick. Returns true when the probe has finished and printed its result.</summary>
    public bool Done { get; private set; }

    public void Tick(float dt) => Done = Advance(dt);

    private bool Advance(float dt)
    {
        _tick++;
        if (_tick < SettleTicks)
        {
            return false;
        }

        if (_tick == SettleTicks)
        {
            _restY = _body.Position.Y;
            _startX = _body.Position.X;
            _expectedSpeed = _speed;
            _body.ApplyCentralImpulse(new Vector3(_body.Mass * _speed, 0, 0));
            GD.Print($"  at rest: y = {_restY:F4} (box bottom {_restY - _body.HalfSize.Y:F4}), contacts {_body.Contacts}");
            return false;
        }

        // What drag alone leaves of last tick's speed.
        _expectedSpeed *= 1 - _body.Tuning.Drag / _body.Mass * dt;
        Vector3 velocity = _body.LinearVelocity;
        _maxSpeedLoss = Mathf.Max(_maxSpeedLoss, _expectedSpeed - velocity.X);
        _expectedSpeed = velocity.X;
        _maxRise = Mathf.Max(_maxRise, Mathf.Abs(_body.Position.Y - _restY));
        _maxVerticalSpeed = Mathf.Max(_maxVerticalSpeed, Mathf.Abs(velocity.Y));
        if (!_body.OnGround)
        {
            _ticksOffGround++;
        }

        if (_tick < SettleTicks + SlideTicks)
        {
            return false;
        }

        GD.Print($"  slid {_body.Position.X - _startX:F2} tiles in {SlideTicks} ticks, final speed {velocity.X:F3}");
        GD.Print($"  largest speed loss beyond drag in one tick: {_maxSpeedLoss:F5}");
        GD.Print($"  largest vertical displacement: {_maxRise:F5}, largest vertical speed: {_maxVerticalSpeed:F5}");
        GD.Print($"  ticks without a floor contact: {_ticksOffGround}");
        return true;
    }
}
