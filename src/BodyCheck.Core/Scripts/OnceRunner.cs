using BodyCheck.Core.Entities;
using BodyCheck.Core.Tasks;

namespace BodyCheck.Core.Scripts;

/// <summary>
/// The action target that runs a <c>Once</c> control entity. When it is asked to <see cref="Run"/> and its switch is
/// on, it runs its step, then turns the switch off. The switch is turned off when the step finishes rather than when it
/// starts, so if the world is built again while the step runs (after a restart, a death or a load), the step runs again
/// from its start rather than being left half done. Use it for a step that should happen once, unless something turns
/// the switch back on.
/// </summary>
/// <param name="step">The step to run.</param>
/// <param name="state">
/// Returns the entity's switch in the game's state, which says whether the step is still to run. The state is saved and
/// outlives the world, so this is called each time the switch is needed.
/// </param>
public sealed class OnceRunner(Call step, Func<IGameState.ISwitchState> state) : IActionTarget
{
    // A run that starts while one is still going joins it, so the step doesn't run twice.
    private readonly ReentrantTask _step = new(Reentry.Join);

    /// <summary>
    /// Runs the step if the switch is on, and returns the task that finishes when the step does. If the step is already
    /// running, returns the task of that run. Returns a finished task if the switch is off.
    /// </summary>
    public TickTask Run(ITick tick) => state().IsOn ? _step.Run(() => RunThenTurnOff(tick)) : TickTask.Finished;

    private async TickTask RunThenTurnOff(ITick tick)
    {
        await step.Run(tick);
        // Turned off only after the step finishes, so a rebuilt world runs the step again from its start. If the step
        // throws, the switch also stays on.
        state().IsOn = false;
    }

    /// <summary>Turns the switch on, without running the step.</summary>
    public TickTask TurnOn(ITick tick) => Switches.Switch(state(), true);
    /// <summary>Turns the switch off, without running the step.</summary>
    public TickTask TurnOff(ITick tick) => Switches.Switch(state(), false);
    /// <summary>Flips the switch, without running the step.</summary>
    public TickTask ToggleOnOff(ITick tick) => Switches.Switch(state(), !state().IsOn);
}
