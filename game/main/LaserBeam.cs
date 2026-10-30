using System;

using BodyCheck.Core.Entities;

using Godot;

namespace BodyCheck;

/// <summary>
/// Draws a robot's <see cref="Laser"/>. It goes in the scene that draws the robot, with a mesh under it named "Beam",
/// one unit long along its Y axis and one unit across, centered on its origin, such as a cylinder. Each frame it
/// stretches the beam from where the laser starts to where it ends, as thick as the laser's stage calls for, and hides
/// it while the laser is off. It shows the robot it is under.
/// </summary>
[GlobalClass]
public partial class LaserBeam : Node3D
{
    /// <summary>How thick the beam is while it follows its target, in tiles. A pixel is 1/16 of a tile.</summary>
    [Export]
    public float AimingWidth { get; set; } = 0.0625f;

    /// <summary>How thick the beam is once it has locked, in tiles.</summary>
    [Export]
    public float LockedWidth { get; set; } = 0.125f;

    /// <summary>How thick the beam is while it fires, in tiles.</summary>
    [Export]
    public float FiringWidth { get; set; } = 0.375f;

    [Export]
    public Color AimingColor { get; set; } = new(1, 0.1f, 0.05f);

    [Export]
    public Color LockedColor { get; set; } = new(1, 0.5f, 0);

    [Export]
    public Color FiringColor { get; set; } = new(0.5f, 0.7f, 1);

    private RobotEntity? _robot;
    private MeshInstance3D _beam = null!;
    private StandardMaterial3D _material = null!;
    private float _emissionMultiplier;

    public override void _Ready()
    {
        _beam = GetNode<MeshInstance3D>("Beam");
        // Placed in world coordinates each frame, from the laser's own, so not moved with the robot or turned with it.
        _beam.TopLevel = true;
        _beam.PhysicsInterpolationMode = PhysicsInterpolationModeEnum.Off;
        _beam.Visible = false;
        _beam.MaterialOverride = _material = (StandardMaterial3D)_beam.GetActiveMaterial(0).Duplicate();
        _material.Transparency = BaseMaterial3D.TransparencyEnum.AlphaHash;
        _emissionMultiplier = _material.EmissionEnergyMultiplier;
    }

    public override void _EnterTree()
    {
        for (Node? node = GetParent(); node is not null; node = node.GetParent())
        {
            if (node is RobotEntity robot)
            {
                _robot = robot;
                return;
            }
        }
    }

    public override void _ExitTree() => _robot = null;

    public override void _Process(double gameDt)
    {
        if (_robot?.Laser is not { Stage: not LaserStage.Off } laser || laser.Origin.IsEqualApprox(laser.End))
        {
            _beam.Visible = false;
            return;
        }

        (float width, Color color) = laser.Stage switch
        {
            LaserStage.Aiming => (AimingWidth, AimingColor),
            LaserStage.Locked => (LockedWidth, GetLockedColor(laser)),
            _ => (FiringWidth, GetFiringColor(laser)),
        };
        // The beam's Y axis along the laser, and its other two across it, one in the plane of play.
        Vector3 along = laser.End - laser.Origin;
        Vector3 across = new Vector3(-along.Y, along.X, 0).Normalized() * width;
        Vector3 depth = Vector3.Back * width;
        _beam.GlobalTransform = new Transform3D(new Basis(across, along, depth), (laser.Origin + laser.End) / 2);
        _beam.Visible = true;
        _material.AlbedoColor = color;
        _material.Emission = color;
        _material.EmissionEnergyMultiplier = _emissionMultiplier * GetFlicker(laser);
    }

    private Color GetLockedColor(Laser laser)
    {
        return LockedColor.Lerp(FiringColor, laser.Progress);
    }

    private Color GetFiringColor(Laser laser)
    {
        return FiringColor.Lerp(AimingColor with { A = 0 }, MathF.Pow(laser.Progress, 5));
    }

    private static float GetFlicker(Laser laser)
    {
        // If firing, flicker 5 times, with flicker attenuating over time.
        return laser.Stage == LaserStage.Firing
            ? 1 + 0.5f * MathF.Sin(laser.Progress * 2 * MathF.PI * 5) * MathF.Pow(1 - laser.Progress, 0.5f)
            : 1;
    }
}
