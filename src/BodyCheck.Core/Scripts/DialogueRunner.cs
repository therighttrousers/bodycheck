using BodyCheck.Core.Entities;
using BodyCheck.Core.Tasks;

namespace BodyCheck.Core.Scripts;

/// <summary>
/// What runs a dialogue: on <c>Default</c>, it presents the title <paramref name="title"/> of the dialogue file
/// <paramref name="file"/>, holding a cutscene in <paramref name="world"/> while it does, as a script would, and
/// finishes when the dialogue ends. Every <c>Default</c> presents it, after any dialogue already waiting.
/// </summary>
public sealed class DialogueRunner(IDialoguePresenter presenter, IScriptWorld world, string file, string title)
    : IActionTarget
{
    public async TickTask Default(ITick tick)
    {
        using IDisposable cutscene = world.HoldCutscene();
        await presenter.Present(tick, file, title);
    }
}
