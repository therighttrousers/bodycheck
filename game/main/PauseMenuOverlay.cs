using System;

using BodyCheck.Core;

using Godot;

namespace BodyCheck;

/// <summary>
/// A full-viewport overlay meant to show when the game is paused. Its contents are size 320x240 and scaled to fit
/// the viewport.
/// </summary>
public partial class PauseMenuOverlay : CanvasLayer
{
    public PauseMenuOverlay()
    {
        Layer = 2; // Above the colour pass, so the dither leaves it alone.

        var center = new CenterContainer { Size = Constants.Resolution };
        AddChild(center);

        var panel = new PanelContainer();
        panel.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = new Color(0.5f, 0.5f, 0.4f, 0.6f),
            CornerRadiusBottomLeft = 10,
            CornerRadiusBottomRight = 10,
            CornerRadiusTopLeft = 10,
            CornerRadiusTopRight = 10,
            ContentMarginLeft = 10,
            ContentMarginRight = 10,
            ContentMarginTop = 10,
            ContentMarginBottom = 10,
        });
        center.AddChild(panel);

        var label = new Label()
        {
            Text = "Paused (menu placeholder)",
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        panel.AddChild(label);
    }

    public override void _Ready()
    {
        // The pause menu needs to stay active when the game is paused.
        ProcessMode = ProcessModeEnum.Always;

        var viewport = GetViewport();
        viewport?.SizeChanged += () =>
        {
            var size = viewport.GetVisibleRect().Size;
            var scale = Math.Min(size.X / Constants.Resolution.X, size.Y / Constants.Resolution.Y);
            GD.Print($"New scale = {scale}");
            Scale = new Vector2(scale, scale);
        };
    }
}
