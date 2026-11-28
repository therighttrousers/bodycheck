using BodyCheck.Core.Entities;
using BodyCheck.Core.Tasks;

namespace BodyCheck.Core.Scripts;

/// <summary>
/// Shows dialogue on screen. The game implements it, so that Core knows nothing of the dialogue backend or of how the
/// game shows dialogue. A <see cref="DialogueRunner"/> presents through it, and the world calls <see cref="Holds"/> on
/// it when the world is built, to check every dialogue in the world.
/// </summary>
public interface IDialoguePresenter
{
    /// <summary>
    /// Shows a dialogue from its title. Returns the task that finishes when the dialogue ends. Dialogue is presented
    /// after any dialogue already being presented or waiting, in the order of the calls. The presenter holds no
    /// cutscene of its own: a caller that wants one holds it, as <see cref="DialogueRunner"/> does. Code awaiting the
    /// task resumes in the world's next tick after the dialogue ends.
    /// </summary>
    /// <remarks>
    /// Check the file and title with <see cref="Holds"/> before calling this.
    /// </remarks>
    /// <param name="tick">The tick that the dialogue waits on, and that its actions on the entities run on.</param>
    /// <param name="file">The dialogue file's name, with no directory and no extension, as <see cref="Holds"/> receives
    /// it.</param>
    /// <param name="title">The title where the dialogue starts.</param>
    /// <param name="entities">
    /// The entities that the dialogue is about. The dialogue file chooses who speaks each line, and the dialogue can
    /// ask these entities for actions through their controllers.
    /// </param>
    TickTask Present(ITick tick, string file, string title, IReadOnlyList<Entity> entities);

    /// <summary>
    /// Whether the dialogue file contains the title. The world calls this when it is built, so a wrong file or title
    /// fails then, not when the dialogue is presented.
    /// </summary>
    bool Holds(string file, string title);
}
