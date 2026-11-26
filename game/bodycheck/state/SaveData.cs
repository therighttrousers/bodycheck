using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

using Godot;

namespace BodyCheck.State;

/// <summary>
/// A save, as its file holds it: only what of the game's state the levels' data can't make again. Names come from the
/// data. <see cref="GameState"/> is made from one, and writes one; nothing else reads it.
/// <para>
/// What a save holds may be plain C#, such as an entity's record, or a Godot object, such as the story's beats, which
/// the dialogue sees as they are; of a Godot object, only the members declared in the game are written, not the
/// engine's. Each property is <c>{ get; set; }</c>, with how a new game starts as its initializer, so
/// that a file written before it was added reads; or <c>required</c>, if its start isn't known here, so that a file
/// without it is kept aside. Never <c>init</c> without <c>required</c>: the source generator gives such a property
/// that a file lacks its type's default, such as null, not its initializer. <c>SaveTest</c> checks this.
/// </para>
/// </summary>
public sealed record SaveData
{
    /// <summary>The version of the file's format that the game reads and writes.</summary>
    public const int CurrentVersion = 1;

    /// <summary>The version of the file's format, which must be <see cref="CurrentVersion"/>.</summary>
    public required int Version { get; init; }

    /// <summary>When the game was started, which a menu of saves would show.</summary>
    public required DateTimeOffset Started { get; init; }

    /// <summary>When the game was last saved, which a menu of saves would show.</summary>
    public required DateTimeOffset Saved { get; init; }

    /// <summary>The name of the world being played, or null if none had been built.</summary>
    public string? WorldName { get; set; }

    /// <summary>What has happened in the story. A save from before there was a story has none, and reads as a new one.</summary>
    public StoryState Story { get; set; } = new();

    /// <summary>Each world's current respawn's id, by the world's name.</summary>
    public required IReadOnlyDictionary<string, string> RespawnIds { get; init; }

    /// <summary>
    /// What is saved of each entity's state, and the player's, by id. Entities with no state of their own have none.
    /// </summary>
    public required IReadOnlyDictionary<string, EntityData> Entities { get; init; }
}

[JsonSourceGenerationOptions(
    RespectNullableAnnotations = true,
    UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    UseStringEnumConverter = true,
    WriteIndented = true)]
[JsonSerializable(typeof(SaveData))]
internal sealed partial class SaveJsonContext : JsonSerializerContext;

/// <summary>
/// How a save is read and written: as <see cref="SaveJsonContext"/>'s generated code says, but of a Godot object, only
/// what the game declares, not the engine's members, such as its <c>NativeInstance</c>.
/// </summary>
internal static class SaveJson
{
    public static JsonTypeInfo<SaveData> Info { get; } = (JsonTypeInfo<SaveData>)new JsonSerializerOptions(
        SaveJsonContext.Default.Options)
    {
        TypeInfoResolver = SaveJsonContext.Default.WithAddedModifier(SkipEngineMembers),
    }.GetTypeInfo(typeof(SaveData));

    private static void SkipEngineMembers(JsonTypeInfo info)
    {
        if (typeof(GodotObject).IsAssignableFrom(info.Type))
        {
            for (int i = info.Properties.Count - 1; i >= 0; i--)
            {
                if ((info.Properties[i].AttributeProvider as MemberInfo)?.DeclaringType?.Assembly
                    != typeof(SaveData).Assembly)
                {
                    info.Properties.RemoveAt(i);
                }
            }
        }
    }
}
