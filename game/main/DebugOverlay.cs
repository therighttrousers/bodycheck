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
        Metric.CoyoteTime,
        Metric.ThrustTime,
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
        var renderMs = (Telemetry.GetVector2(Metric.WorldRenderTime) ?? Vector2.Zero) * 1000;
        var velocity = Telemetry.GetVector2(Metric.Velocity) ?? Vector2.Zero;
        var speed = velocity.Length();
        var angle = Mathf.RadToDeg(velocity.Angle());
        var ground = Telemetry.GetFloat(Metric.GroundSpeed) ?? 0;
        var airRise = Telemetry.GetFloat(Metric.AirRise) ?? 0;
        var airRun = Telemetry.GetFloat(Metric.AirRun) ?? 0;
        var airLand = Telemetry.GetFloat(Metric.AirLand) ?? 0;
        var airTime = Telemetry.GetFloat(Metric.AirTime) ?? 0;
        var feet = Telemetry.GetVector2(Metric.Feet) ?? Vector2.Zero;
        var onGround = (Telemetry.GetBool(Metric.OnGround) ?? false) ? "true " : "false";
        var coyoteTime = Telemetry.GetFloat(Metric.CoyoteTime) ?? 0;
        var thrustTime = Telemetry.GetFloat(Metric.ThrustTime) ?? 0;

        _items.Clear();

        _items.Add(new Item("Frame times", $"{Signed(fps, "000")}  All {Signed(frameMs, "00.00")}  Phys {Signed(physicsMs, "00.00")}  CPU {Signed(renderMs.X, "00.00")}  GPU {Signed(renderMs.Y, "00.00")}"));
        _items.Add(new Item("Velocity", $"Vec {Signed(velocity.X, "00.00")} {Signed(velocity.Y, "00.00")}  Mag {Signed(speed, "00.00")}  Angle {Signed(angle, "000")}  Gnd {Signed(ground, "00.00")}"));
        _items.Add(new Item("Last air", $"Rise {Signed(airRise, "00.00")}  Run {Signed(airRun, "00.00")}  Land {Signed(airLand, "00.00")}  Time {Signed(airTime, "00.000")}"));
        _items.Add(new Item("Feet", $"Vec {Signed(feet.X, "0000.00")} {Signed(feet.Y, "0000.00")}  OnGround {onGround}"));
        _items.Add(new Item("Leniency", $"Coyote {Signed(coyoteTime, "0.000")}  Thrust {Signed(thrustTime, "0.000")}"));

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

    private static string Format(TelemetryValue? value) => value?.Kind switch
    {
        TelemetryKind.Float => Signed(value.Value.X),
        TelemetryKind.Int => Signed(value.Value.X, "0000"),
        TelemetryKind.Vector2 => $"{Signed(value.Value.X)} {Signed(value.Value.Y)}",
        TelemetryKind.Vector2I => $"{Signed(value.Value.X, "0000")} {Signed(value.Value.Y, "0000")}",
        TelemetryKind.Bool => value.Value.X != 0 ? " true" : "false",
        _ => "?",
    };

    /// <summary>
    /// A number with a sign, or a space for zero, and a fixed number of digits, so that it keeps its width.
    /// </summary>
    private static string Signed(float value, string digits = "0000.00") =>
        value.ToString($"+{digits};-{digits}; {digits}", CultureInfo.InvariantCulture);
}
