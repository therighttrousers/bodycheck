using BodyCheck.Core.Entities;
using BodyCheck.Core.Simulation;
using BodyCheck.Core.WorldData;

using Godot;

namespace BodyCheck;

public class AssetLibrary : IAssetLibrary
{
    private static readonly Color PlatformColor = new(0.9f, 0.1f, 0.7f);

    /// <summary>What to draw for an entity of <paramref name="kind"/>, with its origin at the middle of its base.</summary>
    public Node3D GetEntityVisual(EntityKind kind, Vector2 halfSize) => kind switch
    {
        EntityKind.Player => InstantiateModel("res://assets/bob_the_robot/scene.tscn"),
        EntityKind.Robot => InstantiateModel("res://assets/bob_the_robot/robot.tscn"),
        EntityKind.Platform => BoxVisual(halfSize, PlatformColor),
        EntityKind.Block => InstantiateModel("res://assets/portal_cube/scene.tscn"),
    };

    public Node3D GetTileVisual() => InstantiateModel("res://assets/tile/tile.tscn");

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
}
