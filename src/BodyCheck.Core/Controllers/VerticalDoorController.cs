using BodyCheck.Core.Entities;
using BodyCheck.Core.Tasks;

namespace BodyCheck.Core.Controllers;

/// <summary>
/// What moves a door that slides up to open and down to close, between its stops. It does nothing between actions:
/// the door is frozen unless an action is moving it. An action unfreezes it, thrusts it until it stops, against its stop
/// or whatever it meets, and freezes it again, and the door then counts as opened or closed. Its actions share one
/// queue, so that one asked while another runs follows it.
/// </summary>
public sealed class VerticalDoorController : IController<Entity>
{
    private IWorldView _world = null!;

    public VerticalDoorController(Entity entity)
    {
        Entity = entity;
    }

    public void Ready(IWorldView world) => _world = world;

    public Entity Entity { get; }

    public void Apply(float gameDt, float realDt, IDefer defer)
    {
        // Do nothing
    }

    private readonly ReentrantTask _openCloseReentry = new(Reentry.Queue);

    public TickTask ToggleOpenClose(ITick tick) => _openCloseReentry.Run(() =>
        _world.WorldState.GetEntity<IWorldState.IDoorState>(Entity.Id).LastAction == OpenableAction.Close ? OpenRun(tick) : CloseRun(tick));
    public TickTask Open(ITick tick) => _openCloseReentry.Run(() => OpenRun(tick));
    public TickTask Close(ITick tick) => _openCloseReentry.Run(() => CloseRun(tick));

    private TickTask OpenRun(ITick tick) => OpenOrClose(tick, 1, OpenableAction.Open, EntityCue.DoorOpened);

    private TickTask CloseRun(ITick tick) => OpenOrClose(tick, -1, OpenableAction.Close, EntityCue.DoorClosed);

    private async TickTask OpenOrClose(ITick tick, float move, OpenableAction action, EntityCue cue)
    {
        _world.WorldState.GetEntity<IWorldState.IDoorState>(Entity.Id).LastAction = action;

        tick = tick.Scoped(Entity.IsLive);

        // Wait until after all entity steps in this tick.
        await tick.NextTick();
        float totalTime = tick.Dt;

        Entity.Freeze = false;

        bool hadNonzeroVelocity = false;
        while (totalTime < Features.Current.DoorMaxOpenOrCloseTime)
        {
            Entity.ApplyForce(-move * Features.Current.DoorThrustFrac * Entity.Mass * Entity.GetGravity());

            await tick.NextTick();
            totalTime += tick.Dt;

            float speed = Entity.LinearVelocity.Length();
            if (speed >= 0.001f)
            {
                hadNonzeroVelocity = true;
            }
            if (speed < 0.001f)
            {
                break;
            }
        }

        Entity.Freeze = true;

        if (hadNonzeroVelocity)
        {
            Entity.RaiseCue(cue);
        }
    }
}
