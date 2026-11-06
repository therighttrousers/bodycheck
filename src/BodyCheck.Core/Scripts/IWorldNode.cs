using BodyCheck.Core.Entities;

namespace BodyCheck.Core.Scripts;

/// <summary>
/// What the runners can see of the world they run in, which the simulation implements, so that they reach its entities
/// without knowing how it is built.
/// </summary>
public interface IWorldNode
{
    PlayerEntity Player { get; }

    /// <summary>The block the player spawns.</summary>
    Entity PlayerBlock { get; }

    IReadOnlyList<Entity> Entities { get; }
}
