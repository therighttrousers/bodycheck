using BodyCheck.Core.Entities;
using BodyCheck.Core.Tasks;

namespace BodyCheck.Core.Scripts;

/// <summary>
/// One step of a route, such as a trigger's or a sequence's: an action to run on a target. The target is the resolved
/// runner or controller, not an id, so running the step is a direct call.
/// </summary>
/// <remarks>
/// Choose an action that the target responds to. Otherwise <see cref="Run"/> throws a
/// <see cref="NotSupportedException"/>.
/// </remarks>
public readonly record struct Call(IActionTarget Target, TriggerAction Action)
{
    /// <summary>Starts the action on the target. Returns the task that finishes when the action does.</summary>
    /// <param name="tick">The tick that the action waits on.</param>
    public TickTask Run(ITick tick) => Action switch
    {
        TriggerAction.ToggleOnOff => Target.ToggleOnOff(tick),
        TriggerAction.TurnOn => Target.TurnOn(tick),
        TriggerAction.TurnOff => Target.TurnOff(tick),
        TriggerAction.ToggleOpenClose => Target.ToggleOpenClose(tick),
        TriggerAction.Open => Target.Open(tick),
        TriggerAction.Close => Target.Close(tick),
        TriggerAction.Run => Target.Run(tick),
        TriggerAction.Present => Target.Present(tick),
    };
}
