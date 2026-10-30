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
    /// <summary>The velocity the player's controller predicts for this tick's step, for corner correction.</summary>
    PredictedVelocity,
    /// <summary>When the last corner correction was made, in real seconds since the engine started.</summary>
    CornerCorrectionTime,
    /// <summary>Where the center of the entity that the last corner correction nudged was before the nudge.</summary>
    CornerCorrectionCenter,
    /// <summary>How far the last corner correction nudged its entity, and which way.</summary>
    CornerCorrectionNudge,

    // The player's last time in the air, reported on landing. Measured from its feet on the last tick on
    // the ground.
    /// <summary>How far the feet rose, to the highest point.</summary>
    AirRise,
    /// <summary>How far the feet moved along X.</summary>
    AirRun,
    /// <summary>How far the feet ended up above where they started; negative for below.</summary>
    AirLand,
    /// <summary>How long it was in the air, in game seconds.</summary>
    AirTime,

    /// <summary>The tile under the mouse.</summary>
    MouseTile,
}

/// <summary>
/// Measurements that game code pushes, for whatever wants to show or check them, such as the debug overlay
/// and tests. Each <see cref="Metric"/> keeps the last value reported for it until it is reported again or
/// the telemetry is reset, so a value reported once, such as a jump's height on landing, stays readable.
/// <para>
/// Reporting only boxes a value and stores it, so it costs next to nothing each tick, whether or not anything reads it.
/// </para>
/// <para>
/// <b>Prefer to record the game equivalents of SI units</b>; e.g. seconds, meters (tiles), kilograms.
/// </para>
/// <para>
/// <b>Use this only for observing internal state outside of normal gameplay.</b> Think of it as a logger.
/// </para>
/// </summary>
public static class Telemetry
{
    private static readonly object?[] Values = new object?[Enum.GetValues<Metric>().Length];

    public static void Report<T>(Metric metric, T value) where T : struct => Values[(int)metric] = value;

    /// <summary>The last value reported for a metric, or null if none has been since the last reset.</summary>
    public static object? Get(Metric metric) => Values[(int)metric];

    /// <summary>The last value reported for a metric, or null if none has been since the last reset or it isn't a T.</summary>
    public static T? Get<T>(Metric metric) where T : struct => Get(metric) is T value ? value : null;

    /// <summary>Forgets every value, e.g. when a new room is loaded.</summary>
    public static void Reset() => Array.Clear(Values);
}
