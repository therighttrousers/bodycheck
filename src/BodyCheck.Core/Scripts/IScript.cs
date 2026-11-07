using BodyCheck.Core.Tasks;

namespace BodyCheck.Core.Scripts;

/// <summary>
/// A script, which a <c>Script</c> control entity runs: C# for what wiring control entities can't say. A new one is made
/// for each run, with a constructor of no arguments, and dropped when the run finishes.
/// </summary>
public interface IScript
{
    /// <summary>Runs the script, usually as an <c>async</c> method. The task finishes when the script does.</summary>
    /// <param name="world">What the script can see and do in the world it runs in.</param>
    TickTask Run(IScriptWorld world);
}
