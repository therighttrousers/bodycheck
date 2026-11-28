using BodyCheck.Core.Entities;
using BodyCheck.Core.Tasks;

namespace BodyCheck.Core.Scripts;

/// <summary>
/// The action target that runs a <c>Dialogue</c> control entity. It presents a title from a dialogue file, and holds a
/// cutscene until the dialogue ends.
/// </summary>
/// <param name="presenter">The presenter that shows the dialogue.</param>
/// <param name="world">The world whose cutscene holds while the dialogue is shown.</param>
/// <param name="file">The dialogue file. It must contain <paramref name="title"/>.</param>
/// <param name="title">The title where the dialogue starts.</param>
/// <param name="entities">The entities that the dialogue is about, passed on unchanged.</param>
public sealed class DialogueRunner(
    IDialoguePresenter presenter, IScriptWorld world, string file, string title, IReadOnlyList<Entity> entities)
    : IActionTarget
{
    /// <summary>
    /// Presents the dialogue. Returns the task that finishes when the dialogue ends. Each call presents the dialogue
    /// again, queued after any dialogue that is already being presented or waiting. Calls are not joined.
    /// </summary>
    public async TickTask Present(ITick tick)
    {
        using IDisposable cutscene = world.HoldCutscene();
        await presenter.Present(tick, file, title, entities);
    }
}
