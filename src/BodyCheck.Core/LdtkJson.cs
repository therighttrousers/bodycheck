using System.Text.Json;
using System.Text.Json.Serialization;

namespace BodyCheck.Core;

// The parts of LDtk's JSON that the loader reads, as records for System.Text.Json. Everything else in the file is
// skipped. Every property here is required, a null is allowed only where the type has a '?', and a string that
// isn't one of an enum's names is an error. See https://ldtk.io/json/ for the whole format.

/// <summary>An LDtk project: a <c>.ldtk</c> file.</summary>
internal sealed record LdtkProject
{
    public required string JsonVersion { get; init; }
    public required LdtkLevel[] Levels { get; init; }
}

/// <summary>
/// A level. In the project file, a level saved in a file of its own has a path to that file and no layers. That
/// file, a <c>.ldtkl</c>, holds the same level again, with its layers.
/// </summary>
internal sealed record LdtkLevel
{
    public required string Identifier { get; init; }
    public required int PxWid { get; init; }
    public required int PxHei { get; init; }
    public required string? ExternalRelPath { get; init; }
    public required LdtkLayerInstance[]? LayerInstances { get; init; }
}

internal sealed record LdtkLayerInstance
{
    [JsonPropertyName("__identifier")]
    public required LdtkLayerName Identifier { get; init; }

    [JsonPropertyName("__type")]
    public required LdtkLayerType Type { get; init; }

    /// <summary>The layer's width, in cells.</summary>
    [JsonPropertyName("__cWid")]
    public required int CWid { get; init; }

    /// <summary>The layer's height, in cells.</summary>
    [JsonPropertyName("__cHei")]
    public required int CHei { get; init; }

    /// <summary>The size of a cell, in pixels.</summary>
    [JsonPropertyName("__gridSize")]
    public required int GridSize { get; init; }

    [JsonPropertyName("__pxTotalOffsetX")]
    public required int PxTotalOffsetX { get; init; }

    [JsonPropertyName("__pxTotalOffsetY")]
    public required int PxTotalOffsetY { get; init; }

    /// <summary>An IntGrid layer's values, one per cell, top row first; 0 is empty. Empty in other layers.</summary>
    public required int[] IntGridCsv { get; init; }

    /// <summary>An Entities layer's entities. Empty in other layers.</summary>
    public required LdtkEntityInstance[] EntityInstances { get; init; }
}

internal sealed record LdtkEntityInstance
{
    [JsonPropertyName("__identifier")]
    public required LdtkEntityName Identifier { get; init; }

    /// <summary>The entity's pivot as fractions of its size, from its top left: [x, y].</summary>
    [JsonPropertyName("__pivot")]
    public required float[] Pivot { get; init; }

    /// <summary>Where the entity's pivot is, in pixels from the layer's top left: [x, y].</summary>
    public required int[] Px { get; init; }

    /// <summary>The entity's width, in pixels.</summary>
    public required int Width { get; init; }

    /// <summary>The entity's height, in pixels.</summary>
    public required int Height { get; init; }
}

[JsonConverter(typeof(StrictEnumConverter<LdtkLayerType>))]
internal enum LdtkLayerType
{
    IntGrid,
    Entities,
    Tiles,
    AutoLayer,
}

/// <summary>The layers that levels in this project have.</summary>
[JsonConverter(typeof(StrictEnumConverter<LdtkLayerName>))]
internal enum LdtkLayerName
{
    SolidGeometry,
    Characters,
}

/// <summary>The entities defined in this project.</summary>
[JsonConverter(typeof(StrictEnumConverter<LdtkEntityName>))]
internal enum LdtkEntityName
{
    PlayerStart,
    RobotBasic,
    PlatformHorizontal,
    Block,
}

/// <summary>Reads an enum from its name only. The default converter also accepts its number.</summary>
internal sealed class StrictEnumConverter<TEnum>() : JsonStringEnumConverter<TEnum>(allowIntegerValues: false)
    where TEnum : struct, Enum;

/// <summary>The code that reads the records, which System.Text.Json's source generator writes at compile time.</summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    RespectNullableAnnotations = true)]
[JsonSerializable(typeof(LdtkProject))]
[JsonSerializable(typeof(LdtkLevel))]
internal sealed partial class LdtkJsonContext : JsonSerializerContext;
