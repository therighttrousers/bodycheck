using BodyCheck.Core.Entities;
using BodyCheck.Core.Tasks;

namespace BodyCheck.Core.Scripts;

/// <summary>
/// What runs a fanout: on <c>Default</c>, it runs all of its steps at once, in order, and finishes when all have.
/// Every <c>Default</c> runs the steps, alongside any run in progress.
/// </summary>
/// <param name="steps">The steps, at least one.</param>
public sealed class FanoutRunner(IReadOnlyList<Call> steps) : IActionTarget
{
    public async TickTask Default(ITick tick)
    {
        // Start every step before awaiting any.
        TickTask[] runs = [.. steps.Select(step => step.Run(tick))];
        foreach (TickTask run in runs)
        {
            await run;
        }
    }
}
