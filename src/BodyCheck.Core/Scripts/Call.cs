using BodyCheck.Core.Entities;
using BodyCheck.Core.Tasks;

namespace BodyCheck.Core.Scripts;

/// <summary>One step of a route, resolved: an action, and what runs it.</summary>
public readonly record struct Call(IActionTarget Target, TriggerAction Action)
{
    /// <summary>Runs the action on the target.</summary>
    public TickTask Run(ITick tick) => Target.Run(Action, tick);
}
