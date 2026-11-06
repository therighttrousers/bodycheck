using BodyCheck.Core.Entities;
using BodyCheck.Core.Tasks;

namespace BodyCheck.Core.Scripts;

/// <summary>
/// What runs a dialogue: on <c>Default</c>, it waits for the player to be on the ground, for up to
/// <see cref="MaxOnGroundWait"/> seconds, so that the game doesn't pause with it in the air, then presents the title
/// <paramref name="title"/> of the dialogue file <paramref name="file"/>, and finishes when the dialogue ends. Every
/// <c>Default</c> presents it, after any dialogue already waiting.
/// </summary>
public sealed class DialogueRunner(IDialoguePresenter presenter, IWorldNode world, string file, string title) : IActionTarget
{
    /// <summary>The longest it waits for the player to be on the ground, in seconds of game time.</summary>
    private const float MaxOnGroundWait = 2;

    public async TickTask Default(ITick tick)
    {
        float totalTime = 0;
        while (totalTime < MaxOnGroundWait)
        {
            await tick.NextTick();
            totalTime += tick.Dt;
            if (world.Player.OnGround)
            {
                break;
            }
        }
        await presenter.Present(tick, file, title);
    }
}
