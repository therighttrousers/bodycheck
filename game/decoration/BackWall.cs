using System;
using System.Collections.Generic;
using System.Linq;

using BodyCheck.Core.Simulation;
using BodyCheck.Core.WorldData;

using Godot;

namespace BodyCheck.Decoration;

/// <summary>
/// A wall behind a room's tiles, with holes in it: the tiles its growths cover, each of which spreads from one kind of
/// tile into the empty tiles around it, by chance, so that the wall is densest near those tiles. It is drawn with a kind
/// of tile's mesh, darkened, each tile at a random depth, turned by a random quarter-turn, as one multimesh.
/// </summary>
[GlobalClass]
public partial class BackWall : RoomDecorator
{
    /// <summary>
    /// What the wall covers: the tiles that any of them does. Several growths can make a wall grow further in some
    /// directions than others, such as far up and down, and a little across.
    /// </summary>
    [Export] public Godot.Collections.Array<BackWallGrowth> Growths { get; set; } = [];

    /// <summary>The kind of tile whose mesh the wall is drawn with.</summary>
    [Export] public TileIndex DrawnAs { get; set; } = TileIndex.Ground;

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
        var wall = new HashSet<Vector2I>();
        foreach (var (i, growth) in Growths.Index())
        {
            if (growth is null)
            {
                throw new InvalidOperationException($"{nameof(BackWall)} {ResourcePath}: growth {i} is empty.");
            }
            wall.UnionWith(growth.Grow(room, $"back_wall_{i}"));
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
