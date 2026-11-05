using BodyCheck.Core.Tasks;

namespace BodyCheck.Core.Entities;

/// <summary>
/// Something a route can run an action on: a body's controller, or what runs a control entity. It has a method for
/// each <see cref="TriggerAction"/>, which a target that responds to the action implements, often as an <c>async</c>
/// method; the others throw. The world checks every route when it is built, so no route asks a target for an action
/// it doesn't respond to.
/// <para>
/// Each method runs on <c>tick</c>, the world's tick, and returns the action's run, which finishes when the action
/// does, so that whatever ran it can await it.
/// </para>
/// </summary>
public interface IActionTarget
{
    TickTask ToggleOnOff(ITick tick) => ThrowActionNotSupported(nameof(ToggleOnOff));
    TickTask TurnOn(ITick tick) => ThrowActionNotSupported(nameof(TurnOn));
    TickTask TurnOff(ITick tick) => ThrowActionNotSupported(nameof(TurnOff));
    TickTask ToggleOpenClose(ITick tick) => ThrowActionNotSupported(nameof(ToggleOpenClose));
    TickTask Open(ITick tick) => ThrowActionNotSupported(nameof(Open));
    TickTask Close(ITick tick) => ThrowActionNotSupported(nameof(Close));
    TickTask Default(ITick tick) => ThrowActionNotSupported(nameof(Default));

    private TickTask ThrowActionNotSupported(string action) =>
        throw new NotSupportedException($"{GetType().Name} doesn't respond to action {action}.");
}
