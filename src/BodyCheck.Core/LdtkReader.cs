using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

using Godot;

namespace BodyCheck.Core;

/// <summary>
/// Reads the levels of an LDtk project, stitched together into one room, and checks everything it reads. Levels are
/// 20x15 tiles, on a grid of that size; grid cells with no level are solid. A level has two layers:
/// <c>SolidGeometry</c>, an IntGrid in which every value but 0 is a solid tile, and <c>Characters</c>, whose entities
/// start with the middle of their base at the middle of the bottom edge of their box in LDtk.
/// </summary>
public static class LdtkReader
{
    /// <summary>The LDtk versions whose JSON this reads: 1.5 and its patches.</summary>
    private const string JsonVersionPrefix = "1.5.";

    /// <summary>A level's width, in tiles.</summary>
    private const int LevelWidth = 20;

    /// <summary>A level's height, in tiles.</summary>
    private const int LevelHeight = 15;

    /// <summary>
    /// Reads every level of the project at <paramref name="projectPath"/> into one room, named after the project's file.
    /// </summary>
    /// <param name="readFile">
    /// Reads a file's text, given its path. A level's file is found from its path relative to the project's directory.
    /// </param>
    /// <exception cref="InvalidDataException">When a file isn't what this reads, naming the file and what is wrong.</exception>
    public static Room ReadWorld(string projectPath, Func<string, string> readFile)
    {
        LdtkProject project = Parse(projectPath, readFile(projectPath), LdtkJsonContext.Default.LdtkProject);
        if (!project.JsonVersion.StartsWith(JsonVersionPrefix, StringComparison.Ordinal))
        {
            throw Invalid(projectPath, $"JSON version {project.JsonVersion} isn't {JsonVersionPrefix}x.");
        }
        if (project.Worlds.Length != 0)
        {
            throw Invalid(projectPath, "The project has more than one world.");
        }
        if (project.Levels.Length == 0)
        {
            throw Invalid(projectPath, "The project has no levels.");
        }

        // Each level's cell on the grid of levels, with y down, as in LDtk.
        var cells = new HashSet<(int X, int Y)>();
        foreach (LdtkLevel level in project.Levels)
        {
            if (level.PxWid != LevelWidth * World.TilePixels
                || level.PxHei != LevelHeight * World.TilePixels
                || level.WorldX % level.PxWid != 0
                || level.WorldY % level.PxHei != 0
                || level.WorldDepth != 0)
            {
                throw Invalid(projectPath, $"Level '{level.Identifier}' isn't {LevelWidth}x{LevelHeight} tiles, on "
                    + "a grid of that size, at depth 0.");
            }
            if (!cells.Add(Cell(level)))
            {
                throw Invalid(projectPath, $"Level '{level.Identifier}' is where another level is.");
            }
        }
        int left = cells.Min(cell => cell.X);
        int bottom = cells.Max(cell => cell.Y);

        var tiles = new TileGrid(
            (cells.Max(cell => cell.X) - left + 1) * LevelWidth,
            (bottom - cells.Min(cell => cell.Y) + 1) * LevelHeight);
        for (int y = 0; y < tiles.Height; y++)
        {
            for (int x = 0; x < tiles.Width; x++)
            {
                tiles.SetSolid(x, y, true);
            }
        }

        string directory = projectPath[..(projectPath.LastIndexOfAny(['/', '\\']) + 1)];
        var starts = new List<(EntityKind, Vector2)>();
        foreach (LdtkLevel level in project.Levels)
        {
            (int x, int y) = Cell(level);
            var origin = new Vector2I((x - left) * LevelWidth, (bottom - y) * LevelHeight);
            if (level.ExternalRelPath is null)
            {
                AddLevel(projectPath, level, origin, tiles, starts);
            }
            else
            {
                string levelPath = directory + level.ExternalRelPath;
                LdtkLevel external = Parse(levelPath, readFile(levelPath), LdtkJsonContext.Default.LdtkLevel);
                AddLevel(levelPath, external, origin, tiles, starts);
            }
        }

        try
        {
            return new Room(Path.GetFileNameWithoutExtension(projectPath), tiles, starts);
        }
        catch (ArgumentException e)
        {
            throw Invalid(projectPath, e.Message, e);
        }
    }

    private static (int X, int Y) Cell(LdtkLevel level) => (level.WorldX / level.PxWid, level.WorldY / level.PxHei);

    /// <summary>
    /// Copies a level's tiles into <paramref name="tiles"/>, and its entities' starts into <paramref name="starts"/>,
    /// with the level's bottom left tile at <paramref name="origin"/>.
    /// </summary>
    private static void AddLevel(
        string path, LdtkLevel level, Vector2I origin, TileGrid tiles, List<(EntityKind, Vector2)> starts)
    {
        LdtkLayerInstance[] layers = level.LayerInstances
            ?? throw Invalid(path, $"Level '{level.Identifier}' has no layers.");
        foreach (LdtkLayerInstance layer in layers)
        {
            if (layer.GridSize != World.TilePixels
                || layer.CWid != LevelWidth
                || layer.CHei != LevelHeight
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

        if (solid.IntGridCsv.Length != LevelWidth * LevelHeight)
        {
            throw Invalid(path, $"In level '{level.Identifier}', {solid.Identifier} has {solid.IntGridCsv.Length} "
                + $"values for {LevelWidth * LevelHeight} cells.");
        }
        for (int row = 0; row < LevelHeight; row++)
        {
            for (int x = 0; x < LevelWidth; x++)
            {
                tiles.SetSolid(origin.X + x, origin.Y + LevelHeight - 1 - row, solid.IntGridCsv[row * LevelWidth + x] != 0);
            }
        }

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
            starts.Add((kind, origin + new Vector2(
                (left + entity.Width / 2f) / World.TilePixels,
                LevelHeight - bottom / World.TilePixels)));
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
