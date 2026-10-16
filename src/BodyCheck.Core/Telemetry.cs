using Godot;

namespace BodyCheck.Core;

/// <summary>
/// The measurements that game code reports to <see cref="Telemetry"/>, in the order they are shown.
/// </summary>
public enum Metric
{
    /// <summary>CPU and GPU seconds to render the world viewport.</summary>
    WorldRenderTime,

    /// <summary>The player's feet: the middle of the bottom of its box.</summary>
    Feet,
    Velocity,
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
    Float,
    Int,
    Vector2,
    Vector2I,
    Bool,
}

/// <summary>One reported value. An int and a bool are kept in <see cref="X"/>; a bool is 1 or 0.</summary>
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
/// <para>
/// <b>Prefer to record the game equivalents of SI units</b>; e.g. seconds, meters (tiles), kilograms.
/// </para>
/// </summary>
public static class Telemetry
{
    private static readonly TelemetryValue?[] Values = new TelemetryValue?[Enum.GetValues<Metric>().Length];

    public static void Report(Metric metric, float value) => Set(metric, new(TelemetryKind.Float, value));

    public static void Report(Metric metric, int value) => Set(metric, new(TelemetryKind.Int, value));

    public static void Report(Metric metric, Vector2 value) => Set(metric, new(TelemetryKind.Vector2, value.X, value.Y));

    public static void Report(Metric metric, Vector2I value) => Set(metric, new(TelemetryKind.Vector2I, value.X, value.Y));

    public static void Report(Metric metric, bool value) => Set(metric, new(TelemetryKind.Bool, value ? 1 : 0));

    /// <summary>The last value reported for a metric, or null if none has been since the last reset.</summary>
    public static TelemetryValue? Get(Metric metric) => Values[(int)metric];

    public static float? GetFloat(Metric metric)
    {
        var value = Get(metric);
        return value?.Kind switch { TelemetryKind.Float => value.Value.Pair.X, _ => null };
    }

    public static int? GetInt(Metric metric)
    {
        var value = Get(metric);
        return value?.Kind switch { TelemetryKind.Int => (int)value.Value.Pair.X, _ => null };
    }

    public static Vector2? GetVector2(Metric metric)
    {
        var value = Get(metric);
        return value?.Kind switch { TelemetryKind.Vector2 => value.Value.Pair, _ => null };
    }

    public static Vector2I? GetVector2I(Metric metric)
    {
        var value = Get(metric);
        return value?.Kind switch
        {
            TelemetryKind.Vector2I => new Vector2I((int)value.Value.Pair.X, (int)value.Value.Pair.Y),
            _ => null
        };
    }

    public static bool? GetBool(Metric metric)
    {
        var value = Get(metric);
        return value?.Kind switch { TelemetryKind.Bool => value.Value.Pair.X != 0, _ => null };
    }

    /// <summary>Forgets every value, e.g. when a new room is loaded.</summary>
    public static void Reset() => Array.Clear(Values);

    private static void Set(Metric metric, TelemetryValue value) => Values[(int)metric] = value;
}
