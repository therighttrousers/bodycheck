using BodyCheck.Core.Tasks;

namespace BodyCheck.Core.Scripts;

/// <summary>The scripts a world is running, in the order they started.</summary>
public sealed class RunningScripts
{
    private readonly List<IScript> _scripts = [];

    public IReadOnlyList<IScript> All => _scripts;

    /// <summary>Runs <paramref name="script"/>, which is among <see cref="All"/> until it finishes or fails.</summary>
    public async TickTask Run(IScript script, IScriptWorld world, ITick tick)
    {
        _scripts.Add(script);
        try
        {
            await script.Run(world, tick);
        }
        finally
        {
            _scripts.Remove(script);
        }
    }
}
