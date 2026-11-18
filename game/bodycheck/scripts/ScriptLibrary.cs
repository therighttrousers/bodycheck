using System;
using System.Collections.Generic;
using System.Linq;

using BodyCheck.Core.Entities;
using BodyCheck.Core.Scripts;

namespace BodyCheck.Scripts;

/// <summary>
/// The game's <see cref="IScriptLibrary"/>: the classes in this namespace that implement <see cref="IScript"/>, by their
/// names without the namespace, so that levels name them briefly. Each takes its entities through its one public
/// constructor, as <see cref="ScriptArguments"/> says.
/// </summary>
public sealed class ScriptLibrary : IScriptLibrary
{
    public bool Holds(string className, IReadOnlyList<Type> entityTypes) =>
        Find(className) is Type type && ScriptArguments.Constructor(type, entityTypes) is not null;

    public IScript Create(string className, IReadOnlyList<Entity> entities)
    {
        Type type = Find(className) ?? throw new ArgumentException($"{className} isn't a script.", nameof(className));
        var constructor = ScriptArguments.Constructor(type, [.. entities.Select(entity => entity.GetType())])
            ?? throw new ArgumentException($"{className} doesn't take these entities.", nameof(entities));
        return (IScript)constructor.Invoke(ScriptArguments.For(constructor, entities));
    }

    private static Type? Find(string className)
    {
        Type? type = typeof(ScriptLibrary).Assembly.GetType($"{typeof(ScriptLibrary).Namespace}.{className}");
        return type is { IsClass: true, IsAbstract: false } && type.IsAssignableTo(typeof(IScript)) ? type : null;
    }
}
