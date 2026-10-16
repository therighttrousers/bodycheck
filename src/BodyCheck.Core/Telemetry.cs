using System;

using Godot;

namespace BodyCheck.Core;

/// <summary>
/// The measurements that game code reports to <see cref="Telemetry"/>, in the order they are shown.
/// </summary>
public enum Metric
{
    /// <summary>The player's feet: the middle of the bottom of its box.</summary>
    Feet,
    Velocity,
    Speed,
    /// <summary>The angle of the velocity, in degrees anticlockwise from +X.</summary>
    Angle,
    /// <summary>Speed along X relative to what the player stands on.</summary>
    GroundSpeed,
    OnGround,
    CoyoteTicks,
    ThrustTicks,

    // The player's last time in the air, reported on landing. Measured from its feet on the last tick on
    // the ground.
    /// <summary>How far the feet rose, to the highest point.</summary>
    AirRise,
    /// <summary>How far the feet moved along X.</summary>
    AirRun,
    /// <summary>How far the feet ended up above where they started; negative for below.</summary>
    AirLand,
    AirTicks,

    /// <summary>The tile under the mouse.</summary>
    MouseTile,
}

/// <summary>How a <see cref="TelemetryValue"/> is to be read.</summary>
public enum TelemetryKind
{
    Number,
    Count,
    Pair,
    CountPair,
    Flag,
}

/// <summary>One reported value. A count and a flag are kept in <see cref="X"/>; a flag is 1 or 0.</summary>
public readonly record struct TelemetryValue(TelemetryKind Kind, float X, float Y = 0)
{
    public Vector2 Pair => new(X, Y);
}

/// <summary>
/// Measurements that game code pushes, for whatever wants to show or check them, such as the debug overlay
/// and tests. Each <see cref="Metric"/> keeps the last value reported for it until it is reported again or
/// the telemetry is reset, so a value reported once, such as a jump's height on landing, stays readable.
/// <para>
/// Reporting stores a value in place, so it costs next to nothing each tick, whether or not anything reads it.
/// </para>
/// </summary>
public static class Telemetry
{
    private static readonly TelemetryValue?[] Values = new TelemetryValue?[Enum.GetValues<Metric>().Length];

    public static void Report(Metric metric, float value) => Set(metric, new(TelemetryKind.Number, value));

    public static void Report(Metric metric, int value) => Set(metric, new(TelemetryKind.Count, value));

    public static void Report(Metric metric, Vector2 value) => Set(metric, new(TelemetryKind.Pair, value.X, value.Y));

    public static void Report(Metric metric, Vector2I value) => Set(metric, new(TelemetryKind.CountPair, value.X, value.Y));

    public static void Report(Metric metric, bool value) => Set(metric, new(TelemetryKind.Flag, value ? 1 : 0));

    /// <summary>The last value reported for a metric, or null if none has been since the last reset.</summary>
    public static TelemetryValue? Get(Metric metric) => Values[(int)metric];

    /// <summary>Forgets every value, e.g. when a new room is loaded.</summary>
    public static void Reset() => Array.Clear(Values);

    private static void Set(Metric metric, TelemetryValue value) => Values[(int)metric] = value;
}
