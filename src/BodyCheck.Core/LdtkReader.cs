using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

using Godot;

namespace BodyCheck.Core;

/// <summary>
/// Reads the levels of an LDtk project into rooms, one room per level, and checks everything it reads. A level has
/// two layers: <c>SolidGeometry</c>, an IntGrid in which every value but 0 is a solid tile, and <c>Characters</c>,
/// whose entities start with the middle of their base at the middle of the bottom edge of their box in LDtk.
/// </summary>
public static class LdtkReader
{
    /// <summary>The LDtk versions whose JSON this reads: 1.5 and its patches.</summary>
    private const string JsonVersionPrefix = "1.5.";

    /// <summary>
    /// Reads every level of the project at <paramref name="projectPath"/>.
    /// </summary>
    /// <param name="readFile">
    /// Reads a file's text, given its path. A level's file is found from its path relative to the project's directory.
    /// </param>
    /// <exception cref="InvalidDataException">When a file isn't what this reads, naming the file and what is wrong.</exception>
    public static IReadOnlyList<Room> ReadRooms(string projectPath, Func<string, string> readFile)
    {
        LdtkProject project = Parse(projectPath, readFile(projectPath), LdtkJsonContext.Default.LdtkProject);
        if (!project.JsonVersion.StartsWith(JsonVersionPrefix, StringComparison.Ordinal))
        {
            throw Invalid(projectPath, $"JSON version {project.JsonVersion} isn't {JsonVersionPrefix}x.");
        }

        string directory = projectPath[..(projectPath.LastIndexOfAny(['/', '\\']) + 1)];
        var rooms = new List<Room>();
        foreach (LdtkLevel level in project.Levels)
        {
            if (level.ExternalRelPath is null)
            {
                rooms.Add(ToRoom(projectPath, level));
            }
            else
            {
                string levelPath = directory + level.ExternalRelPath;
                rooms.Add(ToRoom(levelPath, Parse(levelPath, readFile(levelPath), LdtkJsonContext.Default.LdtkLevel)));
            }
        }
        return rooms;
    }

    private static Room ToRoom(string path, LdtkLevel level)
    {
        LdtkLayerInstance[] layers = level.LayerInstances
            ?? throw Invalid(path, $"Level '{level.Identifier}' has no layers.");
        foreach (LdtkLayerInstance layer in layers)
        {
            if (layer.GridSize != World.TilePixels
                || layer.CWid * World.TilePixels != level.PxWid
                || layer.CHei * World.TilePixels != level.PxHei
                || layer.PxTotalOffsetX != 0
                || layer.PxTotalOffsetY != 0)
            {
                throw Invalid(path, $"In level '{level.Identifier}', layer {layer.Identifier} isn't a grid of "
                    + $"{World.TilePixels}-pixel cells that covers the level exactly.");
            }
        }

        LdtkLayerInstance Layer(LdtkLayerName name, LdtkLayerType type)
        {
            LdtkLayerInstance[] named = [.. layers.Where(layer => layer.Identifier == name)];
            if (named.Length != 1 || named[0].Type != type)
            {
                throw Invalid(path, $"Level '{level.Identifier}' doesn't have exactly one {name} layer, of type {type}.");
            }
            return named[0];
        }

        LdtkLayerInstance solid = Layer(LdtkLayerName.SolidGeometry, LdtkLayerType.IntGrid);
        LdtkLayerInstance characters = Layer(LdtkLayerName.Characters, LdtkLayerType.Entities);

        int width = solid.CWid;
        int height = solid.CHei;
        if (solid.IntGridCsv.Length != width * height)
        {
            throw Invalid(path, $"In level '{level.Identifier}', {solid.Identifier} has {solid.IntGridCsv.Length} "
                + $"values for {width * height} cells.");
        }
        var tiles = new TileGrid(width, height);
        for (int row = 0; row < height; row++)
        {
            for (int x = 0; x < width; x++)
            {
                tiles.SetSolid(x, height - 1 - row, solid.IntGridCsv[row * width + x] != 0);
            }
        }

        var starts = new List<(EntityKind, Vector2)>();
        foreach (LdtkEntityInstance entity in characters.EntityInstances)
        {
            if (entity.Px.Length != 2 || entity.Pivot.Length != 2)
            {
                throw Invalid(path, $"In level '{level.Identifier}', a {entity.Identifier} has a position or a pivot "
                    + "that isn't two numbers.");
            }
            EntityKind kind = entity.Identifier switch
            {
                LdtkEntityName.PlayerStart => EntityKind.Player,
                LdtkEntityName.RobotBasic => EntityKind.Robot,
                LdtkEntityName.PlatformHorizontal => EntityKind.Platform,
                LdtkEntityName.Block => EntityKind.Block,
                _ => throw new ArgumentOutOfRangeException(nameof(level), entity.Identifier, null),
            };

            // LDtk's y is down, from the top of the level, and the room's is up, from the bottom.
            float left = entity.Px[0] - entity.Pivot[0] * entity.Width;
            float bottom = entity.Px[1] + (1 - entity.Pivot[1]) * entity.Height;
            starts.Add((kind, new Vector2((left + entity.Width / 2f) / World.TilePixels, height - bottom / World.TilePixels)));
        }

        try
        {
            return new Room(level.Identifier, tiles, starts);
        }
        catch (ArgumentException e)
        {
            throw Invalid(path, e.Message, e);
        }
    }

    private static T Parse<T>(string path, string json, JsonTypeInfo<T> type)
    {
        try
        {
            return JsonSerializer.Deserialize(json, type) ?? throw Invalid(path, "The file holds null.");
        }
        catch (JsonException e)
        {
            throw Invalid(path, e.Message, e);
        }
    }

    private static InvalidDataException Invalid(string path, string message, Exception? inner = null) =>
        new($"{path}: {message}", inner);
}
