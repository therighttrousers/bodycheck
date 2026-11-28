using System.Reflection;

using BodyCheck.Core.Entities;

namespace BodyCheck.Core.Scripts;

/// <summary>
/// Matches a script's constructor to the entities it is given. Each entity parameter receives the next entity, in
/// order, and must be of its type. A final <see cref="IReadOnlyList{T}"/> parameter of an entity type receives all the
/// entities that are left, and each must be of its element type. Every entity must be received, so a script with no
/// parameters receives no entities. Any other parameter type makes the match fail.
/// </summary>
public static class ScriptArguments
{
    /// <summary>
    /// Returns the constructor of <paramref name="script"/> if it receives entities of these types, in this order, or
    /// null if it doesn't. A script must have exactly one public constructor to be found. It receives types rather than
    /// entities so that a world can check its scripts when it is built, before it has any entities.
    /// </summary>
    /// <param name="script">The class of the script.</param>
    /// <param name="entityTypes">The types of the entities.</param>
    public static ConstructorInfo? Constructor(Type script, IReadOnlyList<Type> entityTypes) =>
        script.GetConstructors() is [ConstructorInfo constructor]
            && Bind(constructor.GetParameters(), entityTypes, entities: null) is not null
            ? constructor
            : null;

    /// <summary>
    /// Returns the arguments that call <paramref name="constructor"/> with the entities. Throws an
    /// <see cref="ArgumentException"/> if the constructor can't receive them.
    /// </summary>
    /// <remarks>
    /// Pass a constructor that <see cref="Constructor"/> returned for the same entity types.
    /// </remarks>
    public static object[] For(ConstructorInfo constructor, IReadOnlyList<Entity> entities) =>
        Bind(constructor.GetParameters(), [.. entities.Select(entity => entity.GetType())], entities)
            ?? throw new ArgumentException($"{constructor.DeclaringType} can't receive these entities.", nameof(entities));

    private static object[]? Bind(ParameterInfo[] parameters, IReadOnlyList<Type> types, IReadOnlyList<Entity>? entities)
    {
        var arguments = new object[parameters.Length];
        int next = 0;
        for (int i = 0; i < parameters.Length; i++)
        {
            Type type = parameters[i].ParameterType;
            if (type.IsAssignableTo(typeof(Entity)))
            {
                if (next == types.Count || !types[next].IsAssignableTo(type))
                {
                    return null;
                }
                arguments[i] = entities?[next]!;
                next++;
            }
            // A list receives every entity that is left, so it must be the last parameter.
            else if (ListElement(type) is Type element && i == parameters.Length - 1)
            {
                if (!types.Skip(next).All(t => t.IsAssignableTo(element)))
                {
                    return null;
                }
                // Null entities means only the types are checked, so no arguments are built.
                if (entities is not null)
                {
                    // An array of the element type implements IReadOnlyList of that type, so it can be passed for the
                    // list.
                    var rest = Array.CreateInstance(element, types.Count - next);
                    for (int j = next; j < types.Count; j++)
                    {
                        rest.SetValue(entities[j], j - next);
                    }
                    arguments[i] = rest;
                }
                next = types.Count;
            }
            else
            {
                return null;
            }
        }
        // Entities that no parameter took make the match fail.
        return next == types.Count ? arguments : null;
    }

    private static Type? ListElement(Type type) =>
        type.IsGenericType
            && type.GetGenericTypeDefinition() == typeof(IReadOnlyList<>)
            && type.GetGenericArguments()[0] is Type element
            && element.IsAssignableTo(typeof(Entity))
            ? element
            : null;
}
