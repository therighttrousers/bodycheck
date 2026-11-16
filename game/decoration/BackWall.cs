using System.Collections.Generic;
using System.Linq;

using BodyCheck.Core.Simulation;
using BodyCheck.Core.WorldData;

using Godot;

namespace BodyCheck.Decoration;

/// <summary>
/// A wall behind a room's tiles, with holes in it, that spreads from one kind of tile: it starts as those tiles, then
/// grows a few times, each tile of it into each of its four neighbours by chance, so that it is densest near them. It
/// grows only into empty tiles, so that it doesn't spread behind other kinds, such as grass, and stays inside the room. It is drawn with a kind of tile's mesh, darkened, each tile at a random depth, turned by a
/// random quarter-turn, as one multimesh.
/// </summary>
[GlobalClass]
public partial class BackWall : RoomDecorator
{
    private static readonly Vector2I[] Directions = [Vector2I.Up, Vector2I.Left, Vector2I.Down, Vector2I.Right];

    /// <summary>The kind of tile the wall spreads from.</summary>
    [Export] public TileIndex SpreadsFrom { get; set; } = TileIndex.Ground;

    /// <summary>The kind of tile whose mesh the wall is drawn with.</summary>
    [Export] public TileIndex DrawnAs { get; set; } = TileIndex.Ground;

    /// <summary>How many times the wall grows.</summary>
    [Export(PropertyHint.Range, "0,10")] public int Growths { get; set; } = 3;

    /// <summary>
    /// The chance that each tile of the wall grows into each of its neighbours, each time it grows. At 1, the wall has
    /// no holes within <see cref="Growths"/> tiles of the tiles it spreads from.
    /// </summary>
    [Export(PropertyHint.Range, "0,1")] public float GrowChance { get; set; } = 0.4f;

    /// <summary>How much darker than its tiles the wall is, from 0 to 1.</summary>
    [Export(PropertyHint.Range, "0,1")] public float Darkening { get; set; } = 0.6f;

    /// <summary>
    /// How far the wall's front is behind the back of the tiles' front layer, at least, in tiles. The darker layer of
    /// tiles behind them is from 1 to 1.75 deep, so at 1, the wall is as deep as the shallowest of them.
    /// </summary>
    [Export(PropertyHint.Range, "0,4")] public float Depth { get; set; } = 1;

    /// <summary>How much further back than <see cref="Depth"/> a tile of the wall may be, in four steps, in tiles.</summary>
    [Export(PropertyHint.Range, "0,4")] public float Unevenness { get; set; } = 0.75f;

    public override Node3D? Decorate(Room room, IAssetLibrary assets)
    {
        bool InRoom(Vector2I c) => c.X >= 0 && c.Y >= 0 && c.X < room.Size.X && c.Y < room.Size.Y;

        var wall = new HashSet<Vector2I>();
        for (int y = 0; y < room.Size.Y; y++)
        {
            for (int x = 0; x < room.Size.X; x++)
            {
                if (room.GetTileIndex(x, y) == SpreadsFrom)
                {
                    wall.Add(new Vector2I(x, y));
                }
            }
        }

        for (int i = 0; i < Growths; i++)
        {
            foreach (var coord in wall.ToArray())
            {
                foreach (var (d, direction) in Directions.Index())
                {
                    var next = coord + direction;
                    if (InRoom(next) &&
                        room.GetTileIndex(next.X, next.Y) == TileIndex.Empty &&
                        RandomForTile(room, coord, $"back_wall_grow_{d}") < GrowChance)
                    {
                        wall.Add(next);
                    }
                }
            }
        }

        if (wall.Count == 0)
        {
            return null;
        }

        const float thickness = 1;
        var boxes = new List<(Aabb Box, float Rot)>();
        foreach (var coord in wall)
        {
            float step = Mathf.Floor(RandomForTile(room, coord, "back_wall_depth") * 4) / 3;
            float front = -0.5f - Depth - step * Unevenness;
            float rot = Mathf.Floor(RandomForTile(room, coord, "back_wall_rot") * 4) * Mathf.Pi / 2;
            boxes.Add((new Aabb(new Vector3(coord.X, coord.Y, front - thickness), new Vector3(1, 1, thickness)), rot));
        }
        var node = RoomNode.BuildTilesMesh(boxes, assets.GetTileMesh(DrawnAs, Darkening));
        node.Name = "BackWall";
        return node;
    }
}
