using BodyCheck.Core.Controllers;
using BodyCheck.Core.Entities;
using BodyCheck.Core.Tasks;

namespace BodyCheck.Core.Scripts;

/// <summary>
/// The world that a script runs in. It adds to <see cref="IWorldView"/> the player's controller, which controllers
/// don't get, so only scripts can drive the player; holding a cutscene; the world's tick; and every entity by id. The
/// simulation implements it. As with <see cref="IWorldView"/>, read its members each time they are needed, so that what
/// a script sees is current.
/// </summary>
public interface IScriptWorld : IWorldView
{
    /// <summary>
    /// The player's controller. Set its <see cref="PlayerController.ScriptedInput"/> to move the player from a script.
    /// The player acts on it only while a cutscene is held.
    /// </summary>
    PlayerController PlayerController { get; }

    /// <summary>
    /// Holds a cutscene until the returned object is disposed. The game plays a cutscene while any hold is held.
    /// Controllers first see the cutscene on the next tick, since the game's mode changes after the world's tick. Hold
    /// it in a <c>using</c> block, so the cutscene ends when the caller ends, even if it fails.
    /// </summary>
    /// <param name="request">
    /// What the cutscene asks for, joined with the other holds' requests by <see cref="CutsceneRequest.Join"/>. The
    /// default asks for no extra effects.
    /// </param>
    IDisposable HoldCutscene(CutsceneRequest? request = null);

    /// <summary>The world's tick, which a script waits on to pass time.</summary>
    ITick Tick { get; }

    /// <summary>
    /// Every entity in the world, by id. An entity that has been removed stays until the end of the tick in which it
    /// was removed. Code that knows an entity only by its id, such as dialogue, uses this to find it.
    /// </summary>
    IReadOnlyDictionary<string, Entity> Entities { get; }
}
