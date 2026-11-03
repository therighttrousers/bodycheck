using System;

using BodyCheck.Core;

using Godot;

namespace BodyCheck;

/// <summary>
/// A placeholder for the menu: a panel that says the game is paused. It belongs in the game's viewport, on a layer
/// above the colour pass, and is centred in the viewport at whatever size it is.
/// </summary>
public partial class PauseMenuOverlay : CanvasLayer
{
    public PauseMenuOverlay()
    {
        Layer = 2; // Above the colour pass, so the dither leaves it alone.

        var center = new CenterContainer();
        center.SetAnchorsPreset(Control.LayoutPreset.FullRect);
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
            AntiAliasing = false,
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
    }
}
