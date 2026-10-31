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
    /// Each entity's configuration, including where its box is, in the room's coordinates.
    /// </param>
    public Room(string name, Vector2I origin, TileGrid tiles, IEnumerable<EntityConfig> entityConfigs)
    {
        Name = name;
        _tiles = tiles;
        Origin = origin;
        Size = tiles.Size;
        EntityConfigs = [.. entityConfigs];
    }

    /// <summary>
    /// A room described as text, top row first:
    /// <list type="bullet">
    /// <item>'#' is a solid tile</item>
    /// <item>'P' is where the player starts</item>
    /// <item>'R' is where a robot starts</item>
    /// <item>'H' is where a platform starts, 3 tiles wide, which moves between the walls on either side of it</item>
    /// <item>'B' is where a block starts</item>
    /// <item>'S' is a respawn, identified by the room's name and its place in the room</item>
    /// </list>
    /// An entity's box has its bottom left tile at its letter, except a platform's, which has its middle tile there.
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
                EntityConfig? config = rows[row][x] switch
                {
                    // These colors and sizes should match the colors and sizes of entities in LDtk.
                    'P' => new EntityConfig.Player()
                    {
                        MapOrigin = mapOrigin,
                        MapSize = new Vector2I(1, 2),
                        MapColor = new Color("#2CE8F5")
                    },
                    'R' => new EntityConfig.Robot()
                    {
                        MapOrigin = mapOrigin,
                        MapSize = new Vector2I(1, 2),
                        MapColor = new Color("#FEAE34")
                    },
                    'H' => PlatformBetweenWalls(rows[row], x, mapOrigin),
                    'B' => new EntityConfig.Block()
                    {
                        MapOrigin = mapOrigin,
                        MapSize = new Vector2I(1, 1),
                        MapColor = new Color("#BE4A2F")
                    },
                    'S' => new EntityConfig.Respawn()
                    {
                        MapOrigin = mapOrigin,
                        MapSize = new Vector2I(1, 2),
                        MapColor = new Color("#4399D7"),
                        Id = $"{name} {x},{row}"
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
    private static EntityConfig.Platform PlatformBetweenWalls(string row, int x, Vector2I middleTile)
    {
        var mapSize = new Vector2I(3, 1);
        int leftWall = row.LastIndexOf('#', x);
        int rightWall = row.IndexOf('#', x) is int found and >= 0 ? found : row.Length;
        return new EntityConfig.Platform()
        {
            MapOrigin = middleTile - new Vector2I(mapSize.X / 2, 0),
            MapSize = mapSize,
            MapColor = new Color("#63C74D"),
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

    /// <summary>Whether a tile is solid, in the room's coordinates. Everything outside the room is empty.</summary>
    public bool IsSolid(int x, int y) => _tiles.IsSolid(x, y);

    /// <summary>
    /// Each entity's configuration, including where its box is, in the room's coordinates and in the order the room was
    /// given them.
    /// </summary>
    public IReadOnlyList<EntityConfig> EntityConfigs { get; }
}
