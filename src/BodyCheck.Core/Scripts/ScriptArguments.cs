using System.Reflection;

using BodyCheck.Core.Entities;

namespace BodyCheck.Core.Scripts;

/// <summary>
/// How a script takes the entities a <c>Script</c> entity gives it: as the parameters of its one public constructor, in
/// order. A parameter of a type of <see cref="Entity"/> takes the next entity, which must be of its type; one of type
/// <see cref="IReadOnlyList{T}"/> of a type of <see cref="Entity"/> takes the rest, each of which must be of its type,
/// so it must be the last. Every entity must be taken. A script with no parameters takes none.
/// </summary>
public static class ScriptArguments
{
    /// <summary>
    /// The one public constructor of <paramref name="script"/>, if it has one, and it takes entities of the types
    /// <paramref name="entityTypes"/>, so that a world can check its scripts when it is built, before it has entities.
    /// </summary>
    public static ConstructorInfo? Constructor(Type script, IReadOnlyList<Type> entityTypes) =>
        script.GetConstructors() is [ConstructorInfo constructor]
            && Bind(constructor.GetParameters(), entityTypes, entities: null) is not null
            ? constructor
            : null;

    /// <summary>
    /// The arguments for <paramref name="constructor"/> that give it <paramref name="entities"/>, as
    /// <see cref="Constructor"/> has checked it can take them.
    /// </summary>
    public static object[] For(ConstructorInfo constructor, IReadOnlyList<Entity> entities) =>
        Bind(constructor.GetParameters(), [.. entities.Select(entity => entity.GetType())], entities)
            ?? throw new ArgumentException($"{constructor.DeclaringType} can't take these entities.", nameof(entities));

    /// <summary>
    /// The arguments that give <paramref name="parameters"/> entities of <paramref name="types"/>, or null if they
    /// can't take them. With no <paramref name="entities"/>, it only checks, and the arguments are nulls.
    /// </summary>
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
            else if (ListElement(type) is Type element && i == parameters.Length - 1)
            {
                if (!types.Skip(next).All(t => t.IsAssignableTo(element)))
                {
                    return null;
                }
                if (entities is not null)
                {
                    // An array of the element type, which is an IReadOnlyList of it.
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
        return next == types.Count ? arguments : null;
    }

    /// <summary>The type of <see cref="Entity"/> that <paramref name="type"/> is an IReadOnlyList of, or null.</summary>
    private static Type? ListElement(Type type) =>
        type.IsGenericType
            && type.GetGenericTypeDefinition() == typeof(IReadOnlyList<>)
            && type.GetGenericArguments()[0] is Type element
            && element.IsAssignableTo(typeof(Entity))
            ? element
            : null;
}
