namespace BodyCheck.Core.Entities;

/// <summary>
/// The last of a pair of opposite actions that something that opens and closes was asked for, or neither, since the
/// game started.
/// </summary>
public enum OpenableAction
{
    /// <summary>Nothing has asked it to open or close since the game started, so it is where the world's data puts it.</summary>
    None,
    Open,
    Close,
}

/// <summary>
/// What Core sees of the game's state, which the game owns and fills in before it builds a world: only what the game's
/// rules read or change, such as the player's abilities, each door's last action, and whether each trigger and once is
/// on. It outlives the world, which is built from its data and this state. Each piece has one writer in
/// Core, which this view lets set it; the game's own view of the same state lets nothing else set it. Like
/// <see cref="IWorldView"/>, read it when needed, never once and kept.
/// </summary>
public interface IGameState
{
    /// <summary>The player's state; e.g. its abilities.</summary>
    IPlayerState Player { get; }

    /// <summary>The state of the entity whose id is <paramref name="id"/>, as the state of its kind.</summary>
    /// <exception cref="KeyNotFoundException">There is no entity with that id.</exception>
    /// <exception cref="InvalidOperationException">Its state isn't a <typeparamref name="T"/>.</exception>
    T GetEntity<T>(string id) where T : IEntityState;

    /// <summary>What Core sees of an entity's state, which each kind with state of its own extends.</summary>
    interface IEntityState;

    /// <summary>The player's abilities, as the game allows them now.</summary>
    interface IPlayerState : IEntityState
    {
        bool CanPhase { get; }
        bool CanSpawn { get; }
    }

    /// <summary>
    /// The state of a control entity that can be switched on and off: a trigger, which is set off only while it is on,
    /// and a once, which runs its step only while it is on, and turns itself off when its step finishes. What runs it is
    /// its only writer.
    /// </summary>
    interface ISwitchState : IEntityState
    {
        bool IsOn { get; set; }
    }

    /// <summary>A door's state. Its controller is its only writer.</summary>
    interface IDoorState : IEntityState
    {
        /// <summary>
        /// What the door was last asked to do, set when it starts to, so that toggling it reverses what was asked even
        /// if something blocked it; or <see cref="OpenableAction.None"/>, if nothing has asked it since the game started.
        /// It says what was asked, not whether the door is open: something in the way may have stopped it.
        /// </summary>
        OpenableAction LastAction { get; set; }
    }
}
