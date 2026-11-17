using BodyCheck.Core.Entities;

using Godot;

namespace BodyCheck;

/// <summary>What a dialogue file sees of an entity the dialogue is about, through its <see cref="DialogueState"/>.</summary>
public sealed partial class DialogueEntity : RefCounted
{
    /// <summary>The entity, which the dialogue asks for actions through its controller.</summary>
    internal Entity Entity { get; init; } = null!;

    /// <summary>Its name, such as <c>Door a6d1</c>.</summary>
    public string Name { get; init; } = "";

    /// <summary>Its kind's name, such as <c>Door</c>, which its avatar is named for.</summary>
    public string Kind { get; init; } = "";
}
