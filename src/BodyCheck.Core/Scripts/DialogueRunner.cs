using BodyCheck.Core.Entities;
using BodyCheck.Core.Tasks;

namespace BodyCheck.Core.Scripts;

/// <summary>
/// What runs a dialogue: on <c>Default</c>, it presents the title <paramref name="title"/> of the dialogue file
/// <paramref name="file"/>, and finishes when the dialogue ends. Every <c>Default</c> presents it, after any dialogue
/// already waiting.
/// </summary>
public sealed class DialogueRunner(IDialoguePresenter presenter, string file, string title) : IActionTarget
{
    public TickTask Default(ITick tick) => presenter.Present(tick, file, title);
}
