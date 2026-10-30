using System;
using System.Collections.Generic;
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
/// Numbers have a sign and a fixed number of digits, so that they don't shift as they change. It belongs in
/// the window's own viewport, outside the <see cref="Look.WorldViewportContainer"/>, where it is drawn at the window's
/// full resolution and the colour pass doesn't touch it.
/// </para>
/// </summary>
public partial class DebugOverlay : CanvasLayer
{
    private readonly record struct Item(string Label, string Content)
    {
        public string Render(int labelWidth) => Label.PadRight(labelWidth) + " | " + Content;
    }

    private static readonly HashSet<Metric> CustomRender = [
        Metric.WorldRenderTime,
        Metric.Velocity,
        Metric.GroundSpeed,
        Metric.AirRise,
        Metric.AirRun,
        Metric.AirLand,
        Metric.AirTime,
        Metric.Feet,
        Metric.OnGround,
        // This is really for testing:
        Metric.PredictedVelocity,
        // Comment these lines out if diving into the guts of corner correction again:
        Metric.CornerCorrectionTime,
        Metric.CornerCorrectionCenter,
        Metric.CornerCorrectionNudge,
    ];
    private static readonly Metric[] Metrics = [.. Enum.GetValues<Metric>().Where(metric => !CustomRender.Contains(metric))];
    private static readonly string[] Labels = [.. Metrics.Select(metric => Words(metric.ToString()))];

    private readonly Label _label = new();
    private readonly List<Item> _items = [];

    public DebugOverlay()
    {
        Visible = false;

        _label.AddThemeFontOverride("font", new SystemFont { FontNames = ["Consolas", "Courier New", "monospace"] });
        _label.AddThemeFontSizeOverride("font_size", 14);

        var panel = new PanelContainer
        {
            MouseFilter = Control.MouseFilterEnum.Ignore,
            GrowHorizontal = Control.GrowDirection.Begin,
        };
        panel.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = new Color(0, 0, 0, 0.6f),
            ContentMarginLeft = 6,
            ContentMarginRight = 6,
            ContentMarginTop = 4,
            ContentMarginBottom = 4,
        });
        panel.SetAnchorsPreset(Control.LayoutPreset.TopRight);
        panel.AddChild(_label);
        AddChild(panel);
    }

    public override void _Ready()
    {
        // Stay active when the game is paused.
        ProcessMode = ProcessModeEnum.Always;
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

        var fps = (float)Engine.GetFramesPerSecond();
        var frameMs = (float)Performance.GetMonitor(Performance.Monitor.TimeProcess) * 1000;
        var physicsMs = (float)Performance.GetMonitor(Performance.Monitor.TimePhysicsProcess) * 1000;
        // If/when we add more views (e.g. UI), their respective render times should be vector-added here.
        var renderMs = (Telemetry.Get<Vector2>(Metric.WorldRenderTime) ?? Vector2.Zero) * 1000;
        var velocity = Telemetry.Get<Vector2>(Metric.Velocity) ?? Vector2.Zero;
        var speed = velocity.Length();
        var angle = Mathf.RadToDeg(velocity.Angle());
        var ground = Telemetry.Get<float>(Metric.GroundSpeed) ?? 0;
        var airRise = Telemetry.Get<float>(Metric.AirRise) ?? 0;
        var airRun = Telemetry.Get<float>(Metric.AirRun) ?? 0;
        var airLand = Telemetry.Get<float>(Metric.AirLand) ?? 0;
        var airTime = Telemetry.Get<float>(Metric.AirTime) ?? 0;
        var feet = Telemetry.Get<Vector2>(Metric.Feet) ?? Vector2.Zero;
        var onGround = (Telemetry.Get<bool>(Metric.OnGround) ?? false) ? "true " : "false";

        _items.Clear();

        _items.Add(new Item("Frame times", $"{Signed(fps, "000")}  All {Signed(frameMs, "00.00")}  Phys {Signed(physicsMs, "00.00")}  CPU {Signed(renderMs.X, "00.00")}  GPU {Signed(renderMs.Y, "00.00")}"));
        _items.Add(new Item("Velocity", $"Vec {Signed(velocity.X, "00.00")} {Signed(velocity.Y, "00.00")}  Mag {Signed(speed, "00.00")}  Angle {Signed(angle, "000")}  Gnd {Signed(ground, "00.00")}"));
        _items.Add(new Item("Last air", $"Rise {Signed(airRise, "00.00")}  Run {Signed(airRun, "00.00")}  Land {Signed(airLand, "00.00")}  Time {Signed(airTime, "00.000")}"));
        _items.Add(new Item("Feet", $"Vec {Signed(feet.X, "0000.00")} {Signed(feet.Y, "0000.00")}  OnGround {onGround}"));

        // Add all the leftover telemetry.
        foreach (var (label, metric) in Labels.Zip(Metrics))
        {
            _items.Add(new Item(label, Format(Telemetry.Get(metric))));
        }

        var labelWidth = _items.Max(item => item.Label.Length);

        _label.Text = string.Join('\n', _items.Select(item => item.Render(labelWidth)));
    }

    /// <summary>A name as words: "GroundSpeed" is "Ground speed".</summary>
    private static string Words(string name)
    {
        var words = new StringBuilder();
        foreach (char c in name)
        {
            if (char.IsUpper(c) && words.Length > 0)
            {
                words.Append(' ');
            }
            words.Append(words.Length > 0 ? char.ToLowerInvariant(c) : c);
        }
        return words.ToString();
    }

    private static string Format(object? value) => value switch
    {
        float number => Signed(number),
        int number => Signed(number, "0000"),
        Vector2 vector => $"{Signed(vector.X)} {Signed(vector.Y)}",
        Vector2I vector => $"{Signed(vector.X, "0000")} {Signed(vector.Y, "0000")}",
        bool flag => flag ? " true" : "false",
        _ => "?",
    };

    /// <summary>
    /// A number with a sign, or a space for zero, and a fixed number of digits, so that it keeps its width.
    /// </summary>
    private static string Signed(float value, string digits = "0000.00") =>
        value.ToString($"+{digits};-{digits}; {digits}", CultureInfo.InvariantCulture);
}
