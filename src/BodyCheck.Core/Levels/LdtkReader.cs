using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

using BodyCheck.Core.WorldData;

using Godot;

namespace BodyCheck.Core.Levels;

/// <summary>
/// Reads the levels of an LDtk project into a world, one room per level, and checks everything it reads. Levels are
/// 20x15 tiles, on a grid of that size; grid cells with no level are solid, as everything outside a world's rooms is. A
/// level has two layers:
/// <c>SolidGeometry</c>, an IntGrid in which every value but 0 is a solid tile, and <c>Characters</c>, whose entities
/// start with the middle of their base at the middle of the bottom edge of their box in LDtk.
/// </summary>
public static class LdtkReader
{
    /// <summary>The LDtk versions whose JSON this reads: 1.5 and its patches.</summary>
    private const string JsonVersionPrefix = "1.5.";

    /// <summary>
    /// Reads every level of the project at <paramref name="projectPath"/> into a world, named after the project's file,
    /// with a room for each level, named after the level. The levels are moved so that the leftmost and lowest are at
    /// the world's origin.
    /// </summary>
    /// <param name="readFile">
    /// Reads a file's text, given its path. A level's file is found from its path relative to the project's directory.
    /// </param>
    /// <exception cref="InvalidDataException">When a file isn't what this reads, naming the file and what is wrong.</exception>
    public static World ReadWorld(string projectPath, Func<string, string> readFile)
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
            if (level.PxWid != Constants.LevelWidth * Constants.TilePixels
                || level.PxHei != Constants.LevelHeight * Constants.TilePixels
                || level.WorldX % level.PxWid != 0
                || level.WorldY % level.PxHei != 0
                || level.WorldDepth != 0)
            {
                throw Invalid(projectPath, $"Level '{level.Identifier}' isn't {Constants.LevelWidth}x{Constants.LevelHeight} tiles, on "
                    + "a grid of that size, at depth 0.");
            }
            if (!cells.Add(Cell(level)))
            {
                throw Invalid(projectPath, $"Level '{level.Identifier}' is where another level is.");
            }
        }
        int left = cells.Min(cell => cell.X);
        int bottom = cells.Max(cell => cell.Y);

        string directory = projectPath[..(projectPath.LastIndexOfAny(['/', '\\']) + 1)];
        var rooms = new List<Room>();
        foreach (LdtkLevel level in project.Levels)
        {
            (int x, int y) = Cell(level);
            var origin = new Vector2I((x - left) * Constants.LevelWidth, (bottom - y) * Constants.LevelHeight);
            if (level.ExternalRelPath is null)
            {
                rooms.Add(ReadLevel(projectPath, level, origin));
            }
            else
            {
                string levelPath = directory + level.ExternalRelPath;
                LdtkLevel external = Parse(levelPath, readFile(levelPath), LdtkJsonContext.Default.LdtkLevel);
                rooms.Add(ReadLevel(levelPath, external, origin));
            }
        }

        try
        {
            return new World(Path.GetFileNameWithoutExtension(projectPath), rooms);
        }
        catch (ArgumentException e)
        {
            throw Invalid(projectPath, e.Message, e);
        }
    }

    private static (int X, int Y) Cell(LdtkLevel level) => (level.WorldX / level.PxWid, level.WorldY / level.PxHei);

    /// <summary>Reads a level into a room, named after the level, with its bottom left tile at <paramref name="origin"/>.</summary>
    private static Room ReadLevel(string path, LdtkLevel level, Vector2I origin)
    {
        LdtkLayerInstance[] layers = level.LayerInstances
            ?? throw Invalid(path, $"Level '{level.Identifier}' has no layers.");
        foreach (LdtkLayerInstance layer in layers)
        {
            if (layer.GridSize != Constants.TilePixels
                || layer.CWid != Constants.LevelWidth
                || layer.CHei != Constants.LevelHeight
                || layer.PxTotalOffsetX != 0
                || layer.PxTotalOffsetY != 0)
            {
                throw Invalid(path, $"In level '{level.Identifier}', layer {layer.Identifier} isn't a grid of "
                    + $"{Constants.TilePixels}-pixel cells that covers the level exactly.");
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

        if (solid.IntGridCsv.Length != Constants.LevelWidth * Constants.LevelHeight)
        {
            throw Invalid(path, $"In level '{level.Identifier}', {solid.Identifier} has {solid.IntGridCsv.Length} "
                + $"values for {Constants.LevelWidth * Constants.LevelHeight} cells.");
        }
        var tiles = new TileGrid(Constants.LevelWidth, Constants.LevelHeight);
        for (int row = 0; row < Constants.LevelHeight; row++)
        {
            for (int x = 0; x < Constants.LevelWidth; x++)
            {
                tiles.SetSolid(x, Constants.LevelHeight - 1 - row, solid.IntGridCsv[row * Constants.LevelWidth + x] != 0);
            }
        }

        var entityConfigs = new List<EntityConfig>();
        foreach (LdtkEntityInstance entity in characters.EntityInstances)
        {
            if (entity.Px.Length != 2 || entity.Pivot.Length != 2)
            {
                throw Invalid(path, $"In level '{level.Identifier}', a {entity.Identifier} has a position or a pivot "
                    + "that isn't two numbers.");
            }

            // LDtk's y is down, from the top of the level, and the room's is up, from the bottom.
            float left = entity.Px[0] - entity.Pivot[0] * entity.Width;
            float bottom = entity.Px[1] + (1 - entity.Pivot[1]) * entity.Height;
            var start = new Vector2(
                (left + entity.Width / 2f) / Constants.TilePixels,
                Constants.LevelHeight - bottom / Constants.TilePixels
            );
            entityConfigs.Add(entity.Identifier switch
            {
                LdtkEntityName.PlayerStart => new EntityConfig.Player(start),
                LdtkEntityName.RobotBasic => new EntityConfig.Robot(start),
                LdtkEntityName.PlatformHorizontal => new EntityConfig.Platform(start),
                LdtkEntityName.Block => new EntityConfig.Block(start),
            });
        }

        return new Room(level.Identifier, origin, tiles, entityConfigs);
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
