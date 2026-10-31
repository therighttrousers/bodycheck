using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

using BodyCheck.Core.WorldData;

using Godot;

namespace BodyCheck.Core.Levels;

/// <summary>
/// Reads the worlds of an LDtk project with LDtk's multi-worlds on, each into a world with one room per level, and
/// checks everything it reads. A level may be any whole number of tiles in size, at any whole number of tiles from the
/// others, as long as no two overlap. Everything outside a world's rooms is empty. A level has three layers:
/// <c>SolidGeometry</c>, an IntGrid in which every value but 0 is a solid tile, <c>Characters</c>, of entities with
/// bodies, and <c>ControlEntities</c>, of entities without, such as respawns. Each entity keeps the place, size and
/// colour of its box in LDtk, which must be whole tiles.
/// </summary>
public static class LdtkReader
{
    /// <summary>The LDtk versions whose JSON this reads: 1.5 and its patches.</summary>
    private const string JsonVersionPrefix = "1.5.";

    /// <summary>
    /// Reads each world of the project at <paramref name="projectPath"/>, in order, into a world named after it, with a
    /// room for each level, named after the level. A world's levels are moved together so that the leftmost edge of any
    /// of them is at the origin's x, and the lowest edge at its y.
    /// </summary>
    /// <param name="readFile">
    /// Reads a file's text, given its path. A level's file is found from its path relative to the project's directory.
    /// </param>
    /// <exception cref="InvalidDataException">When a file isn't what this reads, naming the file and what is wrong.</exception>
    public static IReadOnlyList<World> ReadWorlds(string projectPath, Func<string, string> readFile)
    {
        LdtkProject project = Parse(projectPath, readFile(projectPath), LdtkJsonContext.Default.LdtkProject);
        if (!project.JsonVersion.StartsWith(JsonVersionPrefix, StringComparison.Ordinal))
        {
            throw Invalid(projectPath, $"JSON version {project.JsonVersion} isn't {JsonVersionPrefix}x.");
        }
        if (project.Worlds.Length == 0)
        {
            throw Invalid(projectPath, "The project doesn't have multi-worlds on.");
        }

        string directory = projectPath[..(projectPath.LastIndexOfAny(['/', '\\']) + 1)];
        return [.. project.Worlds.Select(world => ReadWorld(projectPath, directory, world, readFile))];
    }

    private static World ReadWorld(string projectPath, string directory, LdtkWorld world, Func<string, string> readFile)
    {
        string where = $"In world '{world.Identifier}',";
        if (world.Levels.Length == 0)
        {
            throw Invalid(projectPath, $"World '{world.Identifier}' has no levels.");
        }

        foreach (LdtkLevel level in world.Levels)
        {
            if (level.PxWid <= 0
                || level.PxHei <= 0
                || level.PxWid % Constants.TilePixels != 0
                || level.PxHei % Constants.TilePixels != 0
                || level.WorldX % Constants.TilePixels != 0
                || level.WorldY % Constants.TilePixels != 0
                || level.WorldDepth != 0)
            {
                throw Invalid(projectPath, $"{where} level '{level.Identifier}' isn't a whole number of "
                    + $"{Constants.TilePixels}-pixel tiles in size and from the origin, at depth 0.");
            }
        }
        // LDtk's y is down, from the top of the world, and the world's is up, from the bottom.
        int left = world.Levels.Min(level => level.WorldX);
        int bottom = world.Levels.Max(level => level.WorldY + level.PxHei);

        var rooms = new List<Room>();
        foreach (LdtkLevel level in world.Levels)
        {
            var origin = new Vector2I(
                (level.WorldX - left) / Constants.TilePixels,
                (bottom - level.WorldY - level.PxHei) / Constants.TilePixels);
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
            return new World(world.Identifier, rooms);
        }
        catch (ArgumentException e)
        {
            throw Invalid(projectPath, e.Message, e);
        }
    }

