using BodyCheck.Core.WorldData;

using Godot;

namespace BodyCheck.Core.Simulation;

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
        const float growProb = 0.4f;
        for (int i = 0; i < 3; i++)
        {
            foreach (var coord in wallCoords.ToArray())
            {
                var c = room.Origin + coord;
                if (RandomishForXy(coord.X + 53, coord.Y + 429) < growProb)
                {
                    wallCoords.Add(coord + new Vector2I(0, -1));
                }
                if (RandomishForXy(coord.X + 592, coord.Y + 59) < growProb)
                {
                    wallCoords.Add(coord + new Vector2I(-1, 0));
                }
                if (RandomishForXy(coord.X + 285, coord.Y + 357) < growProb)
                {
                    wallCoords.Add(coord + new Vector2I(0, 1));
                }
                if (RandomishForXy(coord.X + 87, coord.Y + 742) < growProb)
                {
                    wallCoords.Add(coord + new Vector2I(1, 0));
                }
            }
        }

        var solidBoxes = new List<Aabb>();
        foreach (var coord in solidCoords)
        {
            var c = room.Origin + coord;
            float h = 1 + MathF.Floor(RandomishForXy(-c.X, -c.Y) * 4) / 4;
            solidBoxes.Add(new Aabb(new Vector3(coord.X, coord.Y, -0.5f), new Vector3(1, 1, h)));
        }

        var backBoxes = new List<Aabb>();
        foreach (var coord in solidCoords)
        {
            var c = room.Origin + coord;
            float h = 1 + MathF.Floor(RandomishForXy(c.X, c.Y) * 4) / 4;
            backBoxes.Add(new Aabb(new Vector3(coord.X, coord.Y, -0.5f - h), new Vector3(1, 1, h)));
        }

        var wallBoxes = new List<Aabb>();
        foreach (var coord in wallCoords)
        {
            var c = room.Origin + coord;
            float h = 1 + MathF.Floor(RandomishForXy(c.X, c.Y) * 4) / 4;
            wallBoxes.Add(new Aabb(new Vector3(coord.X, coord.Y, -0.5f - h - 1), new Vector3(1, 1, 1)));
        }

        var (solidMesh, meshTransform) = FindMesh(assets.GetTileVisual());
        var backMesh = DarkenedMesh(solidMesh.Mesh, 0.4f);
        var wallMesh = DarkenedMesh(solidMesh.Mesh, 0.6f);

        AddChild(BuildTilesMesh(solidBoxes, solidMesh.Mesh, meshTransform));
        AddChild(BuildTilesMesh(backBoxes, backMesh, meshTransform));
        AddChild(BuildTilesMesh(wallBoxes, wallMesh, meshTransform));
    }

    private static float RandomishForXy(float x, float y)
    {
        return ((
            MathF.Sin(x * 3) +
            MathF.Sin(x * 7) +
            MathF.Sin(x * 13) +
            MathF.Sin(x * 23) +
            MathF.Sin(y * 2) +
            MathF.Sin(y * 5) +
            MathF.Sin(y * 11) +
            MathF.Sin(y * 17)
        ) / 8 + 1) / 2;
    }

    private static MultiMeshInstance3D BuildTilesMesh(List<Aabb> boxes, Mesh mesh, Transform3D transform, Material? materialOverride = null)
    {
        var multimesh = new MultiMesh
        {
            TransformFormat = MultiMesh.TransformFormatEnum.Transform3D,
            InstanceCount = boxes.Count,
            Mesh = mesh,
        };
        foreach (var (i, box) in boxes.Index())
        {
            multimesh.SetInstanceTransform(i, new Transform3D(Basis.Identity, box.GetCenter()).ScaledLocal(box.Size) * transform);
        }
        return new MultiMeshInstance3D() { Multimesh = multimesh, MaterialOverride = materialOverride };
    }

    private static (MeshInstance3D Mesh, Transform3D Transform) FindMesh(Node3D rootNode)
    {
        return FindMeshRecur(rootNode, Transform3D.Identity)
            ?? throw new ArgumentException($"The node {rootNode} does not contain a valid MeshInstance3D.", nameof(rootNode));
    }

    private static (MeshInstance3D Mesh, Transform3D Transform)? FindMeshRecur(Node3D currentNode, Transform3D accum)
    {
        if (currentNode is MeshInstance3D meshInstance)
        {
            return (meshInstance, accum * meshInstance.Transform);
        }
        foreach (Node child in currentNode.GetChildren())
        {
            if (child is Node3D childNode3D)
            {
                var found = FindMeshRecur(childNode3D, accum * childNode3D.Transform);
                if (found != null) return found;
            }
        }
        return null;
    }

    public static Mesh DarkenedMesh(Mesh mesh, float factor = 0.5f)
    {
        Mesh darkMesh = (Mesh)mesh.Duplicate();
        int surfaceCount = darkMesh.GetSurfaceCount();
        for (int i = 0; i < surfaceCount; i++)
        {
            if (darkMesh.SurfaceGetMaterial(i) is StandardMaterial3D material)
            {
                StandardMaterial3D darkMaterial = (StandardMaterial3D)material.Duplicate();
                darkMaterial.AlbedoColor = darkMaterial.AlbedoColor.Darkened(factor);
                darkMesh.SurfaceSetMaterial(i, darkMaterial);
            }
        }
        return darkMesh;
    }
}
