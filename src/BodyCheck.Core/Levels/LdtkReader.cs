using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

using BodyCheck.Core.WorldData;

using Godot;

namespace BodyCheck.Core.Levels;

/// <summary>
/// Reads the worlds of an LDtk project with LDtk's multi-worlds on, each into a world with one room per level, and
/// checks everything it reads. A level may be any whole number of tiles in size, at any whole number of tiles from the
/// others, as long as no two overlap. Everything outside a world's rooms is empty. A level has three layers:
/// <c>SolidGeometry</c>, an IntGrid in which every value but 0 is a solid tile, and <c>Entities</c> and <c>Control</c>,
/// whose entities are read alike: the layers only help the editor show them. Each entity keeps its <c>iid</c>, and the
/// place, size and colour of its box in LDtk, which must be whole tiles. Routes name their targets by <c>iid</c>.
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
        // LDtk's y is down, from the top of the world, and the world's is up, from the bottom. A world with no levels
        // is left for World to reject.
        int left = world.Levels.Select(level => level.WorldX).DefaultIfEmpty().Min();
        int bottom = world.Levels.Select(level => level.WorldY + level.PxHei).DefaultIfEmpty().Max();

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
        LdtkLayerInstance entities = Layer(LdtkLayerName.Entities, LdtkLayerType.Entities);
        LdtkLayerInstance control = Layer(LdtkLayerName.Control, LdtkLayerType.Entities);

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

        EntityConfig[] entityConfigs =
        [
            .. entities.EntityInstances.Concat(control.EntityInstances)
                .Select(entity => ReadEntity(path, level, entity, height))
        ];
        try
        {
            return new Room(level.Identifier, origin, tiles, entityConfigs);
        }
        catch (ArgumentException e)
        {
            throw Invalid(path, e.Message, e);
        }
    }

    /// <summary>Reads an entity into its configuration, in the room's coordinates.</summary>
    /// <param name="height">The level's height, in tiles.</param>
    private static EntityConfig ReadEntity(string path, LdtkLevel level, LdtkEntityInstance entity, int height)
    {
        var fields = new Fields(path, level, entity);
        if (entity.Px.Length != 2 || entity.Pivot.Length != 2)
        {
            throw fields.Invalid("has a position or a pivot that isn't two numbers.");
        }

        // LDtk's y is down, from the top of the level, and the room's is up, from the bottom.
        int leftPx = entity.Px[0] - (int)(entity.Pivot[0] * entity.Width);
        int bottomPx = entity.Px[1] + (int)((1 - entity.Pivot[1]) * entity.Height);
        if (leftPx % Constants.TilePixels != 0
            || bottomPx % Constants.TilePixels != 0
            || entity.Width % Constants.TilePixels != 0
            || entity.Height % Constants.TilePixels != 0)
        {
            throw fields.Invalid($"has a box that isn't a whole number of {Constants.TilePixels}-pixel tiles in size "
                + "and from the level's corner.");
        }
        var origin = new Vector2I(leftPx / Constants.TilePixels, height - bottomPx / Constants.TilePixels);
        var size = new Vector2I(entity.Width, entity.Height) / Constants.TilePixels;
        var color = new Color(entity.SmartColor);
        string id = entity.Iid;

        return entity.Identifier switch
        {
            LdtkEntityName.PlayerStart => new EntityConfig.Player()
            {
                Id = id,
                MapOrigin = origin,
                MapSize = size,
                MapColor = color,
            },
            LdtkEntityName.RobotBasic => new EntityConfig.Robot()
            {
                Id = id,
                MapOrigin = origin,
                MapSize = size,
                MapColor = color,
            },
            LdtkEntityName.Platform => new EntityConfig.Platform()
            {
                Id = id,
                MapOrigin = origin,
                MapSize = size,
                MapColor = color,
                RangeA = fields.Point("RangeA", height),
                RangeB = fields.Point("RangeB", height),
            },
            LdtkEntityName.Block => new EntityConfig.Block()
            {
                Id = id,
                MapOrigin = origin,
                MapSize = size,
                MapColor = color,
            },
            LdtkEntityName.VerticalDoor => new EntityConfig.VerticalDoor()
            {
                Id = id,
                MapOrigin = origin,
                MapSize = size,
                MapColor = color,
            },
            LdtkEntityName.Respawn => new EntityConfig.Respawn()
            {
                Id = id,
                MapOrigin = origin,
                MapSize = size,
                MapColor = color,
            },
            LdtkEntityName.Trigger => new EntityConfig.Trigger()
            {
                Id = id,
                MapOrigin = origin,
                MapSize = size,
                MapColor = color,
                Step = new Step(
                    fields.Read("Target", LdtkJsonContext.Default.LdtkEntityRef).EntityIid,
                    fields.Read("Action", LdtkJsonContext.Default.TriggerAction)),
                Once = fields.Read("Once", LdtkJsonContext.Default.Boolean),
            },
            LdtkEntityName.Sequence => new EntityConfig.Sequence()
            {
                Id = id,
                MapOrigin = origin,
                MapSize = size,
                MapColor = color,
                Steps = fields.Steps(),
                Once = fields.Read("Once", LdtkJsonContext.Default.Boolean),
            },
            LdtkEntityName.Fanout => new EntityConfig.Fanout()
            {
                Id = id,
                MapOrigin = origin,
                MapSize = size,
                MapColor = color,
                Steps = fields.Steps(),
                Once = fields.Read("Once", LdtkJsonContext.Default.Boolean),
            },
            LdtkEntityName.Dialogue => new EntityConfig.Dialogue()
            {
                Id = id,
                MapOrigin = origin,
                MapSize = size,
                MapColor = color,
                File = fields.Read("DialogueFile", LdtkJsonContext.Default.String),
                Title = fields.Read("DialogueTitle", LdtkJsonContext.Default.String),
                Garble = fields.Read("Garble", LdtkJsonContext.Default.Single),
            },
            LdtkEntityName.Script => throw fields.Invalid("isn't supported yet."),
        };
    }

    /// <summary>Reads an entity's fields, each by its name and as its type, and makes errors that name the entity.</summary>
    private sealed class Fields(string path, LdtkLevel level, LdtkEntityInstance entity)
    {
        /// <summary>An error about the entity, given the end of a sentence about it.</summary>
        public InvalidDataException Invalid(string problem, Exception? inner = null) =>
            LdtkReader.Invalid(path, $"In level '{level.Identifier}', a {entity.Identifier} {problem}", inner);

        /// <summary>Reads the field named <paramref name="field"/>, which must have a value, of the given type.</summary>
        public T Read<T>(string field, JsonTypeInfo<T> type)
        {
            LdtkFieldInstance instance = entity.FieldInstances.FirstOrDefault(instance => instance.Identifier == field)
                ?? throw Invalid($"has no field {field}.");
            try
            {
                return instance.Value.Deserialize(type) ?? throw Invalid($"has no value for {field}.");
            }
            catch (JsonException e)
            {
                throw Invalid($"has a {field} that isn't a {typeof(T).Name}: {e.Message}", e);
            }
        }

        /// <summary>Reads a Point field as its cell, in the room's coordinates.</summary>
        /// <param name="height">The level's height, in tiles.</param>
        public Vector2I Point(string field, int height)
        {
            LdtkPoint point = Read(field, LdtkJsonContext.Default.LdtkPoint);
            // LDtk's y is down, from the top of the level, and the room's is up, from the bottom.
            return new Vector2I(point.Cx, height - 1 - point.Cy);
        }

        /// <summary>Reads the arrays <c>Targets</c> and <c>Actions</c>, which must be the same length, as steps.</summary>
        public IReadOnlyList<Step> Steps()
        {
            LdtkEntityRef?[] targets = Read("Targets", LdtkJsonContext.Default.LdtkEntityRefArray);
            TriggerAction[] actions = Read("Actions", LdtkJsonContext.Default.TriggerActionArray);
            if (targets.Length != actions.Length)
            {
                throw Invalid($"has {targets.Length} Targets and {actions.Length} Actions, not the same number.");
            }
            return
            [
                .. targets.Zip(actions, (target, action) =>
                    new Step(target?.EntityIid ?? throw Invalid("has a Target with no value."), action))
            ];
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
