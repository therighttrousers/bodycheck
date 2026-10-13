using Godot;

namespace BodyCheck.Look;

/// <summary>
/// Runs the colour shader over everything drawn below this layer: a rectangle that covers the screen,
/// whose shader reads what is behind it. It can also put a test card behind the rectangle, in place of the
/// game, which the shader turns into a picture of its palette.
/// </summary>
public partial class ColorPass : CanvasLayer
{
    private readonly ShaderMaterial _material = new() { Shader = GD.Load<Shader>("res://look/mode_x.gdshader") };

    private readonly ColorRect _paletteCard = new()
    {
        Material = new ShaderMaterial { Shader = GD.Load<Shader>("res://look/palette_card.gdshader") },
        MouseFilter = Control.MouseFilterEnum.Ignore,
        Visible = false,
    };

    public ColorPass()
    {
        // Added first, so that it is drawn before the rectangle that reads the screen.
        _paletteCard.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        AddChild(_paletteCard);

        var rect = new ColorRect { Material = _material, MouseFilter = Control.MouseFilterEnum.Ignore };
        rect.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        AddChild(rect);
    }

    public bool PaletteShown
    {
        get => _paletteCard.Visible;
        set => _paletteCard.Visible = value;
    }

    public bool Dither
    {
        set => _material.SetShaderParameter("dither", value);
    }
}
