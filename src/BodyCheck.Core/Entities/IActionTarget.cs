using BodyCheck.Core.Tasks;

namespace BodyCheck.Core.Entities;

/// <summary>
/// Something a route can run an action on: a body's controller, or what runs a control entity. Whatever ran the
/// action can await it, until it finishes.
/// </summary>
public interface IActionTarget
{
    /// <summary>
    /// Runs <paramref name="action"/>, which must be one this responds to: the world checks every route when it is
    /// built.
    /// </summary>
    /// <param name="tick">The world's tick, for the action's tasks to run on.</param>
    /// <returns>The action's run, which finishes when the action does.</returns>
    TickTask Run(TriggerAction action, ITick tick);
}
