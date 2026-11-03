using System;

using BodyCheck.Core;
using BodyCheck.Core.Controllers;
using BodyCheck.Core.Entities;
using BodyCheck.Core.Simulation;

using Godot;

namespace BodyCheck;

/// <summary>
/// A placeholder for showing a phase being aimed: a mark around the target, and, with a direction held, two arrows from
/// the center of mass of the player and the target, for the velocity each would leave with if phase were released now,
/// at a tenth of a tile per tile per second. It belongs in the game's viewport, where it is drawn as part of the game's
/// image: over the world, under the colour pass and the menu. It draws while the game is paused for the aim. Its sizes
/// are in pixels at 320x240.
/// </summary>
public partial class PhaseAimMarker : Node2D
{
    private static readonly Color PlayerColor = new(1, 0.85f, 0.2f);
    private const float PlayerLineWidth = 1;
    private const float PlayerHeadLength = 8;

    private static readonly Color TargetColor = new(0.9f, 0.6f, 0.15f);
    private const float TargetLineWidth = 1;
    private const float TargetHeadLength = 5;

    /// <summary>The world whose player's aim is shown.</summary>
    public WorldNode? World { get; set; }

    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;
    }

    public override void _Process(double delta) => QueueRedraw();

    public override void _Draw()
    {
        Camera3D? camera = GetViewport().GetCamera3D();
        if (camera is null || World?.PlayerController.Phase is not PhaseAction.PhaseAim phase)
        {
            return;
        }

        // From the plane of play to the viewport's pixels.
        Vector2 ToScreen(Vector2 point) => camera.UnprojectPosition(point.ToVector3());

        Entity target = phase.Target;
        Vector2 targetCenter = target.Position.ToVector2();
        Vector2 corner = target.HalfSize + new Vector2(0.15f, 0.15f);
        Vector2[] box = [
            ToScreen(targetCenter + corner * new Vector2(-1, -1)), ToScreen(targetCenter + corner * new Vector2(1, -1)),
            ToScreen(targetCenter + corner * new Vector2(1, 1)), ToScreen(targetCenter + corner * new Vector2(-1, 1)),
            ToScreen(targetCenter + corner * new Vector2(-1, -1)),
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
        DrawArrow(ToScreen(center), ToScreen(center + playerDir), PlayerColor, PlayerLineWidth, PlayerHeadLength);
        DrawArrow(ToScreen(center), ToScreen(center + targetDir), TargetColor, TargetLineWidth, TargetHeadLength);
    }

    private void DrawArrow(Vector2 p1, Vector2 p2, Color color, float lineWidth, float headLength)
    {
        Vector2 dir = (p2 - p1).Normalized();
        DrawLine(p1, p2, color, lineWidth);
        DrawLine(p2, p2 - headLength * dir.Rotated(0.5f), color, lineWidth);
        DrawLine(p2, p2 - headLength * dir.Rotated(-0.5f), color, lineWidth);
    }
}
