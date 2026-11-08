using Godot;

namespace BodyCheck.Core.WorldData;

/// <summary>
/// A named room, one part of a <see cref="World"/>: where it is, its tiles, and the configuration of each entity that
/// starts in it. Its tiles and its entities' starts are in its own coordinates, with its bottom left tile at (0, 0).
/// Rooms come from text (<see cref="FromText"/>) and from LDtk levels.
/// </summary>
public sealed class Room : IOffsetTileGrid
{
    private readonly TileGrid _tiles;

    /// <param name="origin">Where the room's bottom left tile is in the world, in tiles.</param>
    /// <param name="tiles">The room's tiles. The room keeps it, so the caller must not change it afterwards.</param>
    /// <param name="entityConfigs">
    /// Each entity's configuration, including where its box is, in the room's coordinates. Every box has a positive
    /// size, but may be outside the room. A platform is one tile high, with two different ends; a door's stops are in its
    /// columns, its open stop above it and its close stop below it, and it doesn't touch both; and a sequence and a
    /// fanout have at least one step; and a trigger set off at the start starts on.
    /// </param>
    /// <exception cref="ArgumentException">When a configuration isn't what the room can hold.</exception>
    public Room(string name, Vector2I origin, TileGrid tiles, IEnumerable<EntityConfig> entityConfigs)
    {
        Name = name;
        _tiles = tiles;
        Origin = origin;
        Size = tiles.Size;
        EntityConfigs = [.. entityConfigs];
        foreach (EntityConfig config in EntityConfigs)
        {
            if (Problem(config) is string problem)
            {
                throw new ArgumentException($"In room '{name}', a {config.Kind} {problem}.", nameof(entityConfigs));
            }
        }
    }

    /// <summary>What is wrong with a configuration, as the end of a sentence about it, or null if nothing is.</summary>
    private static string? Problem(EntityConfig config) => config switch
    {
        { MapSize.X: <= 0 } or { MapSize.Y: <= 0 } => "has a box that isn't of a positive size",
        EntityConfig.Platform { MapSize.Y: not 1 } => "isn't 1 tile high",
        EntityConfig.Platform platform when platform.RangeA == platform.RangeB => "has its two ends in the same cell",
        EntityConfig.VerticalDoor door => DoorProblem(door),
        EntityConfig.Sequence { Steps.Count: 0 } or EntityConfig.Fanout { Steps.Count: 0 } => "has no steps",
        // Never set off, since it is set off only at the start, if it is on then.
        EntityConfig.Trigger { SetOffBy: TriggerSetOffBy.Start, StartsOn: false } => "set off at the start starts off",
        _ => null,
    };

    /// <summary>What is wrong with a door's stops, as <see cref="Problem"/> says it, or null if nothing is.</summary>
    private static string? DoorProblem(EntityConfig.VerticalDoor door)
    {
        int left = door.MapOrigin.X;
        int right = door.MapOrigin.X + door.MapSize.X - 1;
        int bottom = door.MapOrigin.Y;
        int top = door.MapOrigin.Y + door.MapSize.Y - 1;
        if (door.OpenStop.X < left || door.OpenStop.X > right)
        {
            return "has its open stop outside its columns";
        }
        if (door.CloseStop.X < left || door.CloseStop.X > right)
        {
            return "has its close stop outside its columns";
        }
        if (door.OpenStop.Y <= top)
        {
            return "has its open stop not above it";
        }
        if (door.CloseStop.Y >= bottom)
        {
            return "has its close stop not below it";
        }
        if (door.OpenStop.Y == top + 1 && door.CloseStop.Y == bottom - 1)
        {
            return "touches both of its stops, so it can't move";
        }
        return null;
    }

