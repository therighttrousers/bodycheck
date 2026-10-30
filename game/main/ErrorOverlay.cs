using System.Collections.Generic;

using Godot;

namespace BodyCheck;

/// <summary>
/// Shows the errors from the last time the game read its maps and configurations, in the bottom left corner, so that a
/// mistake in a file doesn't stop play. Hidden when there are none. Drawn at the window's
/// full resolution, like the <see cref="DebugOverlay"/>.
/// </summary>
public partial class ErrorOverlay : CanvasLayer
{
    private readonly Label _label = new() { AutowrapMode = TextServer.AutowrapMode.WordSmart };

    public ErrorOverlay()
    {
        Visible = false;
        Layer = 2;

        _label.AddThemeColorOverride("font_color", new Color(1, 0.45f, 0.4f));
        _label.AddThemeFontSizeOverride("font_size", 16);

        var panel = new PanelContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        panel.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = new Color(0, 0, 0, 0.75f),
            ContentMarginLeft = 8,
            ContentMarginRight = 8,
            ContentMarginTop = 6,
            ContentMarginBottom = 6,
        });
        panel.SetAnchorsPreset(Control.LayoutPreset.BottomWide);
        panel.GrowVertical = Control.GrowDirection.Begin;
        panel.AddChild(_label);
        AddChild(panel);
    }

    /// <summary>Shows these errors in place of any shown before.</summary>
    public void ShowErrors(IReadOnlyList<string> errors)
    {
        _label.Text = string.Join("\n", errors);
        Visible = errors.Count > 0;
    }
}
