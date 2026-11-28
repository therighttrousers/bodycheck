using BodyCheck.Core.Tasks;

namespace BodyCheck.Core.Scripts;

/// <summary>
/// The scripts that are running in a world. Run each script through <see cref="Run"/>, so it is listed while it runs.
/// </summary>
public sealed class RunningScripts
{
    private readonly List<IScript> _scripts = [];

    /// <summary>The scripts whose runs haven't finished, in the order they started.</summary>
    public IReadOnlyList<IScript> All => _scripts;

    /// <summary>
    /// Runs the script in the world. Returns the task that finishes when the script does. The script is listed in
    /// <see cref="All"/> until then.
    /// </summary>
    public async TickTask Run(IScript script, IScriptWorld world)
    {
        _scripts.Add(script);
        // Remove in finally, so the list stays correct when the script throws or its scope ends.
        try
        {
            await script.Run(world);
        }
        finally
        {
            _scripts.Remove(script);
        }
    }
}
