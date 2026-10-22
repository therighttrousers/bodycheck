using System;
using System.Collections.Generic;

using BodyCheck.Core.Simulation;
using BodyCheck.Core.WorldData;

using Godot;

namespace BodyCheck;

/// <summary>
/// What the game draws for each kind of entity, a model or a box the size of its body, and for tiles. It loads the
/// tile's mesh once, and each darkened copy of it once.
/// </summary>
public class AssetLibrary : IAssetLibrary
{
    private const string TilePath = "res://assets/tile/tile.tscn";

    private static readonly Color PlatformColor = new(0.9f, 0.1f, 0.7f);

    private (Mesh Mesh, Transform3D Transform)? _tile;
    private readonly Dictionary<float, Mesh> _darkenedTiles = [];

    public Node3D GetEntityVisual(EntityKind kind, Vector2 halfSize) => kind switch
    {
        EntityKind.Player => InstantiateModel("res://assets/bob_the_robot/scene.tscn"),
        EntityKind.Robot => InstantiateModel("res://assets/bob_the_robot/robot.tscn"),
        EntityKind.Platform => BoxVisual(halfSize, PlatformColor),
        EntityKind.Block => InstantiateModel("res://assets/portal_cube/scene.tscn"),
    };

    public (Mesh Mesh, Transform3D Transform) GetTileMesh(float darkening = 0)
    {
        _tile ??= LoadMesh(TilePath);
        (Mesh mesh, Transform3D transform) = _tile.Value;
        if (darkening == 0)
        {
            return (mesh, transform);
        }
        if (!_darkenedTiles.TryGetValue(darkening, out Mesh? darkened))
        {
            darkened = DarkenedMesh(mesh, darkening);
            _darkenedTiles[darkening] = darkened;
        }
        return (darkened, transform);
    }

    private static MeshInstance3D BoxVisual(Vector2 halfSize, Color color, bool glows = false)
    {
        var mesh = new MeshInstance3D
        {
            Mesh = new BoxMesh
            {
                Size = new Vector3(2 * halfSize.X, 2 * halfSize.Y, 2 * halfSize.X),
                Material = new StandardMaterial3D
                {
                    AlbedoColor = color,
                    EmissionEnabled = glows,
                    Emission = color,
                    EmissionEnergyMultiplier = 1.5f,
                },
            },
            Position = new Vector3(0, halfSize.Y, 0),
        };
        if (glows)
        {
            mesh.AddChild(new OmniLight3D { LightColor = color, LightEnergy = 2, OmniRange = 6 });
        }
        return mesh;
    }

    private static Node3D InstantiateModel(string path)
    {
        var scene = GD.Load<PackedScene>(path);
        return scene.Instantiate<Node3D>();
    }

    /// <summary>
    /// The first mesh in the scene at <paramref name="path"/>, with the transform from the scene's root to it. The
    /// scene's nodes are freed; the mesh outlives them.
    /// </summary>
    private static (Mesh Mesh, Transform3D Transform) LoadMesh(string path)
    {
        Node3D root = InstantiateModel(path);
        try
        {
            return FindMesh(root, Transform3D.Identity)
                ?? throw new ArgumentException($"The scene {path} has no MeshInstance3D.", nameof(path));
        }
        finally
        {
            root.Free();
        }
    }

    private static (Mesh Mesh, Transform3D Transform)? FindMesh(Node3D node, Transform3D accum)
    {
        if (node is MeshInstance3D meshInstance)
        {
            return (meshInstance.Mesh, accum);
        }
        foreach (Node child in node.GetChildren())
        {
            if (child is Node3D child3D)
            {
                var found = FindMesh(child3D, accum * child3D.Transform);
                if (found != null)
                {
                    return found;
                }
            }
        }
        return null;
    }

    /// <summary>A copy of <paramref name="mesh"/> with each surface's albedo darkened by <paramref name="factor"/>.</summary>
    private static Mesh DarkenedMesh(Mesh mesh, float factor)
    {
        var darkMesh = (Mesh)mesh.Duplicate();
        for (int i = 0; i < darkMesh.GetSurfaceCount(); i++)
        {
            if (darkMesh.SurfaceGetMaterial(i) is StandardMaterial3D material)
            {
                var darkMaterial = (StandardMaterial3D)material.Duplicate();
                darkMaterial.AlbedoColor = darkMaterial.AlbedoColor.Darkened(factor);
                darkMesh.SurfaceSetMaterial(i, darkMaterial);
            }
        }
        return darkMesh;
    }
}