    /// <summary>
    /// A room described as text, top row first:
    /// <list type="bullet">
    /// <item>'#' is a ground tile</item>
    /// <item>'P' is where the player starts</item>
    /// <item>'R' is where a robot starts</item>
    /// <item>'H' is where a platform starts, 3 tiles wide, which moves between the walls on either side of it</item>
    /// <item>'B' is where a block starts</item>
    /// <item>'S' is a respawn</item>
    /// </list>
    /// An entity's box has its bottom left tile at its letter, except a platform's, which has its middle tile there. An
    /// entity's id is the room's name and its letter's column and row, from the top left.
    /// </summary>
    public static Room FromText(string name, string text, Vector2I origin = default)
    {
        string[] rows = text.ReplaceLineEndings("\n").Split('\n');

        var entityConfigs = new List<EntityConfig>();
        for (int row = 0; row < rows.Length; row++)
        {
            for (int x = 0; x < rows[row].Length; x++)
            {
                Vector2I mapOrigin = new(x, rows.Length - 1 - row);
                string id = $"{name} {x},{row}";
                EntityConfig? config = rows[row][x] switch
                {
                    // These colors and sizes should match the colors and sizes of entities in LDtk.
                    'P' => new EntityConfig.Player()
                    {
                        MapOrigin = mapOrigin,
                        MapSize = new Vector2I(1, 2),
                        MapColor = new Color("#2CE8F5"),
                        Id = id,
                    },
                    'R' => new EntityConfig.Robot()
                    {
                        MapOrigin = mapOrigin,
                        MapSize = new Vector2I(1, 2),
                        MapColor = new Color("#FEAE34"),
                        Id = id,
                    },
                    'H' => PlatformBetweenWalls(rows[row], x, mapOrigin, id),
                    'B' => new EntityConfig.Block()
                    {
                        MapOrigin = mapOrigin,
                        MapSize = new Vector2I(1, 1),
                        MapColor = new Color("#BE4A2F"),
                        Id = id,
                    },
                    'S' => new EntityConfig.Respawn()
                    {
                        MapOrigin = mapOrigin,
                        MapSize = new Vector2I(1, 2),
                        MapColor = new Color("#4399D7"),
                        Id = id,
                    },
                    _ => null,
                };
                if (config is not null)
                {
                    entityConfigs.Add(config);
                }
            }
        }

        return new Room(name, origin, TileGrid.FromText(rows), entityConfigs);
    }

    /// <summary>
    /// A platform whose box is centered on column <paramref name="x"/> of <paramref name="row"/>, and which moves
    /// between the nearest walls on either side of it in that row, or the room's edges.
    /// </summary>
    private static EntityConfig.Platform PlatformBetweenWalls(string row, int x, Vector2I middleTile, string id)
    {
        var mapSize = new Vector2I(3, 1);
        int leftWall = row.LastIndexOf('#', x);
        int rightWall = row.IndexOf('#', x) is int found and >= 0 ? found : row.Length;
        return new EntityConfig.Platform()
        {
            MapOrigin = middleTile - new Vector2I(mapSize.X / 2, 0),
            MapSize = mapSize,
            MapColor = new Color("#63C74D"),
            Id = id,
            RangeA = new Vector2I(leftWall + 2, middleTile.Y),
            RangeB = new Vector2I(rightWall - 2, middleTile.Y),
        };
    }

    public string Name { get; }

    /// <summary>Where the room's bottom left tile is in the world, in tiles.</summary>
    public Vector2I Origin { get; }

    /// <summary>The room's size, in tiles.</summary>
    public Vector2I Size { get; }

    /// <summary>The tiles the room covers, in the world's coordinates.</summary>
    public Rect2I Bounds => new(Origin, Size);

    /// <summary>A tile's kind, in the room's coordinates. Everything outside the room is empty.</summary>
    public TileIndex GetTileIndex(int x, int y) => _tiles.GetTileIndex(x, y);

    /// <summary>
    /// Each entity's configuration, including where its box is, in the room's coordinates and in the order the room was
    /// given them.
    /// </summary>
    public IReadOnlyList<EntityConfig> EntityConfigs { get; }
}
