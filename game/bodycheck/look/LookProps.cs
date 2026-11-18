using System.Collections.Generic;

using Godot;

namespace BodyCheck.Look;

/// <summary>
/// Shapes to judge the rendering pipeline on, which gray boxes don't show: curved and slanted faces,
/// smooth and flat shading, a mirror finish, glow, glass, a yellow lamp and a grayscale ramp. The shapes turn,
/// because dither and aliasing are judged in motion. They have no colliders.
/// </summary>
public partial class LookProps : Node3D
{
    // Radians per second.
    private const float SpinSpeed = 0.7f;

    private readonly List<Node3D> _spinners = [];

    public LookProps(Vector2I size)
    {
        float y = size.Y - 5.5f;
        float spacing = size.X / 6f;

        // Smooth and shiny.
        AddSpinner(spacing, y,
            new SphereMesh { Radius = 1.2f, Height = 2.4f },
            new StandardMaterial3D { AlbedoColor = new Color(1.0f, 0.75f, 0.3f), Metallic = 1, Roughness = 0.25f });

        // The same shape with few faces and flat shading, as mid-1990s would have drawn it.
        AddSpinner(2 * spacing, y,
            Faceted(new SphereMesh { Radius = 1.2f, Height = 2.4f, RadialSegments = 8, Rings = 4 }),
            new StandardMaterial3D { AlbedoColor = new Color(0.3f, 0.5f, 1.0f) });

        AddSpinner(3 * spacing, y,
            new PrismMesh { Size = new Vector3(2.2f, 2.2f, 2.2f) },
            new StandardMaterial3D { AlbedoColor = new Color(0.9f, 0.2f, 0.2f) });

        var glow = new Color(0.4f, 1.0f, 0.3f);
        MeshInstance3D torus = AddSpinner(4 * spacing, y,
            new TorusMesh { InnerRadius = 0.6f, OuterRadius = 1.2f },
            new StandardMaterial3D
            {
                AlbedoColor = glow,
                EmissionEnabled = true,
                Emission = glow,
                EmissionEnergyMultiplier = 2,
            });
        torus.AddChild(new OmniLight3D { LightColor = glow, LightEnergy = 2, OmniRange = 6 });

        // A pure yellow lamp, out of reach of the torus's light. Its falloff over the gray wall is where bands
        // and false colours in the colour pass show first.
        var yellow = new Color(1, 1, 0);
        var lamp = new MeshInstance3D
        {
            Mesh = new SphereMesh
            {
                Radius = 0.3f,
                Height = 0.6f,
                Material = new StandardMaterial3D
                {
                    AlbedoColor = yellow,
                    EmissionEnabled = true,
                    Emission = yellow,
                    EmissionEnergyMultiplier = 2,
                },
            },
            Position = new Vector3(spacing, 5, 0.5f),
        };
        lamp.AddChild(new OmniLight3D { LightColor = yellow, LightEnergy = 2, OmniRange = 6 });
        AddChild(lamp);

        // Glass, in front of something to see through it.
        MeshInstance3D glass = AddSpinner(5 * spacing, y,
            new BoxMesh { Size = new Vector3(2.4f, 2.4f, 0.2f) },
            new StandardMaterial3D
            {
                AlbedoColor = new Color(0.6f, 0.9f, 1.0f, 0.3f),
                Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
                Roughness = 0.1f,
                RefractionEnabled = true,
                RefractionScale = 0.05f,
            });
        glass.Position += new Vector3(0, 0, 0.5f);
        AddChild(new MeshInstance3D
        {
            Mesh = new SphereMesh
            {
                Radius = 0.6f,
                Height = 1.2f,
                Material = new StandardMaterial3D { AlbedoColor = new Color(1.0f, 0.4f, 0.8f) },
            },
            Position = new Vector3(5 * spacing, y, -0.2f),
        });

        // An unlit ramp from black to white along the top of the room.
        AddChild(new MeshInstance3D
        {
            Mesh = new QuadMesh
            {
                Size = new Vector2(size.X - 4, 1),
                Material = new StandardMaterial3D
                {
                    AlbedoTexture = new GradientTexture2D
                    {
                        Gradient = new Gradient { Colors = [Colors.Black, Colors.White], Offsets = [0, 1] },
                    },
                    ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
                },
            },
            Position = new Vector3(size.X / 2f, size.Y - 2f, -0.45f),
        });
    }

    public override void _Process(double delta)
    {
        foreach (Node3D spinner in _spinners)
        {
            spinner.RotateY(SpinSpeed * (float)delta);
            spinner.RotateX(SpinSpeed * 0.37f * (float)delta);
        }
    }

    private MeshInstance3D AddSpinner(float x, float y, Mesh mesh, Material material)
    {
        var node = new MeshInstance3D { Mesh = mesh, MaterialOverride = material, Position = new Vector3(x, y, 0) };
        AddChild(node);
        _spinners.Add(node);
        return node;
    }

    /// <summary>Rebuilds a mesh with one normal per triangle, so that each face is shaded flat.</summary>
    private static ArrayMesh Faceted(Mesh mesh)
    {
        Vector3[] corners = mesh.GetFaces();
        var tool = new SurfaceTool();
        tool.Begin(Mesh.PrimitiveType.Triangles);
        for (int i = 0; i < corners.Length; i += 3)
        {
            // The engine's front faces wind clockwise.
            Vector3 normal = (corners[i + 2] - corners[i]).Cross(corners[i + 1] - corners[i]).Normalized();
            for (int j = 0; j < 3; j++)
            {
                tool.SetNormal(normal);
                tool.AddVertex(corners[i + j]);
            }
        }
        return tool.Commit();
    }
}
