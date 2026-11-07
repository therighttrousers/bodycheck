using BodyCheck.Core.Entities;
using BodyCheck.Core.Tasks;

namespace BodyCheck.Core.Scripts;

/// <summary>
/// What runs a <c>Script</c> control entity: on <c>Default</c>, it runs a new instance of its script, given its
/// <paramref name="entities"/>, among the world's <paramref name="running"/> scripts, and finishes when the script does.
/// Every <c>Default</c> runs one, alongside any run in progress.
/// </summary>
/// <param name="className">The script's class, by the name <paramref name="library"/> knows it by.</param>
public sealed class ScriptRunner(
    IScriptLibrary library,
    string className,
    IReadOnlyList<Entity> entities,
    IScriptWorld world,
    RunningScripts running) : IActionTarget
{
    public TickTask Default(ITick tick) => running.Run(library.Create(className, entities), world);
}
