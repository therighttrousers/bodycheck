namespace BodyCheck.Core.Entities;

/// <summary>The last of a pair of opposite actions that something that opens and closes was asked for.</summary>
public enum OpenableAction
{
    Open,
    Close,
}

/// <summary>
/// What Core sees of the game's state, which the game owns and fills in before it builds a world: only what the game's
/// rules read or change, such as the player's abilities and each door's last action. Each piece has one writer in
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

    /// <summary>A door's state. Its controller is its only writer.</summary>
    interface IDoorState : IEntityState
    {
        /// <summary>
        /// What the door was last asked to do, set when it starts to, so that toggling it reverses what was asked even
        /// if something blocked it.
        /// </summary>
        OpenableAction LastAction { get; set; }
    }
}
