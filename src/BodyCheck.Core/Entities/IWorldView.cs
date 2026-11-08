namespace BodyCheck.Core.Entities;

/// <summary>
/// What controllers can see of the world they run in, beyond what their entity can tell them, which the simulation
/// implements and gives each controller in <see cref="IController{TEntity}.Ready"/>, once it is built. Each reads it
/// when it needs it, never once and kept, so that what it sees is current, even in an action that waits across ticks.
/// </summary>
public interface IWorldView
{
    PlayerEntity Player { get; }

    /// <summary>The block the player spawns.</summary>
    Entity PlayerBlock { get; }

    /// <summary>What the game is doing.</summary>
    GameMode GameMode { get; }

    /// <summary>What the player is pressing this tick, whatever the game's mode.</summary>
    PlayerInput PlayerInput { get; }

    /// <summary>
    /// Removes <paramref name="entity"/> from the world. It can be asked at any point in a tick, by a controller or a
    /// script, even about the entity whose controller or script asks, and more than once. From then on the entity isn't
    /// <see cref="Entity.IsLive"/>, so scopes about it end, and it isn't stepped. At the end of the tick, the world
    /// unwinds what ran in those scopes, while the entity is still whole, and then takes it out and frees it.
    /// </summary>
    /// <exception cref="ArgumentException">
    /// When the entity isn't in the world, or is the player or the player's block, which the world always has.
    /// </exception>
    void Remove(Entity entity);
}
