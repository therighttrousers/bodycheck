using BodyCheck.Core.Entities;
using BodyCheck.Core.Tasks;

namespace BodyCheck.Core.Scripts;

/// <summary>
/// What runs a trigger: when set off, it runs its step, if it is on. Setting it off while the step runs starts
/// nothing. Routes can turn it on and off, which finishes at once, but can't set it off: only the player does.
/// </summary>
/// <param name="startsOn">Whether it is on before any route turns it on or off.</param>
public sealed class TriggerRunner(Call step, bool startsOn) : IActionTarget
{
    private readonly ReentrantTask _step = new(Reentry.Join);

    /// <summary>Whether it is on. Turning it off doesn't stop a run in progress.</summary>
    public bool IsOn { get; private set; } = startsOn;

    /// <summary>Runs the step if it is on, unless it is running already.</summary>
    /// <returns>The step's run, which finishes when the step does, or a finished task if it is off.</returns>
    public TickTask SetOff(ITick tick) => IsOn ? _step.Run(() => step.Run(tick)) : TickTask.Finished;

    public TickTask TurnOn(ITick tick) => Switch(true);
    public TickTask TurnOff(ITick tick) => Switch(false);
    public TickTask ToggleOnOff(ITick tick) => Switch(!IsOn);

    private TickTask Switch(bool on)
    {
        IsOn = on;
        return TickTask.Finished;
    }
}
