using BodyCheck.Core.Entities;
using BodyCheck.Core.Tasks;

namespace BodyCheck.Core.Scripts;

/// <summary>
/// What runs a sequence: on <c>Default</c>, it runs each of its steps in turn, each when the one before has finished,
/// and finishes when the last does. Every <c>Default</c> runs the steps, alongside any run in progress.
/// </summary>
/// <param name="steps">The steps, at least one.</param>
public sealed class SequenceRunner(IReadOnlyList<Call> steps) : IActionTarget
{
    public async TickTask Default(ITick tick)
    {
        foreach (Call step in steps)
        {
            await step.Run(tick);
        }
    }
}
