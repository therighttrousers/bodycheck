using BodyCheck.Core.Entities;
using BodyCheck.Core.Tasks;

namespace BodyCheck.Core.Scripts;

/// <summary>
/// What runs a dialogue: on <c>Present</c>, it presents the title <paramref name="title"/> of the dialogue file
/// <paramref name="file"/>, about <paramref name="entities"/>, holding a cutscene in <paramref name="world"/> while it
/// does, as a script would, and finishes when the dialogue ends. Every <c>Present</c> presents it, after any dialogue
/// already waiting.
/// </summary>
public sealed class DialogueRunner(
    IDialoguePresenter presenter, IScriptWorld world, string file, string title, IReadOnlyList<Entity> entities)
    : IActionTarget
{
    public async TickTask Present(ITick tick)
    {
        using IDisposable cutscene = world.HoldCutscene();
        await presenter.Present(tick, file, title, entities);
    }
}
