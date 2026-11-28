using BodyCheck.Core.Entities;
using BodyCheck.Core.Tasks;

namespace BodyCheck.Core.Scripts;

/// <summary>
/// The action target that runs a <c>Script</c> control entity. It makes a script from its class name and runs it. Each
/// run is listed among the running scripts until it finishes. Runs can overlap, since there is no reentry policy: each
/// <see cref="Run"/> starts a run alongside any run in progress. Each run makes a new script, so runs share no state.
/// </summary>
/// <param name="library">Makes the script.</param>
/// <param name="className">The class of the script.</param>
/// <param name="entities">The entities the script receives, in the order its constructor receives them.</param>
/// <param name="world">The world the script runs in.</param>
/// <param name="running">Lists the scripts that are running.</param>
public sealed class ScriptRunner(
    IScriptLibrary library,
    string className,
    IReadOnlyList<Entity> entities,
    IScriptWorld world,
    RunningScripts running) : IActionTarget
{
    /// <summary>Makes a new script and runs it. Returns the task that finishes when the script does.</summary>
    public TickTask Run(ITick tick) => running.Run(library.Create(className, entities), world);
}
