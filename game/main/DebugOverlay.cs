using System;
using System.Globalization;
using System.Linq;
using System.Text;

using BodyCheck.Core;

using Godot;

namespace BodyCheck;

/// <summary>
/// Shows the frame rate and times, then every <see cref="Telemetry"/> value reported since the last reset,
/// in the top right corner. F3 switches it on and off.
/// <para>
/// Numbers have a sign and a fixed number of digits, so that they don't shift as they change. It is drawn
/// on a layer above the <see cref="Look.ColorPass"/>, which leaves it in full colour.
/// </para>
/// </summary>
public partial class DebugOverlay : CanvasLayer
{
    private static readonly Metric[] Metrics = Enum.GetValues<Metric>();
    private static readonly string[] Labels = Metrics.Select(metric => Words(metric.ToString())).ToArray();
    private static readonly int LabelWidth = Math.Max(Labels.Max(label => label.Length), "PHYSICS MS".Length);

    private readonly PixelText _label = new();
    private readonly StringBuilder _text = new();

    public DebugOverlay()
    {
        Layer = 2; // Above the colour pass, which is on layer 1.
        Visible = false;

        var panel = new PanelContainer
        {
            MouseFilter = Control.MouseFilterEnum.Ignore,
            GrowHorizontal = Control.GrowDirection.Begin,
        };
        panel.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = new Color(0, 0, 0, 0.6f),
            ContentMarginLeft = 2,
            ContentMarginRight = 2,
            ContentMarginTop = 1,
            ContentMarginBottom = 1,
        });
        panel.SetAnchorsPreset(Control.LayoutPreset.TopRight);
        panel.AddChild(_label);
        AddChild(panel);
    }

    public override void _UnhandledKeyInput(InputEvent @event)
    {
        if (@event is InputEventKey { Pressed: true, Echo: false, PhysicalKeycode: Key.F3 })
        {
            Visible = !Visible;
        }
    }

    public override void _Process(double delta)
    {
        if (!Visible)
        {
            return;
        }

        _text.Clear();
        Line("FPS", Signed((float)Engine.GetFramesPerSecond(), "0000"));
        Line("FRAME MS", Signed((float)delta * 1000));
        Line("PHYSICS MS", Signed((float)Performance.GetMonitor(Performance.Monitor.TimePhysicsProcess) * 1000));
        for (int i = 0; i < Metrics.Length; i++)
        {
            if (Telemetry.Get(Metrics[i]) is { } value)
            {
                Line(Labels[i], Format(value));
            }
        }
        _label.Text = _text.ToString().TrimEnd();
    }

    private void Line(string label, string value) =>
        _text.Append(label.PadRight(LabelWidth)).Append(' ').Append(value).Append('\n');

    /// <summary>A name in capitals, with a space between its words: "GroundSpeed" is "GROUND SPEED".</summary>
    private static string Words(string name)
    {
        var words = new StringBuilder();
        foreach (char c in name)
        {
            if (char.IsUpper(c) && words.Length > 0)
            {
                words.Append(' ');
            }
            words.Append(char.ToUpperInvariant(c));
        }
        return words.ToString();
    }

    private static string Format(TelemetryValue value) => value.Kind switch
    {
        TelemetryKind.Number => Signed(value.X),
        TelemetryKind.Count => Signed(value.X, "0000"),
        TelemetryKind.Pair => $"{Signed(value.X)} {Signed(value.Y)}",
        TelemetryKind.CountPair => $"{Signed(value.X, "0000")} {Signed(value.Y, "0000")}",
        TelemetryKind.Flag => value.X != 0 ? "YES" : "NO",
        _ => "?",
    };

    /// <summary>
    /// A number with a sign, or a space for zero, and a fixed number of digits, so that it keeps its width.
    /// </summary>
    private static string Signed(float value, string digits = "0000.00") =>
        value.ToString($"+{digits};-{digits}; {digits}", CultureInfo.InvariantCulture);
}
