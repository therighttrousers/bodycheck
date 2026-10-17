using Godot;

namespace BodyCheck.Core;

/// <summary>The kinds of entity that a room says where to start.</summary>
public enum EntityKind
{
    Player,
    Robot,
    Platform,
    Block,
}

/// <summary>
/// A named room: its tiles, and where each of its entities starts. Rooms come from text (<see cref="FromText"/>)
/// and from LDtk levels (<see cref="LdtkReader"/>).
/// </summary>
public sealed class Room
{
    private readonly TileGrid _tiles;

    /// <param name="tiles">The room's tiles. The room keeps it, so the caller must not change it afterwards.</param>
    /// <param name="starts">Each entity's kind, and where the middle of its base starts. There must be one player.</param>
    public Room(string name, TileGrid tiles, IEnumerable<(EntityKind Kind, Vector2 Start)> starts)
    {
        Name = name;
        _tiles = tiles;

        var playerStarts = new List<Vector2>();
        var robotStarts = new List<Vector2>();
        var platformStarts = new List<Vector2>();
        var blockStarts = new List<Vector2>();
        foreach ((EntityKind kind, Vector2 start) in starts)
        {
            List<Vector2> list = kind switch
            {
                EntityKind.Player => playerStarts,
                EntityKind.Robot => robotStarts,
                EntityKind.Platform => platformStarts,
                EntityKind.Block => blockStarts,
                _ => throw new ArgumentOutOfRangeException(nameof(starts), kind, null),
            };
            list.Add(start);
        }

        if (playerStarts.Count != 1)
        {
            throw new ArgumentException($"Room '{name}' has {playerStarts.Count} player starts, not 1.", nameof(starts));
        }
        PlayerStart = playerStarts[0];
        RobotStarts = robotStarts;
        PlatformStarts = platformStarts;
        BlockStarts = blockStarts;
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

        var starts = new List<(EntityKind, Vector2)>();
        for (int row = 0; row < rows.Length; row++)
        {
            for (int x = 0; x < rows[row].Length; x++)
            {
                EntityKind? kind = rows[row][x] switch
                {
                    'P' => EntityKind.Player,
                    'R' => EntityKind.Robot,
                    'H' => EntityKind.Platform,
                    'B' => EntityKind.Block,
                    _ => null,
                };
                if (kind is not null)
                {
                    starts.Add((kind.Value, new Vector2(x + 0.5f, rows.Length - 1 - row)));
                }
            }
        }

        return new Room(name, TileGrid.FromText(rows), starts);
    }

    public string Name { get; }

    /// <summary>Where the player's feet start.</summary>
    public Vector2 PlayerStart { get; }

    /// <summary>Where each robot's feet start.</summary>
    public IReadOnlyList<Vector2> RobotStarts { get; }

    /// <summary>Where each platform's base starts.</summary>
    public IReadOnlyList<Vector2> PlatformStarts { get; }

    /// <summary>Where each block's base starts.</summary>
    public IReadOnlyList<Vector2> BlockStarts { get; }

    /// <summary>Builds a fresh grid, so that one world's changes to its tiles don't reach another's.</summary>
    public TileGrid CreateTiles() => _tiles.Clone();
}
