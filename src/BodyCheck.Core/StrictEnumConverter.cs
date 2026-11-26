using System.Text.Json.Serialization;

namespace BodyCheck.Core;

/// <summary>
/// Reads an enum from its name only, for an enum in a file the game reads (see <see cref="JsonFiles"/>). The default
/// converter also accepts its number.
/// </summary>
public sealed class StrictEnumConverter<TEnum>() : JsonStringEnumConverter<TEnum>(allowIntegerValues: false)
    where TEnum : struct, Enum;
