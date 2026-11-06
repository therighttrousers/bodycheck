namespace BodyCheck.Core.Entities;

/// <summary>
/// What decides an entity's actions: the player's input, or an NPC's behaviour. It acts on the entity only
/// with forces, impulses and the motors that the entity's kind of body has, and with actions it defers to the end of
/// the tick.
/// <para>
/// A controller is built with the entity it controls, as the kind of body it needs, and controls only that one. It
/// gets the world it runs in through <see cref="Ready"/>, once the world is built.
/// </para>
/// </summary>
public interface IController<out TEntity> : IActionTarget where TEntity : Entity
{
    /// <summary>The entity the controller controls.</summary>
    public TEntity Entity { get; }

    /// <summary>
    /// Called once, when the world the controller runs in has been built, or at once for an entity added to a built
    /// world, before any <see cref="Apply"/>, <see cref="ApplyPaused"/> or action. A controller that sees the world
    /// keeps <paramref name="world"/> here, and reads it each time it needs it, so that what it sees is current.
    /// </summary>
    public void Ready(IWorldView world)
    {
    }

    /// <summary>
    /// Called on the tick, before the controller steps, when the game's mode differs from the one the world last stepped
    /// its controllers in: where a controller changes state that would mean something else in the new mode, such as
    /// input it remembers. A trip to the menu or the map and back is no change, since no controller runs meanwhile. It
    /// is called for every change, the slowdown and the pause for a phase included, so a controller tests for the
    /// modes it cares about.
    /// </summary>
    /// <param name="from">The mode the world last stepped its controllers in.</param>
    /// <param name="to">The mode now.</param>
    public void GameModeChanged(GameMode from, GameMode to)
    {
    }

    /// <summary>Called once per physics tick, before the engine's step.</summary>
    /// <param name="gameDt">The tick's length in game time, which the engine's time scale shortens.</param>
    /// <param name="realDt">The tick's length in real time, which the time scale doesn't change.</param>
    /// <param name="defer">An object that allows running actions later in the tick.</param>
    public void Apply(float gameDt, float realDt, IDefer defer);

    /// <summary>
    /// Whether the controller asks for the game to be paused, so that it can decide something with all else
    /// standing still, such as which way to phase. Whatever pauses the game for it calls <see cref="ApplyPaused"/>
    /// instead of <see cref="Apply"/> until the controller stops asking.
    /// </summary>
    public bool RequestsPause => false;

    /// <summary>
    /// Called once per physics tick while the game is paused because a controller asked. Nothing moves then:
    /// the engine doesn't step, so forces and impulses wait until play resumes. No game time passes.
    /// </summary>
    /// <param name="realDt">The tick's length in real time.</param>
    /// <param name="defer">An object that allows running actions later in the tick.</param>
    public void ApplyPaused(float realDt, IDefer defer)
    {
    }
}
