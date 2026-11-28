using BodyCheck.Core.Entities;
using BodyCheck.Core.Tasks;

namespace BodyCheck.Core.Scripts;

/// <summary>
/// The action target that runs a <c>Trigger</c> control entity. It runs its step when the trigger is set off, while the
/// trigger's switch is on. The world sets a trigger off when the player touches its area, interacts with it, or when
/// the world starts. A route can switch a trigger on and off, but can't set it off, since this runner doesn't respond
/// to <see cref="IActionTarget.Run"/>.
/// </summary>
/// <remarks>
/// The switch stays on after the step runs, so a trigger that stays on runs its step each time it is set off.
/// </remarks>
/// <param name="step">The step to run when the trigger is set off.</param>
/// <param name="state">
/// Returns the entity's switch in the game's state, which says whether the trigger is on. The state is saved and
/// outlives the world, so this is called each time the switch is needed.
/// </param>
public sealed class TriggerRunner(Call step, Func<IGameState.ISwitchState> state) : IActionTarget
{
    // A set-off during a run of the step joins that run, so the step doesn't run twice at once.
    private readonly ReentrantTask _step = new(Reentry.Join);

    /// <summary>
    /// Whether the switch is on, so the trigger runs its step when it is set off. Turning the switch off doesn't stop a
    /// run that is in progress.
    /// </summary>
    public bool IsOn => state().IsOn;

    /// <summary>
    /// Runs the step if the switch is on, and returns the task that finishes when the step does. If the step is already
    /// running, returns the task of that run. Returns a finished task if the switch is off.
    /// </summary>
    public TickTask SetOff(ITick tick) => IsOn ? _step.Run(() => step.Run(tick)) : TickTask.Finished;

    /// <summary>Turns the switch on, without running the step.</summary>
    public TickTask TurnOn(ITick tick) => Switches.Switch(state(), true);
    /// <summary>Turns the switch off, without running the step.</summary>
    public TickTask TurnOff(ITick tick) => Switches.Switch(state(), false);
    /// <summary>Flips the switch, without running the step.</summary>
    public TickTask ToggleOnOff(ITick tick) => Switches.Switch(state(), !state().IsOn);
}

internal static class Switches
{
    /// <summary>Sets the switch now, and returns a finished task, since the change takes no time.</summary>
    public static TickTask Switch(IGameState.ISwitchState state, bool on)
    {
        state.IsOn = on;
        return TickTask.Finished;
    }
}
