using System.Collections.Generic;

using BodyCheck.Core.Rooms;

using Godot;

namespace BodyCheck;

/// <summary>
/// Builds the solid tiles of a grid as one static body. The engine removes ghost collisions at the seams
/// between shapes of one body, but not between bodies.
/// </summary>
public static class TileColliders
{
    // Walkers have no engine friction, and the smaller of two coefficients wins, so this only affects
    // bodies that have some.
    private const float Friction = 1;

    /// <summary>
    /// Returns a static body with a box collider for each box of tiles. Each collider is drawn, so the
    /// layout is visible.
    /// </summary>
    /// <param name="merge">Whether to merge tiles into larger boxes, or give each tile its own.</param>
    public static StaticBody3D Build(TileGrid tiles, bool merge = true)
    {
        var body = new StaticBody3D { PhysicsMaterialOverride = new PhysicsMaterial { Friction = Friction, Bounce = 0 } };
        var material = new StandardMaterial3D { AlbedoColor = new Color(0.35f, 0.38f, 0.45f) };
        foreach (Rect2I box in TileBoxes(tiles, merge))
        {
            var size = new Vector3(box.Size.X, box.Size.Y, 1);
            var position = new Vector3(box.Position.X + box.Size.X / 2f, box.Position.Y + box.Size.Y / 2f, 0);
            body.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = size }, Position = position });
            body.AddChild(new MeshInstance3D
            {
                Mesh = new BoxMesh { Size = size, Material = material },
                Position = position,
            });
        }
        return body;
    }

    /// <summary>
    /// The solid tiles as boxes, in tile coordinates. Merging joins each horizontal run of tiles into one
    /// box, then joins runs that sit directly above one another and span the same columns.
    /// </summary>
    private static List<Rect2I> TileBoxes(TileGrid tiles, bool merge)
    {
        var boxes = new List<Rect2I>();

        // The runs of the row below, by their first and last column, with the index of the box each belongs to.
        var below = new Dictionary<(int, int), int>();
        for (int y = 0; y < tiles.Height; y++)
        {
            var current = new Dictionary<(int, int), int>();
            for (int x = 0; x < tiles.Width; x++)
            {
                if (!tiles.IsSolid(x, y))
                {
                    continue;
                }

                int start = x;
                while (merge && x + 1 < tiles.Width && tiles.IsSolid(x + 1, y))
                {
                    x++;
                }

                if (merge && below.TryGetValue((start, x), out int index))
                {
                    boxes[index] = new Rect2I(boxes[index].Position, boxes[index].Size + new Vector2I(0, 1));
                }
                else
                {
                    index = boxes.Count;
                    boxes.Add(new Rect2I(start, y, x - start + 1, 1));
                }
                current[(start, x)] = index;
            }
            below = current;
        }
        return boxes;
    }
}
