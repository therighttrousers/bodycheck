using System;
using System.IO;
using System.Text.Json;

using Godot;

namespace BodyCheck.State;

/// <summary>
/// Where saves are kept: a file for each slot, <c>{slot}.json</c>, in one directory, so that the slots are the files
/// in it. It reads and writes saves only; the game's state turns itself into a save and back.
/// </summary>
/// <param name="directory">The directory, which may be a Godot path, such as <c>user://saves</c>.</param>
public sealed class SaveStore(string directory)
{
    /// <summary>The game's saves' directory.</summary>
    public const string GameDirectory = "user://saves";

    /// <summary>The one slot there is for now.</summary>
    public const string DefaultSlot = "default";

    private string PathOf(string slot) => Path.Combine(ProjectSettings.GlobalizePath(directory), slot + ".json");

    /// <summary>
    /// The save in <paramref name="slot"/>, or null if there is none. A file that can't be read, doesn't parse, or
    /// isn't of <see cref="SaveData.CurrentVersion"/> is renamed aside, to <c>{slot}.bad.json</c>, so that the next
    /// game doesn't write over it, and throws.
    /// </summary>
    /// <exception cref="InvalidDataException">The file isn't a save the game reads, naming it and where it went.</exception>
    public SaveData? Load(string slot)
    {
        string path = PathOf(slot);
        if (!File.Exists(path))
        {
            return null;
        }
        try
        {
            SaveData save = JsonSerializer.Deserialize(File.ReadAllText(path), SaveJson.Info)
                ?? throw new InvalidDataException("The file holds null.");
            return save.Version == SaveData.CurrentVersion
                ? save
                : throw new InvalidDataException($"It is version {save.Version}, not {SaveData.CurrentVersion}.");
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException or InvalidDataException)
        {
            string aside = Path.ChangeExtension(path, ".bad.json");
            File.Move(path, aside, overwrite: true);
            throw new InvalidDataException($"{path} isn't a save the game reads, so it is kept as {aside}: {e.Message}", e);
        }
    }

    /// <summary>
    /// Writes <paramref name="save"/> to <paramref name="slot"/>, to a temporary file first, which then replaces the
    /// old one, so that a crash while writing leaves the last save whole.
    /// </summary>
    /// <exception cref="IOException">It can't be written.</exception>
    public void Save(string slot, SaveData save)
    {
        string path = PathOf(slot);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        string temporary = path + ".tmp";
        File.WriteAllText(temporary, JsonSerializer.Serialize(save, SaveJson.Info));
        File.Move(temporary, path, overwrite: true);
    }
}
