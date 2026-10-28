using System;

using BodyCheck.Core;
using BodyCheck.Core.Controllers;
using BodyCheck.Core.Entities;
using BodyCheck.Core.Simulation;
using BodyCheck.Look;

using Godot;

namespace BodyCheck;

/// <summary>
/// A placeholder for showing a phase being aimed: a mark around the target, and, with a direction held, two arrows from
/// the center of mass of the player and the target, for the velocity each would leave with if phase were released now,
/// at a tenth of a tile per tile per second. Like the <see cref="DebugOverlay"/>, it is drawn
/// over the game at the window's full resolution, and it draws while the game is paused for the aim.
/// </summary>
public partial class PhaseAimMarker : Node2D
{
    private static readonly Color PlayerColor = new(1, 0.85f, 0.2f);
    private const float PlayerLineWidth = 4;
    private const float PlayerHeadLength = 30;

    private static readonly Color TargetColor = new(0.9f, 0.6f, 0.15f);
    private const float TargetLineWidth = 3;
    private const float TargetHeadLength = 20;

    private readonly WorldViewportContainer _worldView;

    public PhaseAimMarker(WorldViewportContainer worldView)
    {
        _worldView = worldView;
    }

    /// <summary>The world whose player's aim is shown.</summary>
    public WorldNode? World { get; set; }

    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;
    }

    public override void _Process(double delta) => QueueRedraw();

    public override void _Draw()
    {
        Camera3D? camera = _worldView.World.GetCamera3D();
        if (camera is null || World?.PlayerController.Phase is not PhaseAction phase)
        {
            return;
        }

        // From the plane of play to the window's pixels, where the container draws the game scaled up.
        Transform2D toWindow = _worldView.GetGlobalTransform();
        Vector2 ToWindow(Vector2 point) => toWindow * camera.UnprojectPosition(point.ToVector3());

        Entity target = phase.Target;
        Vector2 targetCenter = target.Position.ToVector2();
        Vector2 corner = target.HalfSize + new Vector2(0.15f, 0.15f);
        Vector2[] box = [
            ToWindow(targetCenter + corner * new Vector2(-1, -1)), ToWindow(targetCenter + corner * new Vector2(1, -1)),
            ToWindow(targetCenter + corner * new Vector2(1, 1)), ToWindow(targetCenter + corner * new Vector2(-1, 1)),
            ToWindow(targetCenter + corner * new Vector2(-1, -1)),
        ];
        DrawPolyline(box, TargetColor, TargetLineWidth);

        if (phase.Direction == Vector3.Zero)
        {
            return;
        }

        Vector2 center = phase.CenterOfMass().ToVector2();
        var (playerImpulse, targetImpulse) = phase.Impulses();
        Vector3 playerVelocity = (phase.Player.Mass * phase.Player.LinearVelocity + playerImpulse) / phase.Player.Mass;
        Vector3 targetVelocity = (phase.Target.Mass * phase.Target.LinearVelocity + targetImpulse) / phase.Target.Mass;
        Vector2 playerDir = playerVelocity.ToVector2() / 8;
        Vector2 targetDir = targetVelocity.ToVector2() / 8;
        DrawArrow(ToWindow(center), ToWindow(center + playerDir), PlayerColor, PlayerLineWidth, PlayerHeadLength);
        DrawArrow(ToWindow(center), ToWindow(center + targetDir), TargetColor, TargetLineWidth, TargetHeadLength);
    }

    private void DrawArrow(Vector2 p1, Vector2 p2, Color color, float lineWidth, float headLength)
    {
        Vector2 dir = (p2 - p1).Normalized();
        DrawLine(p1, p2, color, lineWidth);
        DrawLine(p2, p2 - headLength * dir.Rotated(0.5f), color, lineWidth);
        DrawLine(p2, p2 - headLength * dir.Rotated(-0.5f), color, lineWidth);
    }
}
