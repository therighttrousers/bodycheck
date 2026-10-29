namespace BodyCheck.Core.Entities;

/// <summary>
/// What decides an entity's actions: the player's input, or an NPC's behaviour. It acts on the entity only
/// with forces, impulses and the motors that the entity's kind of body has, and with actions it defers to the end of
/// the tick.
/// <para>
/// A controller is built with the entity it controls, as the kind of body it needs, and controls only that one.
/// </para>
/// </summary>
public interface ICharacterController
{
    /// <summary>The entity the controller controls.</summary>
    public Entity Entity { get; }

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
