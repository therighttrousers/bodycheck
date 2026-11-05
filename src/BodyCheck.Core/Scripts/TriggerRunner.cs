using BodyCheck.Core.Tasks;

namespace BodyCheck.Core.Scripts;

/// <summary>
/// What runs a trigger: when touched, it runs its step. A touch while the step runs starts nothing. It responds to no
/// action, so nothing else runs it.
/// </summary>
public sealed class TriggerRunner(Call step)
{
    private readonly ReentrantTask _step = new(Reentry.Join);

    /// <summary>Runs the step, unless it is running already.</summary>
    /// <returns>The step's run, which finishes when the step does.</returns>
    public TickTask Touch(ITick tick) => _step.Run(() => step.Run(tick));
}
