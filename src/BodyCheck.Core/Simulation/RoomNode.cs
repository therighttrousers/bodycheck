using BodyCheck.Core.WorldData;

using Godot;

namespace BodyCheck.Core.Simulation;

/// <summary>
/// What a room looks like: for each kind of tile, its tiles and a layer of darker tiles behind them, each turned by a
/// random quarter-turn to hide repetition in the art. Each layer is one multimesh. It has no colliders, which the world
/// builds for all its rooms at once.
/// </summary>
public partial class RoomNode : Node3D
{
    public RoomNode(Room room, IAssetLibrary assets)
    {
        Position = new Vector3(room.Origin.X, room.Origin.Y, 0);

        for (TileIndex index = TileIndex.Empty + 1; index < TileIndex.Count; index++)
        {
            // Collect the coordinates of this kind's tiles.
            var coords = new List<Vector2I>();
            for (int y = 0; y < room.Size.Y; y++)
            {
                for (int x = 0; x < room.Size.X; x++)
                {
                    if (room.GetTileIndex(x, y) == index)
                    {
                        coords.Add(new Vector2I(x, y));
                    }
                }
            }

            var tileBoxes = new List<(Aabb Box, float Rot)>();
            foreach (var coord in coords)
            {
                float h = 1 + MathF.Floor(RandomishForXy(room.Origin + coord, "solid") * 4) / 4;
                float rot = MathF.Floor(RandomishForXy(room.Origin + coord, "solid_rot") * 4) * MathF.PI / 2;
                tileBoxes.Add((new Aabb(new Vector3(coord.X, coord.Y, -0.5f), new Vector3(1, 1, h)), rot));
            }

            var backBoxes = new List<(Aabb Box, float Rot)>();
            foreach (var coord in coords)
            {
                float h = 1 + MathF.Floor(RandomishForXy(room.Origin + coord, "back") * 4) / 4;
                float rot = MathF.Floor(RandomishForXy(room.Origin + coord, "back_rot") * 4) * MathF.PI / 2;
                backBoxes.Add((new Aabb(new Vector3(coord.X, coord.Y, -0.5f - h), new Vector3(1, 1, h)), rot));
            }

            if (tileBoxes.Count > 0)
            {
                AddChild(BuildTilesMesh(tileBoxes, assets.GetTileMesh(index)));
            }
            if (backBoxes.Count > 0)
            {
                AddChild(BuildTilesMesh(backBoxes, assets.GetTileMesh(index, darkening: 0.4f)));
            }
        }
    }

    private static float RandomishForXy(Vector2I worldCoord, string salt)
    {
        return new Rand($"{worldCoord.X},{worldCoord.Y},{salt}").NextFloat();
    }

    private static MultiMeshInstance3D BuildTilesMesh(
        List<(Aabb Box, float Rot)> boxes, (Mesh Mesh, Transform3D Transform) tile)
    {
        var multimesh = new MultiMesh
        {
            TransformFormat = MultiMesh.TransformFormatEnum.Transform3D,
            InstanceCount = boxes.Count,
            Mesh = tile.Mesh,
        };
        foreach (var (i, (box, rot)) in boxes.Index())
        {
            var transform =
                new Transform3D(Basis.Identity, box.GetCenter())
                    .RotatedLocal(Vector3.Back, rot)
                    .ScaledLocal(box.Size);
            multimesh.SetInstanceTransform(i, transform * tile.Transform);
        }
        return new MultiMeshInstance3D() { Multimesh = multimesh };
    }
}
