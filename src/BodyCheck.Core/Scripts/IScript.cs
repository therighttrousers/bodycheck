using BodyCheck.Core.Tasks;

namespace BodyCheck.Core.Scripts;

/// <summary>
/// A script that runs game logic over several ticks. A <c>Script</c> control entity runs one, for logic that wiring
/// control entities together can't express. A script may hold a cutscene while it runs, but many hold none. A
/// <see cref="IScriptLibrary"/> makes a new script for each run, and the script is dropped when its run finishes, so it
/// can store nothing between runs. It receives its entities through its one public constructor, as
/// <see cref="ScriptArguments"/> describes.
/// </summary>
public interface IScript
{
    /// <summary>Runs the script in the world. Returns the task that finishes when the script does.</summary>
    /// <remarks>
    /// Usually written as an <c>async</c> method. Hold any cutscene in a <c>using</c> block, so the hold is released
    /// however the script ends.
    /// </remarks>
    TickTask Run(IScriptWorld world);
}
