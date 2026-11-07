using BodyCheck.Core.Entities;

namespace BodyCheck.Core.Scripts;

/// <summary>
/// The scripts the world can run, by name, which the game implements, so that Core doesn't need to know the game's types.
/// A script takes the entities it is given as <see cref="ScriptArguments"/> says.
/// </summary>
public interface IScriptLibrary
{
    /// <summary>
    /// Whether <paramref name="className"/> names a script that takes entities of <paramref name="entityTypes"/>, so
    /// that a world can check its scripts when it is built, not when they run.
    /// </summary>
    /// <param name="className">The script's class, by the name a <c>Script</c> entity gives it.</param>
    /// <param name="entityTypes">The classes of the entities the <c>Script</c> entity gives it, in order.</param>
    bool Holds(string className, IReadOnlyList<Type> entityTypes);

    /// <summary>Makes a new instance of the script <paramref name="className"/>, for one run.</summary>
    /// <param name="className">The script's class, as <see cref="Holds"/> takes it.</param>
    /// <param name="entities">The entities it is given, of the types <see cref="Holds"/> was asked about.</param>
    IScript Create(string className, IReadOnlyList<Entity> entities);
}
