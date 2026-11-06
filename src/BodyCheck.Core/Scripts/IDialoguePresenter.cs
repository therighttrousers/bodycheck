using BodyCheck.Core.Tasks;

namespace BodyCheck.Core.Scripts;

/// <summary>
/// What presents dialogue, which the game implements, so that Core knows nothing of the dialogue backend or how the
/// game shows it.
/// </summary>
public interface IDialoguePresenter
{
    /// <summary>
    /// Presents the title <paramref name="title"/> of the dialogue file <paramref name="file"/>, after any dialogue
    /// already presented or waiting. It plays no cutscene of its own: what presents it holds one, if it wants one. The
    /// task finishes when the dialogue ends, and what waits for it resumes in the world's next tick.
    /// </summary>
    /// <param name="tick">The tick that what waits for the dialogue resumes on.</param>
    /// <param name="file">The dialogue file, by its name, without a directory or an extension.</param>
    TickTask Present(ITick tick, string file, string title);

    /// <summary>
    /// Whether the dialogue file <paramref name="file"/> exists and holds the title <paramref name="title"/>, so that
    /// a world can check its dialogues when it is built, not when they are presented.
    /// </summary>
    /// <param name="file">The dialogue file, by its name, as <see cref="Present"/> takes it.</param>
    bool Holds(string file, string title);
}