    /// <summary>Reads a level into a room, named after the level, with its bottom left tile at <paramref name="origin"/>.</summary>
    private static Room ReadLevel(string path, LdtkLevel level, Vector2I origin)
    {
        LdtkLayerInstance[] layers = level.LayerInstances
            ?? throw Invalid(path, $"Level '{level.Identifier}' has no layers.");
        int width = level.PxWid / Constants.TilePixels;
        int height = level.PxHei / Constants.TilePixels;
        foreach (LdtkLayerInstance layer in layers)
        {
            if (layer.GridSize != Constants.TilePixels
                || layer.CWid != width
                || layer.CHei != height
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
        LdtkLayerInstance controlEntities = Layer(LdtkLayerName.ControlEntities, LdtkLayerType.Entities);

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

        var entityConfigs = new List<EntityConfig>();
        foreach (LdtkEntityInstance entity in characters.EntityInstances.Concat(controlEntities.EntityInstances))
        {
            if (entity.Px.Length != 2 || entity.Pivot.Length != 2)
            {
                throw Invalid(path, $"In level '{level.Identifier}', a {entity.Identifier} has a position or a pivot "
                    + "that isn't two numbers.");
            }

            // LDtk's y is down, from the top of the level, and the room's is up, from the bottom.
            int leftPx = entity.Px[0] - (int)(entity.Pivot[0] * entity.Width);
            int bottomPx = entity.Px[1] + (int)((1 - entity.Pivot[1]) * entity.Height);
            if (leftPx % Constants.TilePixels != 0
                || bottomPx % Constants.TilePixels != 0
                || entity.Width % Constants.TilePixels != 0
                || entity.Height % Constants.TilePixels != 0)
            {
                throw Invalid(path, $"In level '{level.Identifier}', a {entity.Identifier}'s box isn't a whole number "
                    + $"of {Constants.TilePixels}-pixel tiles in size and from the level's corner.");
            }
            var start = new Vector2I(leftPx / Constants.TilePixels, height - bottomPx / Constants.TilePixels);
            var size = new Vector2I(entity.Width, entity.Height) / Constants.TilePixels;
            entityConfigs.Add(entity.Identifier switch
            {
                LdtkEntityName.PlayerStart => new EntityConfig.Player()
                {
                    MapOrigin = start,
                    MapSize = size,
                    MapColor = new Color(entity.SmartColor)
                },
                LdtkEntityName.RobotBasic => new EntityConfig.Robot()
                {
                    MapOrigin = start,
                    MapSize = size,
                    MapColor = new Color(entity.SmartColor)
                },
                LdtkEntityName.Platform => ReadPlatform(path, level, entity, start, size, height),
                LdtkEntityName.Block => new EntityConfig.Block()
                {
                    MapOrigin = start,
                    MapSize = size,
                    MapColor = new Color(entity.SmartColor)
                },
                LdtkEntityName.Respawn => new EntityConfig.Respawn()
                {
                    MapOrigin = start,
                    MapSize = size,
                    MapColor = new Color(entity.SmartColor),
                    Id = entity.Iid
                },
            });
        }

        return new Room(level.Identifier, origin, tiles, entityConfigs);
    }

    /// <summary>
    /// Reads a platform, whose Point fields <c>RangeA</c> and <c>RangeB</c> are two different cells that the middle of
    /// its box moves between.
    /// </summary>
    /// <param name="height">The level's height, in tiles.</param>
    private static EntityConfig.Platform ReadPlatform(
        string path, LdtkLevel level, LdtkEntityInstance entity, Vector2I mapOrigin, Vector2I mapSize, int height)
    {
        if (mapSize.X <= 0)
        {
            throw Invalid(path, $"In level '{level.Identifier}', a {entity.Identifier}'s width is not positive.");
        }
        if (mapSize.Y != 1)
        {
            throw Invalid(path, $"In level '{level.Identifier}', a {entity.Identifier}'s height is not 1 tile.");
        }
        Vector2I rangeA = ReadPoint(path, level, entity, "RangeA", height);
        Vector2I rangeB = ReadPoint(path, level, entity, "RangeB", height);
        if (rangeA == rangeB)
        {
            throw Invalid(path, $"In level '{level.Identifier}', a {entity.Identifier}'s RangeA and RangeB are the "
                + "same cell.");
        }
        return new EntityConfig.Platform()
        {
            MapOrigin = mapOrigin,
            MapSize = mapSize,
            MapColor = new Color(entity.SmartColor),
            RangeA = rangeA,
            RangeB = rangeB,
        };
    }

    /// <summary>Reads an entity's Point field as its cell, in the room's coordinates.</summary>
    /// <param name="height">The level's height, in tiles.</param>
    private static Vector2I ReadPoint(string path, LdtkLevel level, LdtkEntityInstance entity, string field, int height)
    {
        JsonElement? value = entity.FieldInstances.FirstOrDefault(instance => instance.Identifier == field)?.Value;
        if (value is not { ValueKind: JsonValueKind.Object })
        {
            throw Invalid(path, $"In level '{level.Identifier}', a {entity.Identifier} has no point {field}.");
        }

        LdtkPoint point;
        try
        {
            point = value.Value.Deserialize(LdtkJsonContext.Default.LdtkPoint)!;
        }
        catch (JsonException e)
        {
            throw Invalid(path, $"In level '{level.Identifier}', a {entity.Identifier}'s {field} isn't a point.", e);
        }
        // LDtk's y is down, from the top of the level, and the room's is up, from the bottom.
        return new Vector2I(point.Cx, height - 1 - point.Cy);
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
