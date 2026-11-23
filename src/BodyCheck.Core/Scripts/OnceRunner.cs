using BodyCheck.Core.Entities;
using BodyCheck.Core.Tasks;

namespace BodyCheck.Core.Scripts;

/// <summary>
/// What runs a once: on <c>Run</c>, it runs its step, if it is on, and turns itself off when the step finishes, so that
/// every <c>Run</c> after finishes at once. One made while the step runs gets that run, and finishes when it does.
/// Routes can turn it on again, and off, as they can a trigger, which finishes at once.
/// <para>
/// It turns off when the step finishes, not when it starts, so that a world built again while the step runs, by a
/// restart, a death or a load, runs the step again from its start, rather than leaving it half done.
/// </para>
/// </summary>
/// <param name="state">Its state in the game's state, which says whether it is on, read each time it is needed.</param>
public sealed class OnceRunner(Call step, Func<IGameState.ISwitchState> state) : IActionTarget
{
    private readonly ReentrantTask _step = new(Reentry.Join);

    public TickTask Run(ITick tick) => state().IsOn ? _step.Run(() => RunThenTurnOff(tick)) : TickTask.Finished;

    private async TickTask RunThenTurnOff(ITick tick)
    {
        await step.Run(tick);
        state().IsOn = false;
    }

    public TickTask TurnOn(ITick tick) => Switches.Switch(state(), true);
    public TickTask TurnOff(ITick tick) => Switches.Switch(state(), false);
    public TickTask ToggleOnOff(ITick tick) => Switches.Switch(state(), !state().IsOn);
}
