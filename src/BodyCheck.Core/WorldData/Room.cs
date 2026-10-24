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
    /// Each entity's configuration, including where the middle of its base starts, in the room's coordinates.
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
    /// <item>'H' is where a horizontal platform starts</item>
    /// <item>'B' is where a block starts</item>
    /// </list>
    /// An entity starts with the middle of its base at the middle of the bottom edge of its tile.
    /// </summary>
    public static Room FromText(string name, string text, Vector2I origin = default)
    {
        string[] rows = text.ReplaceLineEndings("\n").Split('\n');

        var entityConfigs = new List<EntityConfig>();
        for (int row = 0; row < rows.Length; row++)
        {
            for (int x = 0; x < rows[row].Length; x++)
            {
                Vector2 start = new(x + 0.5f, rows.Length - 1 - row);
                EntityConfig? config = rows[row][x] switch
                {
                    // These colors and sizes should match the colors and sizes of entities in LDtk.
                    'P' => new EntityConfig.Player(start, new Vector2(0.5f, 1), new Color("#2CE8F5")),
                    'R' => new EntityConfig.Robot(start, new Vector2(0.5f, 1), new Color("#FEAE34")),
                    'H' => new EntityConfig.Platform(start, new Vector2(1.5f, 0.5f), new Color("#63C74D")),
                    'B' => new EntityConfig.Block(start, new Vector2(0.5f, 0.5f), new Color("#BE4A2F")),
                    'S' => new EntityConfig.Respawn(start, new Vector2(0.5f, 1), new Color("#4399D7")),
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
    /// Each entity's configuration, including where the middle of its base starts, in the room's coordinates and in
    /// the order the room was given them.
    /// </summary>
    public IReadOnlyList<EntityConfig> EntityConfigs { get; }
}
