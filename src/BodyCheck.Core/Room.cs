using Godot;

namespace BodyCheck.Core;

/// <summary>
/// A named room described as text, top row first:
/// <list type="bullet">
/// <item>'#' is a solid tile</item>
/// <item>'P' is where the player starts</item>
/// <item>'R' is where a robot starts</item>
/// <item>'H' is where a horizontal platform starts</item>
/// </list>
/// </summary>
public sealed class Room
{
    private readonly string[] _rows;

    public Room(string name, string text)
    {
        Name = name;
        _rows = text.ReplaceLineEndings("\n").Split('\n');

        var robotStarts = new List<Vector2>();
        var platformStarts = new List<Vector2>();
        Vector2? playerStart = null;
        for (int row = 0; row < _rows.Length; row++)
        {
            for (int x = 0; x < _rows[row].Length; x++)
            {
                // The middle of the bottom edge of the tile.
                var floorPoint = new Vector2(x + 0.5f, _rows.Length - 1 - row);
                switch (_rows[row][x])
                {
                    case 'P':
                        playerStart = floorPoint;
                        break;
                    case 'R':
                        robotStarts.Add(floorPoint);
                        break;
                    case 'H':
                        platformStarts.Add(floorPoint);
                        break;
                }
            }
        }

        PlayerStart = playerStart ?? throw new ArgumentException($"Room '{name}' has no player start.");
        RobotStarts = robotStarts;
        PlatformStarts = platformStarts;
    }

    public string Name { get; }

    /// <summary>Where the player's feet start.</summary>
    public Vector2 PlayerStart { get; }

    /// <summary>Where each robot's feet start.</summary>
    public IReadOnlyList<Vector2> RobotStarts { get; }

    /// <summary>Where each platform's base starts.</summary>
    public IReadOnlyList<Vector2> PlatformStarts { get; }

    /// <summary>Builds a fresh grid, so that one world's changes to its tiles don't reach another's.</summary>
    public TileGrid CreateTiles() => TileGrid.FromText(_rows);
}
