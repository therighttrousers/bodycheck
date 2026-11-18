using System;
using System.Collections.Generic;
using System.Linq;

using BodyCheck.Core.WorldData;

using Godot;

namespace BodyCheck.Decoration;

/// <summary>The directions in which a <see cref="BackWallGrowth"/> grows, as bits: 0101 is up and down.</summary>
[Flags]
public enum GrowDirections
{
    Up = 1,
    Left = 2,
    Down = 4,
    Right = 8,
}

/// <summary>
/// One part of a <see cref="BackWall"/>: it starts as one kind of tile, then grows a few times, each tile of it into
/// each of its empty neighbours in the given directions by chance, so that it is densest near those tiles. It grows only
/// into empty tiles, so that it doesn't spread behind other kinds, such as grass, and stays inside the room.
/// </summary>
[GlobalClass]
public partial class BackWallGrowth : Resource
{
    /// <summary>The kind of tile it spreads from.</summary>
    [Export] public TileIndex SpreadsFrom { get; set; } = TileIndex.Ground;

    /// <summary>The directions in which it grows.</summary>
    [Export] public GrowDirections Directions { get; set; } = GrowDirections.Up | GrowDirections.Left | GrowDirections.Down | GrowDirections.Right;

    /// <summary>How many times it grows.</summary>
    [Export(PropertyHint.Range, "0,10")] public int Times { get; set; } = 3;

    /// <summary>
    /// The chance that each tile grows into each neighbour, each time. At 1, it has no holes within <see cref="Times"/>
    /// tiles of the tiles it spreads from.
    /// </summary>
    [Export(PropertyHint.Range, "0,1")] public float Chance { get; set; } = 0.4f;

    /// <summary>
    /// The tiles of <paramref name="room"/> it covers, in the room's coordinates. <paramref name="salt"/> tells the
    /// growths of one wall apart, so that each has random numbers of its own.
    /// </summary>
    public HashSet<Vector2I> Grow(Room room, string salt)
    {
        // The room's y is up.
        (GrowDirections Direction, Vector2I Step)[] steps =
            [(GrowDirections.Up, new(0, 1)), (GrowDirections.Left, new(-1, 0)), (GrowDirections.Down, new(0, -1)), (GrowDirections.Right, new(1, 0))];
        var directions = steps.Where(s => Directions.HasFlag(s.Direction)).ToArray();

        var tiles = new HashSet<Vector2I>();
        for (int y = 0; y < room.Size.Y; y++)
        {
            for (int x = 0; x < room.Size.X; x++)
            {
                if (room.GetTileIndex(x, y) == SpreadsFrom)
                {
                    tiles.Add(new Vector2I(x, y));
                }
            }
        }

        for (int i = 0; i < Times; i++)
        {
            foreach (var coord in tiles.ToArray())
            {
                foreach (var (direction, step) in directions)
                {
                    var next = coord + step;
                    if (next.X >= 0 && next.Y >= 0 && next.X < room.Size.X && next.Y < room.Size.Y &&
                        room.GetTileIndex(next.X, next.Y) == TileIndex.Empty &&
                        RoomDecorator.RandomForTile(room, coord, $"{salt}_grow_{direction}") < Chance)
                    {
                        tiles.Add(next);
                    }
                }
            }
        }
        return tiles;
    }
}
