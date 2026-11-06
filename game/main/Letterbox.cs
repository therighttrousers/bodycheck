using System;

using Godot;

namespace BodyCheck;

/// <summary>
/// Black bars across the top and bottom of the game's view, which say that the player's control has been taken away,
/// as in a scripted cutscene. While <see cref="Shown"/>, they slide in from the edges, and otherwise slide out, over
/// <see cref="SlideTime"/> real seconds, in whole pixels.
/// <para>
/// It belongs in the game's viewport. It is on the layer below the colour pass, so that the bars are part of the
/// picture, under the dialogue box and every other overlay.
/// </para>
/// </summary>
public partial class Letterbox : CanvasLayer
{
    /// <summary>Each bar's height when shown, in the game viewport's pixels.</summary>
    private const int BarHeight = 24;

    /// <summary>How long, in real seconds, the bars take to slide all the way in or out.</summary>
    private const float SlideTime = 0.25f;

    private readonly ColorRect _top = new() { Color = Colors.Black, MouseFilter = Control.MouseFilterEnum.Ignore };
    private readonly ColorRect _bottom = new() { Color = Colors.Black, MouseFilter = Control.MouseFilterEnum.Ignore };

    // How far the bars are in, from 0, out of view, to 1, all the way in.
    private float _amount;

    public Letterbox()
    {
        Layer = 0; // Below the colour pass, which is on 1.

        _top.SetAnchorsPreset(Control.LayoutPreset.TopWide);
        AddChild(_top);

        _bottom.SetAnchorsPreset(Control.LayoutPreset.BottomWide);
        _bottom.GrowVertical = Control.GrowDirection.Begin;
        AddChild(_bottom);

        SetBarHeights();
    }

    /// <summary>Whether the bars are to be in view. They slide toward it.</summary>
    public bool Shown { get; set; }

    public override void _Process(double delta)
    {
        // In real time, so that a slowdown doesn't slow the slide.
        float realDt = (float)(delta / Engine.TimeScale);
        float amount = Mathf.MoveToward(_amount, Shown ? 1 : 0, realDt / SlideTime);
        if (amount != _amount)
        {
            _amount = amount;
            SetBarHeights();
        }
    }

    private void SetBarHeights()
    {
        int height = (int)MathF.Round(BarHeight * _amount);
        _top.OffsetBottom = height;
        _bottom.OffsetTop = -height;
        _top.Visible = _bottom.Visible = height > 0;
    }
}
