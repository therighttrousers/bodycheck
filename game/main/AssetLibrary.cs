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
    private static readonly Dictionary<TileIndex, string> TilePaths = new()
    {
        [TileIndex.Ground] = "res://assets/tiles/ground/ground.tscn",
        [TileIndex.Grass] = "res://assets/tiles/grass/grass.tscn",
    };
    private const string BlockPath = "res://assets/portal_cube/scene.tscn";
    private const string PlatformPath = "res://assets/platform/scene.tscn";

    // How opaque the player's block is: a ghost of a block.
    private const float PlayerBlockAlpha = 0.4f;

    private readonly Dictionary<(TileIndex index, float darkening), (Mesh Mesh, Transform3D Transform)> _tiles = [];

    public Node3D? GetEntityVisual(EntityConfig config) => config.Kind switch
    {
        EntityKind.Player => InstantiateModel("res://assets/bob_the_robot/scene.tscn"),
        EntityKind.Robot => InstantiateModel("res://assets/bob_the_robot/robot.tscn"),
        EntityKind.Platform => InstantiatePlatform(2 * BodyTuning.Platform(config.MapSize).HalfSize.X),
        EntityKind.Block => InstantiateModel(BlockPath),
        EntityKind.VerticalDoor => InstantiateDoor(config),
        EntityKind.Respawn or EntityKind.Trigger or EntityKind.Sequence or EntityKind.Fanout or EntityKind.Once
            or EntityKind.Dialogue or EntityKind.Script => null,
    };

    public Node3D GetPlayerBlockVisual(Vector2 halfSize)
    {
        Node3D model = InstantiateModel(BlockPath);
        MakeGhostly(model);
        return model;
    }

    public (Mesh Mesh, Transform3D Transform) GetTileMesh(TileIndex index, float darkening = 0)
    {
        if (!_tiles.TryGetValue((index, darkening), out var tile))
        {
            (Mesh mesh, Transform3D transform) = LoadMesh(TilePaths[index]);
            tile = _tiles[(index, darkening)] = (darkening == 0 ? mesh : DarkenedMesh(mesh, darkening), transform);
        }
        return tile;
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

    /// <summary>A door: a box the size of its body, and its sounds, which are placeholders.</summary>
    private Node3D InstantiateDoor(EntityConfig config)
    {
        var door = new Node3D();
        door.AddChild(GetDebugBoxVisual(config.MapSize, config.MapColor, alpha: 1));
        var sounds = new CueSounds();
        sounds.AddChild(CueSound(EntityCue.DoorOpened, "res://assets/entity_sfx/door/door_open.wav"));
        sounds.AddChild(CueSound(EntityCue.DoorClosed, "res://assets/entity_sfx/door/door_close.wav"));
        door.AddChild(sounds);
        return door;
    }

    /// <summary>What plays a sound for a cue, under a <see cref="CueSounds"/>.</summary>
    private static AudioStreamPlayer3D CueSound(EntityCue cue, string path) => new()
    {
        Name = cue.ToString(),
        Stream = GD.Load<AudioStream>(path),
        Bus = "Sfx",
    };

    private static Node3D InstantiateModel(string path)
    {
        var scene = GD.Load<PackedScene>(path);
        return scene.Instantiate<Node3D>();
    }

    /// <summary>
    /// The platform's model, <paramref name="width"/> tiles wide: its end caps, whose origins are at their outer ends, go
    /// to the ends, and its center, one tile wide, stretches between them.
    /// </summary>
    private static Node3D InstantiatePlatform(float width)
    {
        Node3D model = InstantiateModel(PlatformPath);
        var capLeft = model.GetNode<MeshInstance3D>("Model/CapLeft");
        var capRight = model.GetNode<MeshInstance3D>("Model/CapRight");
        var center = model.GetNode<MeshInstance3D>("Model/Center");
        capLeft.Position = capLeft.Position with { X = -width / 2 };
        capRight.Position = capRight.Position with { X = width / 2 };
        float capsWidth = capLeft.GetAabb().Size.X + capRight.GetAabb().Size.X;
        center.Scale = center.Scale with { X = width - capsWidth };
        return model;
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
