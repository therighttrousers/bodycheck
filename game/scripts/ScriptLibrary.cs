using System;

using BodyCheck.Core.Scripts;

namespace BodyCheck.Scripts;

/// <summary>
/// The game's <see cref="IScriptLibrary"/>: the classes in this namespace that implement <see cref="IScript"/> and have
/// a constructor of no arguments, by their names without the namespace, so that levels name them briefly.
/// </summary>
public sealed class ScriptLibrary : IScriptLibrary
{
    public bool Holds(string className) => Find(className) is not null;

    public IScript Create(string className) =>
        (IScript)Activator.CreateInstance(
            Find(className) ?? throw new ArgumentException($"{className} isn't a script.", nameof(className)))!;

    private static Type? Find(string className)
    {
        Type? type = typeof(ScriptLibrary).Assembly.GetType($"{typeof(ScriptLibrary).Namespace}.{className}");
        return type is { IsClass: true, IsAbstract: false } && type.IsAssignableTo(typeof(IScript))
            && type.GetConstructor(Type.EmptyTypes) is not null
            ? type
            : null;
    }
}
