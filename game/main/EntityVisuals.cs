using BodyCheck.Core.Entities;
using BodyCheck.Core.WorldData;

using Godot;

namespace BodyCheck;

/// <summary>What the game draws for each kind of entity: a model, or a box the size of its body.</summary>
public static class EntityVisuals
{
    private static readonly Color PlatformColor = new(0.9f, 0.1f, 0.7f);

    /// <summary>What to draw for an entity of <paramref name="kind"/>, with its origin at the middle of its base.</summary>
    public static Node3D For(EntityKind kind, BodyTuning body) => kind switch
    {
        EntityKind.Player => InstantiateModel("res://assets/bob_the_robot/scene.tscn"),
        EntityKind.Robot => InstantiateModel("res://assets/bob_the_robot/robot.tscn"),
        EntityKind.Platform => BoxVisual(body, PlatformColor),
        EntityKind.Block => InstantiateModel("res://assets/portal_cube/scene.tscn"),
    };

    /// <summary>A box the size of a body, to draw an entity as.</summary>
    private static MeshInstance3D BoxVisual(BodyTuning body, Color color, bool glows = false)
    {
        Vector2 halfSize = body.HalfSize;
        var mesh = new MeshInstance3D
        {
            Mesh = new BoxMesh
            {
                Size = new Vector3(2 * halfSize.X, 2 * halfSize.Y, Entity.Depth),
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
}
