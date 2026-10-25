namespace BodyCheck.Core.Entities;

/// <summary>
/// Actions that wait for a later point in the tick than the controller that asks for them.
/// <para>
/// A controller acts on bodies with forces and impulses, which the engine applies in its step, so no controller sees
/// what another did in the same tick, whatever order they run in. An action that changes the world at once, such as
/// moving a body, waits until after the entity steps to keep it so: every controller has run by then, and the engine
/// hasn't yet stepped.
/// </para>
/// <para>
/// An action that controllers this tick must not see the effect of, even in the engine's step, such as a change of
/// gravity they have reckoned with, waits until after the engine's step. It runs at the start of the next tick,
/// before any entity steps, whether that tick is played or paused. On a paused tick the engine doesn't step, so an
/// action asked for then runs before the engine steps next.
/// </para>
/// <para>
/// The actions of a tick whose entity steps fail partway never run (022 §2.1).
/// </para>
/// </summary>
public interface IDefer
{
    /// <summary>Runs <paramref name="action"/> once every entity has stepped this tick, before the engine's step.</summary>
    void RunAfterEntitySteps(Action action);

    /// <summary>
    /// Runs <paramref name="action"/> after the engine's step: at the start of the next tick, before any entity steps.
    /// </summary>
    void RunAfterPhysicsStep(Action action);
}
