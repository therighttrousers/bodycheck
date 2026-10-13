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

    private float _lightnessSpread = 0.07f;
    private float _chromaSpread = 0.015f;

    public ColorPass()
    {
        Image palette = Palette.CreateImage();
        _material.SetShaderParameter("palette", ImageTexture.CreateFromImage(palette));
        _material.SetShaderParameter("palette_lab", ImageTexture.CreateFromImage(Palette.ToOklab(palette)));
        LightnessSpread = _lightnessSpread;
        ChromaSpread = _chromaSpread;

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

    /// <summary>How far the dither reaches along lightness, in Oklab.</summary>
    public float LightnessSpread
    {
        get => _lightnessSpread;
        set
        {
            _lightnessSpread = Mathf.Max(value, 0);
            _material.SetShaderParameter("lightness_spread", _lightnessSpread);
        }
    }

    /// <summary>How far the dither reaches along each of a and b, in Oklab.</summary>
    public float ChromaSpread
    {
        get => _chromaSpread;
        set
        {
            _chromaSpread = Mathf.Max(value, 0);
            _material.SetShaderParameter("chroma_spread", _chromaSpread);
        }
    }
}
