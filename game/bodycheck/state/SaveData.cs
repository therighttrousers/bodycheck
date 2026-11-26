using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

using BodyCheck.Core.Entities;

namespace BodyCheck.State;

/// <summary>
/// A save, as its file holds it: only what of the game's state the levels' data can't make again. Names and kinds come
/// from the data, so each kind's states are a map by id, since an id never changes kind. <see cref="GameState"/> is
/// made from one, and writes one; nothing else reads it.
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
    public string? WorldName { get; init; }

    /// <summary>Each world's current respawn's id, by the world's name.</summary>
    public required IReadOnlyDictionary<string, string> RespawnIds { get; init; }

    /// <summary>The player's abilities.</summary>
    public required PlayerData Player { get; init; }

    /// <summary>Each door's last action, by id.</summary>
    public required IReadOnlyDictionary<string, OpenableAction> Doors { get; init; }

    /// <summary>Whether each trigger and once is on, by id.</summary>
    public required IReadOnlyDictionary<string, bool> Switches { get; init; }

    /// <summary>The player's abilities, as a save holds them.</summary>
    public sealed record PlayerData
    {
        public required bool CanPhase { get; init; }
        public required bool CanSpawn { get; init; }
    }
}

[JsonSourceGenerationOptions(
    RespectNullableAnnotations = true,
    UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    UseStringEnumConverter = true,
    WriteIndented = true)]
[JsonSerializable(typeof(SaveData))]
internal sealed partial class SaveJsonContext : JsonSerializerContext;
