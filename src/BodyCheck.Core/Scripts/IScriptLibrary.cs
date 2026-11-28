using BodyCheck.Core.Entities;

namespace BodyCheck.Core.Scripts;

/// <summary>
/// Makes the scripts that a world runs, by class name. The game implements it, so that Core doesn't need to know the
/// game's script types. A script receives its entities as <see cref="ScriptArguments"/> describes.
/// </summary>
public interface IScriptLibrary
{
    /// <summary>
    /// Whether the class is a script that receives entities of these types, in this order. The world checks its scripts
    /// with this when it is built, before it has any entities, so it receives types rather than entities.
    /// </summary>
    /// <param name="className">The class name that a <c>Script</c> entity gives.</param>
    /// <param name="entityTypes">The classes of the entities that the <c>Script</c> entity gives, in order.</param>
    bool Holds(string className, IReadOnlyList<Type> entityTypes);

    /// <summary>Makes a new script of the class, for the entities. Returns a new script on each call.</summary>
    /// <remarks>
    /// Check the class and the entities with <see cref="Holds"/> first.
    /// </remarks>
    IScript Create(string className, IReadOnlyList<Entity> entities);
}
