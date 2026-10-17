using BodyCheck.Core.Controllers;
using BodyCheck.Core.Entities;

using Godot;

namespace BodyCheck.Probes;

/// <summary>
/// Measures the riding criteria of decision record 000 §2.2 and the platform numbers of §2.4: the player
/// is dropped onto a carrier (a patrolling robot, or a hovering platform), stands idle on it for 30
/// seconds, then taps jump.
/// </summary>
public sealed class RidingProbe : IProbe
{
    private const int SettleTicks = 600;
    private const int RideTicks = 1800;
    private const int JumpTicks = 120;

    private readonly Entity _rider;
    private readonly Entity _carrier;
    private readonly float _carrierStartY;
    private int _tick;

    private float _lowestCarrierY = float.MaxValue;
    private float _highestCarrierYAfterDip = float.MinValue;
    private float _offsetAtStart;
    private float _carrierXAtStart;
    private float _largestOverlap;
    private float _largestOffsetChange;
    private int _ticksOffCarrier;
    private float _jumpStartY;
    private float _apex;

    public RidingProbe(Entity rider, Entity carrier)
    {
        _rider = rider;
        _carrier = carrier;
        _carrierStartY = carrier.Position.Y;
    }

    public bool Done => _tick > SettleTicks + RideTicks + JumpTicks;

    public PlayerInput Input { get; private set; }

    public void Tick(float dt) => Input = NextInput();

    /// <summary>Called once per physics tick, before the controllers. Returns the input for this tick.</summary>
    private PlayerInput NextInput()
    {
        int t = _tick++;
        float offset = _rider.Position.X - _carrier.Position.X;
        float overlap = _carrier.Position.Y + _carrier.HalfSize.Y - (_rider.Position.Y - _rider.HalfSize.Y);

        if (t < SettleTicks)
        {
            // Landing and settling: how far the carrier dips, and how far it comes back up past where it settles.
            if (_carrier.Position.Y < _lowestCarrierY)
            {
                _lowestCarrierY = _carrier.Position.Y;
                _highestCarrierYAfterDip = float.MinValue;
            }
            _highestCarrierYAfterDip = Mathf.Max(_highestCarrierYAfterDip, _carrier.Position.Y);
            return default;
        }

        if (t == SettleTicks)
        {
            float settled = _carrier.Position.Y;
            GD.Print($"  carrier under the rider: settles {_carrierStartY - settled:F3} tiles low, "
                + $"dipped {_carrierStartY - _lowestCarrierY:F3} on landing, "
                + $"came back {_highestCarrierYAfterDip - settled:F3} above where it settles");
            _offsetAtStart = offset;
            _carrierXAtStart = _carrier.Position.X;
        }

        if (t <= SettleTicks + RideTicks)
        {
            _largestOverlap = Mathf.Max(_largestOverlap, overlap);
            _largestOffsetChange = Mathf.Max(_largestOffsetChange, Mathf.Abs(offset - _offsetAtStart));
            if (_rider.Support != _carrier)
            {
                _ticksOffCarrier++;
            }
        }

        if (t == SettleTicks + RideTicks)
        {
            GD.Print($"  idle for {RideTicks} ticks: rider moved {offset - _offsetAtStart:F4} tiles relative to the carrier "
                + $"(at most {_largestOffsetChange:F4}), carrier now {_carrier.Position.X - _carrierXAtStart:F1} tiles from where it was");
            GD.Print($"  largest overlap of rider and carrier: {_largestOverlap:F4} tiles; "
                + $"ticks the rider wasn't supported by the carrier: {_ticksOffCarrier}");
            _jumpStartY = _rider.Position.Y;
            _apex = _jumpStartY;
            GD.Print($"  before the jump: carrier vx {_carrier.LinearVelocity.X:F3}, rider vx {_rider.LinearVelocity.X:F3}");
            return new PlayerInput(0, 0, true);
        }

        if (t == SettleTicks + RideTicks + 2)
        {
            GD.Print($"  two ticks after the jump: carrier vx {_carrier.LinearVelocity.X:F3}, rider vx {_rider.LinearVelocity.X:F3}");
        }
        _apex = Mathf.Max(_apex, _rider.Position.Y);
        if (t == SettleTicks + RideTicks + JumpTicks)
        {
            GD.Print($"  tap jump from the carrier: apex {_apex - _jumpStartY:F3} tiles");
        }
        return default;
    }
}
