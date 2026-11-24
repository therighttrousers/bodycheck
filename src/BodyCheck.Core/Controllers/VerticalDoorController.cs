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
/// <param name="startsClosed">Whether the door starts nearer its close stop than its open stop.</param>
/// <param name="travel">How far, at most, the door moves from one stop to the other.</param>
public sealed class VerticalDoorController(Entity entity, bool startsClosed, float travel) : IController<Entity>
{
    private IWorldView _world = null!;

    public void Ready(IWorldView world) => _world = world;

    /// <summary>
    /// Moves the door straight to the stop of its last action, if it has been asked for one since the game started, so
    /// that a door opened before the world was built again is open; one that hasn't stays where the world's data puts
    /// it, even between its stops. It moves until it meets its stop, or whatever is in the way, as an action would, but
    /// at once.
    /// </summary>
    public void RestoreGameState()
    {
        OpenableAction lastAction = _world.GameState.GetEntity<IGameState.IDoorState>(Entity.Id).LastAction;
        if (lastAction != OpenableAction.None)
        {
            float move = lastAction == OpenableAction.Open ? 1 : -1;
            Entity.MoveAndCollide(new Vector3(0, move * travel, 0));
        }
    }

    public Entity Entity { get; } = entity;

    public void Apply(float gameDt, float realDt, IDefer defer)
    {
        // Do nothing
    }

    private readonly ReentrantTask _openCloseReentry = new(Reentry.Queue);

    public TickTask ToggleOpenClose(ITick tick) => _openCloseReentry.Run(() =>
    {
        var lastAction = _world.GameState.GetEntity<IGameState.IDoorState>(Entity.Id).LastAction;
        var closed = lastAction == OpenableAction.None ? startsClosed : lastAction == OpenableAction.Close;
        return closed ? OpenRun(tick) : CloseRun(tick);
    });

    public TickTask Open(ITick tick) => _openCloseReentry.Run(() => OpenRun(tick));
    public TickTask Close(ITick tick) => _openCloseReentry.Run(() => CloseRun(tick));

    private TickTask OpenRun(ITick tick) => OpenOrClose(tick, 1, OpenableAction.Open, EntityCue.DoorOpened);
    private TickTask CloseRun(ITick tick) => OpenOrClose(tick, -1, OpenableAction.Close, EntityCue.DoorClosed);

    private async TickTask OpenOrClose(ITick tick, float move, OpenableAction action, EntityCue cue)
    {
        _world.GameState.GetEntity<IGameState.IDoorState>(Entity.Id).LastAction = action;

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
