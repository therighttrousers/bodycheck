using System;
using System.Collections.Generic;

using Godot;

namespace BodyCheck.Look;

/// <summary>
/// Switches for each part of the rendering pipeline, on the number keys, so that the look can be judged
/// by eye (decision records 003 and 004):
/// <list type="bullet">
/// <item>1: low resolution on or off</item>
/// <item>2: next low-resolution size</item>
/// <item>3: next anti-aliasing level</item>
/// <item>4: colour pass on or off</item>
/// <item>5: dither on or off</item>
/// <item>6: the palette, in place of the game, on or off</item>
/// <item>[ and ]: the dither's lightness spread, down and up</item>
/// <item>; and ': the dither's chroma spread, down and up</item>
/// </list>
/// </summary>
public partial class LookSwitches : Node
{
    private static readonly Vector2I[] Sizes = [new(320, 240), new(256, 224), new(384, 216), new(640, 480)];
    private static readonly Viewport.Msaa[] MsaaLevels =
        [Viewport.Msaa.Disabled, Viewport.Msaa.Msaa2X, Viewport.Msaa.Msaa4X, Viewport.Msaa.Msaa8X];

    private readonly DirectionalLight3D _light;
    private readonly Godot.Environment _environment;
    private readonly ColorPass _colorPass;

    private bool _lowRes = true;
    private int _size;
    private int _msaa = 1;
    private bool _dither = true;

    public LookSwitches(DirectionalLight3D light, Godot.Environment environment, ColorPass colorPass)
    {
        _light = light;
        _environment = environment;
        _colorPass = colorPass;
    }

    /// <summary>Raised after any switch changes. The size of the viewport may have changed with it.</summary>
    public event Action? Changed;

    /// <summary>The state of every switch, for showing to the user.</summary>
    public string Summary
    {
        get
        {
            var parts = new List<string>
            {
                _lowRes ? $"{Sizes[_size].X}x{Sizes[_size].Y}" : "native",
                MsaaLevels[_msaa] == Viewport.Msaa.Disabled ? "no AA" : $"MSAA {1 << (int)MsaaLevels[_msaa]}x",
                _colorPass.Visible
                    ? (_dither ? $"dither {_colorPass.LightnessSpread:0.00}/{_colorPass.ChromaSpread:0.000}" : "no dither")
                    : "full colour",
            };
            if (_light.ShadowEnabled)
            {
                parts.Add("shadows");
            }
            if (_environment.SsaoEnabled)
            {
                parts.Add("AO");
            }
            if (_environment.SsrEnabled)
            {
                parts.Add("reflections");
            }
            if (_environment.GlowEnabled)
            {
                parts.Add("glow");
            }
            return string.Join(", ", parts);
        }
    }

    public override void _Ready() => Apply();

    public override void _UnhandledKeyInput(InputEvent @event)
    {
        if (@event is not InputEventKey { Pressed: true, Echo: false } key)
        {
            return;
        }

        switch (key.PhysicalKeycode)
        {
            case Key.Key1: _lowRes = !_lowRes; break;
            case Key.Key2: _size = (_size + 1) % Sizes.Length; break;
            case Key.Key3: _msaa = (_msaa + 1) % MsaaLevels.Length; break;
            case Key.Key4: _colorPass.Visible = !_colorPass.Visible; break;
            case Key.Key5: _dither = !_dither; break;
            case Key.Key6: _colorPass.PaletteShown = !_colorPass.PaletteShown; break;
            case Key.Bracketleft: _colorPass.LightnessSpread -= 0.01f; break;
            case Key.Bracketright: _colorPass.LightnessSpread += 0.01f; break;
            case Key.Semicolon: _colorPass.ChromaSpread -= 0.005f; break;
            case Key.Apostrophe: _colorPass.ChromaSpread += 0.005f; break;
            default: return;
        }
        Apply();
    }

    private void Apply()
    {
        // The project settings start the window in this mode. Disabled draws at the window's own size.
        Window window = GetWindow();
        window.ContentScaleMode = _lowRes ? Window.ContentScaleModeEnum.Viewport : Window.ContentScaleModeEnum.Disabled;
        window.ContentScaleSize = Sizes[_size];
        window.Msaa3D = MsaaLevels[_msaa];

        _colorPass.Dither = _dither;

        GD.Print($"Look: {Summary}");
        Changed?.Invoke();
    }
}
