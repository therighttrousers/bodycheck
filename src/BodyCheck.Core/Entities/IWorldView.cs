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

    /// <summary>The player's input this tick: none in a cutscene.</summary>
    PlayerInput PlayerInput { get; }
}
