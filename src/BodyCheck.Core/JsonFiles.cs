using System.Text.Json;
using System.Text.Json.Serialization;

namespace BodyCheck.Core;

/// <summary>
/// How the game's JSON files are read and written, configurations and saves alike: strictly, so that a mistake is an
/// error that says where it is rather than a value quietly lost, and leniently in what is easy to get wrong when
/// editing a file by hand. Names are the properties' own, as in C#, and enums are read by name only, through
/// <see cref="StrictEnumConverter{TEnum}"/> on each enum.
/// </summary>
public static class JsonFiles
{
    /// <summary>
    /// The options for reading and writing a file, for a source generator's context to be made with, or to be given
    /// its resolver: nulls only where a property allows them, nothing unknown, comments and trailing commas allowed,
    /// and indented when written.
    /// </summary>
    public static JsonSerializerOptions Options() => new()
    {
        RespectNullableAnnotations = true,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        WriteIndented = true,
    };

    /// <summary>The same leniency, for parsing a file into a <see cref="JsonDocument"/> or a JSON node.</summary>
    public static JsonDocumentOptions DocumentOptions { get; } =
        new() { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true };
}
