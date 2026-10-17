using Godot;

namespace BodyCheck.Core.Rooms;

/// <summary>
/// A named room: its tiles, and the configuration of each of its entities. Rooms come from text (<see cref="FromText"/>)
/// and from LDtk levels.
/// </summary>
public sealed class Room
{
    private readonly TileGrid _tiles;

    /// <param name="tiles">The room's tiles. The room keeps it, so the caller must not change it afterwards.</param>
    /// <param name="entityConfigs">
    /// Each entity's configuration, including where the middle of its base starts. There must be one player.
    /// </param>
    public Room(string name, TileGrid tiles, IEnumerable<EntityConfig> entityConfigs)
    {
        Name = name;
        _tiles = tiles;

        EntityConfigs = [.. entityConfigs];

        int numPlayerStarts = EntityConfigs.Count(c => c.Kind == EntityKind.Player);
        if (numPlayerStarts != 1)
        {
            throw new ArgumentException($"Room '{name}' has {numPlayerStarts} player starts, not 1.", nameof(entityConfigs));
        }
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
    public static Room FromText(string name, string text)
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
                    'P' => new EntityConfig.Player(start),
                    'R' => new EntityConfig.Robot(start),
                    'H' => new EntityConfig.Platform(start),
                    'B' => new EntityConfig.Block(start),
                    _ => null,
                };
                if (config is not null)
                {
                    entityConfigs.Add(config);
                }
            }
        }

        return new Room(name, TileGrid.FromText(rows), entityConfigs);
    }

    public string Name { get; }

    /// <summary>
    /// Each entity's configuration, including where the middle of its base starts, in the order the room was
    /// given them.
    /// </summary>
    public IReadOnlyList<EntityConfig> EntityConfigs { get; }

    /// <summary>Builds a fresh grid, so that one world's changes to its tiles don't reach another's.</summary>
    public TileGrid CreateTiles() => _tiles.Clone();
}
