using System;
using System.Collections.Generic;

using Godot;

namespace BodyCheck.Look;

/// <summary>
/// Switches for each part of the rendering pipeline, on the number keys, so that the look can be judged by eye:
/// <list type="bullet">
/// <item>1: low resolution on or off</item>
/// <item>3: animations in steps or not</item>
/// <item>4: colour pass on or off</item>
/// <item>5: dither on or off</item>
/// <item>6: the palette, in place of the game, on or off</item>
/// <item>7 and 8: the dither's lightness spread, down and up</item>
/// <item>9 and 0: the dither's chroma spread, down and up</item>
/// </list>
/// </summary>
public partial class LookSwitches : Node
{
    private static readonly Vector2I LowResolution = new(320, 240);

    private readonly WorldViewportContainer _worldView;
    private readonly DirectionalLight3D _light;
    private readonly Godot.Environment _environment;
    private readonly ColorPass _colorPass;
    private readonly AnimationStepper _animationStepper;

    private bool _lowRes = true;
    private bool _dither = true;
    private bool _stepAnimations = true;

    public LookSwitches(
        WorldViewportContainer view,
        DirectionalLight3D light,
        Godot.Environment environment,
        ColorPass colorPass,
        AnimationStepper animationStepper)
    {
        _worldView = view;
        _light = light;
        _environment = environment;
        _colorPass = colorPass;
        _animationStepper = animationStepper;
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
                _lowRes ? $"{LowResolution.X}x{LowResolution.Y}" : "native",
                _stepAnimations ? "stepped animation" : "smooth animation",
                _colorPass.Visible
                    ? (_dither ? $"dither {_colorPass.LightnessSpread:0.00}/{_colorPass.ChromaSpread:0.000}" : "no dither")
                    : "full colour",
            };
            return string.Join(", ", parts);
        }
    }

    public override void _Ready()
    {
        // Stay active when the game is paused.
        ProcessMode = ProcessModeEnum.Always;

        Apply();
    }

    public override void _UnhandledKeyInput(InputEvent @event)
    {
        if (@event is not InputEventKey { Pressed: true, Echo: false } key)
        {
            return;
        }

        switch (key.PhysicalKeycode)
        {
            case Key.Key1: _lowRes = !_lowRes; break;
            case Key.Key3: _stepAnimations = !_stepAnimations; break;
            case Key.Key4: _colorPass.Visible = !_colorPass.Visible; break;
            case Key.Key5: _dither = !_dither; break;
            case Key.Key6: _colorPass.PaletteShown = !_colorPass.PaletteShown; break;
            case Key.Key7: _colorPass.LightnessSpread -= 0.01f; break;
            case Key.Key8: _colorPass.LightnessSpread += 0.01f; break;
            case Key.Key9: _colorPass.ChromaSpread -= 0.005f; break;
            case Key.Key0: _colorPass.ChromaSpread += 0.005f; break;
            default: return;
        }
        Apply();
    }

    private void Apply()
    {
        _worldView.Resolution = _lowRes ? LowResolution : null;

        _colorPass.Dither = _dither;
        _animationStepper.Enabled = _stepAnimations;

        GD.Print($"Look: {Summary}");
        Changed?.Invoke();
    }
}
