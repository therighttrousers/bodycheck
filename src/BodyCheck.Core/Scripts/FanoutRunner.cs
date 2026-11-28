using BodyCheck.Core.Entities;
using BodyCheck.Core.Tasks;

namespace BodyCheck.Core.Scripts;

/// <summary>
/// The action target that runs a <c>Fanout</c> control entity. It runs several steps together. Each <see cref="Run"/>
/// starts every step, in their listed order, and finishes when the last one does. A second <see cref="Run"/> while one
/// is in progress starts the steps again, alongside the run in progress. There is no join or queue.
/// Compare <see cref="SequenceRunner"/>, which runs its steps in turn.
/// </summary>
/// <param name="steps">The steps to run together.</param>
public sealed class FanoutRunner(IReadOnlyList<Call> steps) : IActionTarget
{
    /// <summary>Starts every step. Returns the task that finishes once every step has finished.</summary>
    public TickTask Run(ITick tick) => tick.All(steps.Select(step => step.Run(tick)));
}
