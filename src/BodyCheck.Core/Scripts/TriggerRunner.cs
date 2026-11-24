using BodyCheck.Core.Entities;
using BodyCheck.Core.Tasks;

namespace BodyCheck.Core.Scripts;

/// <summary>
/// What runs a trigger: when set off, it runs its step, if it is on. Setting it off while the step runs starts
/// nothing. Routes can turn it on and off, which finishes at once, but can't set it off: only the player does.
/// </summary>
/// <param name="state">Its state in the game's state, which says whether it is on, read each time it is needed.</param>
public sealed class TriggerRunner(Call step, Func<IGameState.ISwitchState> state) : IActionTarget
{
    private readonly ReentrantTask _step = new(Reentry.Join);

    /// <summary>Whether it is on. Turning it off doesn't stop a run in progress.</summary>
    public bool IsOn => state().IsOn;

    /// <summary>Runs the step if it is on, unless it is running already.</summary>
    /// <returns>The step's run, which finishes when the step does, or a finished task if it is off.</returns>
    public TickTask SetOff(ITick tick) => IsOn ? _step.Run(() => step.Run(tick)) : TickTask.Finished;

    public TickTask TurnOn(ITick tick) => Switches.Switch(state(), true);
    public TickTask TurnOff(ITick tick) => Switches.Switch(state(), false);
    public TickTask ToggleOnOff(ITick tick) => Switches.Switch(state(), !state().IsOn);
}

/// <summary>Switching a control entity on and off, as its runner responds to a route.</summary>
internal static class Switches
{
    /// <summary>Switches <paramref name="state"/> on or off, at once.</summary>
    public static TickTask Switch(IGameState.ISwitchState state, bool on)
    {
        state.IsOn = on;
        return TickTask.Finished;
    }
}
