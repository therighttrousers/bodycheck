namespace BodyCheck.Core.Entities;

/// <summary>
/// Actions that wait until every controller has run in a tick, and run before the engine's step. A controller acts on
/// bodies with forces and impulses, which the engine applies in its step, so no controller sees what another did in
/// the same tick, whatever order they run in. An action that changes the world at once, such as moving a body, waits
/// here to keep it so.
/// </summary>
public interface IEndOfTick
{
    /// <summary>Runs <paramref name="action"/> once every controller has run this tick.</summary>
    void Defer(Action action);
}
