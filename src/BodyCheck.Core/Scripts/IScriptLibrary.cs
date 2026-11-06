namespace BodyCheck.Core.Scripts;

/// <summary>
/// The scripts the world can run, by name, which the game implements, so that Core doesn't need to know the game's types.
/// </summary>
public interface IScriptLibrary
{
    /// <summary>
    /// Whether <paramref name="className"/> names a script, so that a world can check its scripts when it is built, not
    /// when they run.
    /// </summary>
    /// <param name="className">The script's class, by the name a <c>Script</c> entity gives it.</param>
    bool Holds(string className);

    /// <summary>Makes a new instance of the script <paramref name="className"/>, for one run.</summary>
    /// <param name="className">The script's class, as <see cref="Holds"/> takes it.</param>
    IScript Create(string className);
}
