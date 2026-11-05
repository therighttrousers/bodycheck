namespace BodyCheck.Core;

/// <summary>
/// The world's tick, as an async action sees it: what to await to let ticks pass. Whatever runs an action gives it the
/// world's, so that what it waits on resumes in that world's tick.
/// <para>
/// What awaits one resumes where the world runs its <see cref="ActionTaskQueue"/>: after every entity has stepped and
/// the actions they deferred have run, before the engine's step. Every controller has run by then, so an action can
/// change a body at once, as a controller's deferred action would.
/// </para>
/// </summary>
public interface ITick
{
    /// <summary>A task that finishes in the next played tick.</summary>
    ActionTask NextTick();

    /// <summary>
    /// A task that finishes once <paramref name="gameSeconds"/> of game time have passed, counted in played ticks: in the
    /// tick whose end comes nearest, so that rounding never adds a tick, but never in the one it is made in, so that
    /// waiting for no time finishes in the next and a loop that waits can't spin within a tick.
    /// </summary>
    ActionTask Wait(float gameSeconds);
}
