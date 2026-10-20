using System.Text.Json.Serialization;

namespace BodyCheck.Core;

/// <summary>Reads an enum from its name only. The default converter also accepts its number.</summary>
internal sealed class StrictEnumConverter<TEnum>() : JsonStringEnumConverter<TEnum>(allowIntegerValues: false)
    where TEnum : struct, Enum;
