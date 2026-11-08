using BodyCheck.Core.Entities;
using BodyCheck.Core.Tasks;

using Godot;

namespace BodyCheck.Core.Controllers;

/// <summary>
/// What moves a door that slides up to open and down to close, between its stops. It does nothing between actions:
/// the door is frozen unless an action is moving it. An action unfreezes it, thrusts it until it stops, against its stop
/// or whatever it meets, and freezes it again, and the door then counts as opened or closed. Its actions share one
/// queue, so that one asked while another runs follows it.
/// </summary>
public sealed class VerticalDoorController : IController<Entity>
{
    private readonly Entity _entity;
    private bool _closed;

    public VerticalDoorController(Entity entity, bool closed)
    {
        _entity = entity;
        _closed = closed;
    }

    public Entity Entity => _entity;

    public void Apply(float gameDt, float realDt, IDefer defer)
    {
        // Do nothing
    }

    private readonly ReentrantTask _openCloseReentry = new(Reentry.Queue);

    public TickTask ToggleOpenClose(ITick tick) => _openCloseReentry.Run(() => _closed ? OpenRun(tick) : CloseRun(tick));
    public TickTask Open(ITick tick) => _openCloseReentry.Run(() => OpenRun(tick));
    public TickTask Close(ITick tick) => _openCloseReentry.Run(() => CloseRun(tick));

    private TickTask OpenRun(ITick tick) => OpenOrClose(tick, 1, false, EntityCue.DoorOpened);

    private TickTask CloseRun(ITick tick) => OpenOrClose(tick, -1, true, EntityCue.DoorClosed);

    private async TickTask OpenOrClose(ITick tick, float move, bool closing, EntityCue cue)
    {
        if (_closed == closing) return;
        tick = tick.Scoped(_entity.IsLive);

        // Wait until after all entity steps in this tick.
        await tick.NextTick();
        float totalTime = tick.Dt;

        _entity.Freeze = false;

        bool hadNonzeroVelocity = false;
        while (totalTime < Features.Current.DoorMaxOpenOrCloseTime)
        {
            _entity.ApplyForce(-move * Features.Current.DoorThrustFrac * _entity.Mass * _entity.GetGravity());

            await tick.NextTick();
            totalTime += tick.Dt;

            float speed = _entity.LinearVelocity.Length();
            if (speed >= 0.001f)
            {
                hadNonzeroVelocity = true;
            }
            if (speed < 0.001f)
            {
                break;
            }
        }

        _entity.Freeze = true;
        _closed = closing;

        if (hadNonzeroVelocity)
        {
            _entity.RaiseCue(cue);
        }
    }
}
