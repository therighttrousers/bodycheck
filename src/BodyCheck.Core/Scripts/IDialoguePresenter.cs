using BodyCheck.Core.Tasks;

namespace BodyCheck.Core.Scripts;

/// <summary>
/// What presents dialogue, which the game implements, so that Core knows nothing of the dialogue backend or how the
/// game shows it.
/// </summary>
public interface IDialoguePresenter
{
    /// <summary>
    /// Presents the title <paramref name="title"/> of the dialogue file <paramref name="file"/>, garbled by
    /// <paramref name="garble"/>, the fraction of glyphs replaced, from 0 to 1. The game pauses for it at its own point
    /// in the tick. The task finishes when the dialogue ends, and what waits for it resumes in the next played tick.
    /// </summary>
    /// <param name="tick">The tick that what waits for the dialogue resumes on.</param>
    /// <param name="file">The dialogue file, by its name, without a directory or an extension.</param>
    TickTask Present(ITick tick, string file, string title, float garble);
}
