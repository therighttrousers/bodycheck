using BodyCheck.Core.Entities;
using BodyCheck.Core.Tasks;

namespace BodyCheck.Core.Scripts;

/// <summary>
/// The action target that runs a <c>Sequence</c> control entity. It runs its steps in turn, each one after the one
/// before finishes. A second <see cref="Run"/> while one is in progress starts the steps again from the first,
/// alongside the run in progress. Compare <see cref="FanoutRunner"/>, which runs its steps together.
/// </summary>
/// <param name="steps">The steps to run, in order.</param>
public sealed class SequenceRunner(IReadOnlyList<Call> steps) : IActionTarget
{
    /// <summary>Runs the steps in order. Returns the task that finishes after the last step does.</summary>
    public async TickTask Run(ITick tick)
    {
        foreach (Call step in steps)
        {
            await step.Run(tick);
        }
    }
}
