using BodyCheck.Core.Controllers;
using BodyCheck.Core.Entities;
using BodyCheck.Core.Tasks;

namespace BodyCheck.Core.Scripts;

/// <summary>
/// What a script can see and do in the world it runs in: what controllers see, and also the player's controller, to
/// drive the player with input in a cutscene, holding a cutscene, and the world's tick. The simulation implements it.
/// Like <see cref="IWorldView"/>, read it when needed, never once and kept, so that what a script sees is current.
/// </summary>
public interface IScriptWorld : IWorldView
{
    /// <summary>
    /// The player's controller, whose <see cref="PlayerController.ScriptedInput"/> the player acts on in a cutscene.
    /// </summary>
    PlayerController PlayerController { get; }

    /// <summary>
    /// Asks for a cutscene, until the handle returned is disposed: the game plays one while any is held. Hold it with
    /// <c>using</c>, so that the cutscene ends when the script does, even if it fails. The game's mode changes after
    /// the world's tick, so controllers see the cutscene from the next tick.
    /// </summary>
    /// <param name="request">
    /// How the cutscene is to be presented, which the world joins with every other hold's: by default, with nothing more.
    /// </param>
    IDisposable HoldCutscene(CutsceneRequest? request = null);

    /// <summary>The world's tick, which what the script awaits resumes on.</summary>
    ITick Tick { get; }
}
