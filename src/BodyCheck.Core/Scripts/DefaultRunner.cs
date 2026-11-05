using BodyCheck.Core.Entities;
using BodyCheck.Core.Tasks;

namespace BodyCheck.Core.Scripts;

/// <summary>What runs a control entity that does one thing, so responds to <see cref="TriggerAction.Default"/> alone.</summary>
public abstract class DefaultRunner : IActionTarget
{
    public TickTask Run(TriggerAction action, ITick tick)
    {
        if (action != TriggerAction.Default)
        {
            throw new ArgumentOutOfRangeException(nameof(action), action, $"A {GetType().Name} responds to Default only.");
        }
        return RunDefault(tick);
    }

    /// <summary>Runs <see cref="TriggerAction.Default"/>.</summary>
    protected abstract TickTask RunDefault(ITick tick);
}
