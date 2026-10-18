using BodyCheck.Core.Controllers;
using BodyCheck.Core.Entities;
using BodyCheck.Core.Simulation;
using BodyCheck.Look;

using Godot;

namespace BodyCheck;

/// <summary>
/// A placeholder for showing a phase being aimed: a mark around the target, and an arrow in the direction held,
/// centered on the center of mass of the player and the target. Like the <see cref="ShapeWireframes"/>, it is drawn
/// over the game at the window's full resolution, and it draws while the game is paused for the aim.
/// </summary>
public partial class PhaseAimMarker : Node2D
{
    private static readonly Color MarkColor = new(1, 0.85f, 0.2f);
    private const float LineWidth = 3;
    private const float HeadLength = 0.5f;

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
        if (camera is null || World?.PlayerController.Aim is not PhaseAim aim)
        {
            return;
        }

        // From the plane of play to the window's pixels, where the container draws the game scaled up.
        Transform2D toWindow = _worldView.GetGlobalTransform();
        Vector2 ToWindow(Vector2 point) => toWindow * camera.UnprojectPosition(new Vector3(point.X, point.Y, 0));

        Entity target = aim.Target;
        Vector2 targetCenter = new(target.Position.X, target.Position.Y);
        Vector2 corner = target.HalfSize + new Vector2(0.15f, 0.15f);
        Vector2[] box = [
            ToWindow(targetCenter + corner * new Vector2(-1, -1)), ToWindow(targetCenter + corner * new Vector2(1, -1)),
            ToWindow(targetCenter + corner * new Vector2(1, 1)), ToWindow(targetCenter + corner * new Vector2(-1, 1)),
            ToWindow(targetCenter + corner * new Vector2(-1, -1)),
        ];
        DrawPolyline(box, MarkColor, LineWidth);

        if (aim.Direction == Vector2.Zero)
        {
            return;
        }

        Vector2 center = new(aim.Center.X, aim.Center.Y);
        Vector2 half = World.PlayerTuning.PhaseMinSeparation / 2 * aim.Direction;
        Vector2 tip = center + half;
        DrawLine(ToWindow(center - half), ToWindow(tip), MarkColor, LineWidth);
        DrawLine(ToWindow(tip), ToWindow(tip - HeadLength * aim.Direction.Rotated(0.5f)), MarkColor, LineWidth);
        DrawLine(ToWindow(tip), ToWindow(tip - HeadLength * aim.Direction.Rotated(-0.5f)), MarkColor, LineWidth);
    }
}
