using System;
using System.Collections.Generic;

using BodyCheck.Core.Entities;
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
    private const string BlockPath = "res://assets/portal_cube/scene.tscn";

    // How opaque the player's block is: a ghost of a block.
    private const float PlayerBlockAlpha = 0.4f;

    private (Mesh Mesh, Transform3D Transform)? _tile;
    private readonly Dictionary<float, Mesh> _darkenedTiles = [];

    public Node3D? GetEntityVisual(EntityConfig config) => config.Kind switch
    {
        EntityKind.Player => InstantiateModel("res://assets/bob_the_robot/scene.tscn"),
        EntityKind.Robot => InstantiateModel("res://assets/bob_the_robot/robot.tscn"),
        EntityKind.Platform => GetDebugBoxVisual(2 * BodyTuning.Platform(config.MapSize).HalfSize, config.MapColor, 1.0f),
        EntityKind.Block => InstantiateModel(BlockPath),
        EntityKind.Respawn => null,
    };

    public Node3D GetPlayerBlockVisual(Vector2 halfSize)
    {
        Node3D model = InstantiateModel(BlockPath);
        MakeGhostly(model);
        return model;
    }

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

    public MeshInstance3D GetDebugBoxVisual(Vector2 size, Color color, float alpha = 0.5f)
    {
        return new MeshInstance3D
        {
            Mesh = new BoxMesh
            {
                Size = new Vector3(size.X, size.Y, 1),
                Material = new StandardMaterial3D
                {
                    AlbedoColor = new Color(color.R, color.G, color.B, alpha),
                    Transparency = alpha < 1 ? BaseMaterial3D.TransparencyEnum.Alpha : BaseMaterial3D.TransparencyEnum.Disabled,
                },
            },
        };
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
    private static Mesh DarkenedMesh(Mesh mesh, float factor) =>
        WithMaterials(mesh, material => material.AlbedoColor = material.AlbedoColor.Darkened(factor));

    // Makes every mesh under the node see-through, with copies of their meshes, so that the scene's own are unchanged.
    private static void MakeGhostly(Node node)
    {
        if (node is MeshInstance3D meshInstance)
        {
            meshInstance.Mesh = WithMaterials(meshInstance.Mesh, material =>
            {
                material.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
                material.AlbedoColor = material.AlbedoColor with { A = PlayerBlockAlpha };
            });
        }
        foreach (Node child in node.GetChildren())
        {
            MakeGhostly(child);
        }
    }

    /// <summary>A copy of <paramref name="mesh"/> with a copy of each surface's material, changed by <paramref name="change"/>.</summary>
    private static Mesh WithMaterials(Mesh mesh, Action<BaseMaterial3D> change)
    {
        var copy = (Mesh)mesh.Duplicate();
        for (int i = 0; i < copy.GetSurfaceCount(); i++)
        {
            if (copy.SurfaceGetMaterial(i) is BaseMaterial3D material)
            {
                var changed = (BaseMaterial3D)material.Duplicate();
                change(changed);
                copy.SurfaceSetMaterial(i, changed);
            }
        }
        return copy;
    }
}
