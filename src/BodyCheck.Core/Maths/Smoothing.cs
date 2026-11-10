namespace BodyCheck.Core.Maths;

public static class Smoothing
{
    /// <summary>
    /// Moves <paramref name="current"/> toward <paramref name="target"/> for <paramref name="delta"/> seconds by
    /// exponential smoothing of time constant <paramref name="smoothTime"/>: the distance left shrinks by a factor of
    /// <c>e</c> in that time, so that it covers about 63% of the way, close to what <see cref="Spring.CriticallyDamped"/>
    /// covers in its smoothing time. It is a one-pole low-pass filter, or an exponential moving average, made exact for
    /// a frame of any length: two short frames move it as far as one long one.
    /// </summary>
    public static float Exponential(float current, float target, float smoothTime, float delta) =>
        target + (current - target) * MathF.Exp(-delta / smoothTime);
}
