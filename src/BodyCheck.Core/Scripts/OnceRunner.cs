using BodyCheck.Core.Tasks;

namespace BodyCheck.Core.Scripts;

/// <summary>
/// What runs a once: on the first <c>Default</c>, it runs its step, and every <c>Default</c> gets that first run, so
/// one made while it runs finishes when it does, and one made after finishes at once. What it remembers lasts as long
/// as it does.
/// </summary>
public sealed class OnceRunner(Call step) : DefaultRunner
{
    private readonly ReentrantTask _step = new(Reentry.Once, step.Run);

    protected override TickTask RunDefault(ITick tick) => _step.Run(tick);
}
