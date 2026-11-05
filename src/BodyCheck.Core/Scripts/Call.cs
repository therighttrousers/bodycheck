using BodyCheck.Core.Entities;
using BodyCheck.Core.Tasks;

namespace BodyCheck.Core.Scripts;

/// <summary>One step of a route, resolved: an action, and what runs it.</summary>
public readonly record struct Call(IActionTarget Target, TriggerAction Action)
{
    /// <summary>Runs the action on the target.</summary>
    public TickTask Run(ITick tick) => Action switch
    {
        TriggerAction.ToggleOnOff => Target.ToggleOnOff(tick),
        TriggerAction.TurnOn => Target.TurnOn(tick),
        TriggerAction.TurnOff => Target.TurnOff(tick),
        TriggerAction.ToggleOpenClose => Target.ToggleOpenClose(tick),
        TriggerAction.Open => Target.Open(tick),
        TriggerAction.Close => Target.Close(tick),
        TriggerAction.Default => Target.Default(tick),
    };
}
