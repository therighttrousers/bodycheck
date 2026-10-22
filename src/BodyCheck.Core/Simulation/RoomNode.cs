using BodyCheck.Core.WorldData;

using Godot;

namespace BodyCheck.Core.Simulation;

/// <summary>
/// What a room looks like: its solid tiles, a layer of darker tiles behind them, and a back wall, with holes in it,
/// that spreads from them. Each layer is one multimesh. It has no colliders, which the world builds for all its rooms
/// at once.
/// </summary>
public partial class RoomNode : Node3D
{
    public RoomNode(Room room, IAssetLibrary assets)
    {
        Position = new Vector3(room.Origin.X, room.Origin.Y, 0);

        // Collect the coordinates of the solid tiles.
        var solidCoords = new List<Vector2I>();
        for (int y = 0; y < room.Size.Y; y++)
        {
            for (int x = 0; x < room.Size.X; x++)
            {
                if (!room.IsSolid(x, y)) continue;
                solidCoords.Add(new Vector2I(x, y));
            }
        }

        // Start with wall coordinates = solid tile coordinates.
        var wallCoords = new HashSet<Vector2I>(solidCoords);
        // For a few iterations, grow the set from existing members in each of 4 directions with some probability.
        // This will result in a back wall with large holes in it and wall tiles concentrated near the solid tiles.
        const float growThresh = 0.45f; // Not a probability - RandomishForXy is not uniform
        for (int i = 0; i < 3; i++)
        {
            foreach (var coord in wallCoords.ToArray())
            {
                var worldCoord = coord + room.Origin;
                if (RandomishForXy(worldCoord + new Vector2I(53, 429)) < growThresh)
                {
                    wallCoords.Add(coord + new Vector2I(0, -1));
                }
                if (RandomishForXy(worldCoord + new Vector2I(592, 59)) < growThresh)
                {
                    wallCoords.Add(coord + new Vector2I(-1, 0));
                }
                if (RandomishForXy(worldCoord + new Vector2I(285, 357)) < growThresh)
                {
                    wallCoords.Add(coord + new Vector2I(0, 1));
                }
                if (RandomishForXy(worldCoord + new Vector2I(87, 742)) < growThresh)
                {
                    wallCoords.Add(coord + new Vector2I(1, 0));
                }
            }
        }

        var solidBoxes = new List<Aabb>();
        foreach (var coord in solidCoords)
        {
            float h = 1 + MathF.Floor(RandomishForXy(-(room.Origin + coord)) * 4) / 4;
            solidBoxes.Add(new Aabb(new Vector3(coord.X, coord.Y, -0.5f), new Vector3(1, 1, h)));
        }

        var backBoxes = new List<Aabb>();
        foreach (var coord in solidCoords)
        {
            float h = 1 + MathF.Floor(RandomishForXy(room.Origin + coord) * 4) / 4;
            backBoxes.Add(new Aabb(new Vector3(coord.X, coord.Y, -0.5f - h), new Vector3(1, 1, h)));
        }

        // The same depths as the back tiles', so that the wall sits right behind them where there are any.
        var wallBoxes = new List<Aabb>();
        foreach (var coord in wallCoords)
        {
            if (coord.X < 0 || coord.Y < 0 || coord.X >= room.Size.X || coord.Y >= room.Size.Y)
            {
                continue;
            }
            float h = 1 + MathF.Floor(RandomishForXy(room.Origin + coord) * 4) / 4;
            wallBoxes.Add(new Aabb(new Vector3(coord.X, coord.Y, -0.5f - h - 1), new Vector3(1, 1, 1)));
        }

        AddChild(BuildTilesMesh(solidBoxes, assets.GetTileMesh()));
        AddChild(BuildTilesMesh(backBoxes, assets.GetTileMesh(darkening: 0.4f)));
        AddChild(BuildTilesMesh(wallBoxes, assets.GetTileMesh(darkening: 0.6f)));
    }

    private static float RandomishForXy(Vector2I worldCoord)
    {
        return ((
            MathF.Sin(worldCoord.X * 3) +
            MathF.Sin(worldCoord.X * 7) +
            MathF.Sin(worldCoord.X * 13) +
            MathF.Sin(worldCoord.X * 23) +
            MathF.Sin(worldCoord.Y * 2) +
            MathF.Sin(worldCoord.Y * 5) +
            MathF.Sin(worldCoord.Y * 11) +
            MathF.Sin(worldCoord.Y * 17)
        ) / 8 + 1) / 2;
    }

    private static MultiMeshInstance3D BuildTilesMesh(List<Aabb> boxes, (Mesh Mesh, Transform3D Transform) tile)
    {
        var multimesh = new MultiMesh
        {
            TransformFormat = MultiMesh.TransformFormatEnum.Transform3D,
            InstanceCount = boxes.Count,
            Mesh = tile.Mesh,
        };
        foreach (var (i, box) in boxes.Index())
        {
            multimesh.SetInstanceTransform(i, new Transform3D(Basis.Identity, box.GetCenter()).ScaledLocal(box.Size) * tile.Transform);
        }
        return new MultiMeshInstance3D() { Multimesh = multimesh };
    }
}
