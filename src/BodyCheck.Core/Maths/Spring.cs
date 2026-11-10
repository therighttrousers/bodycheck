using Godot;

namespace BodyCheck.Core.Maths;

public sealed class Spring
{
    /// <summary>
    /// Moves <paramref name="position"/>, at <paramref name="velocity"/>, toward <paramref name="goal"/> for
    /// <paramref name="delta"/> seconds, as a critically damped spring of smoothing time <paramref name="smoothTime"/>
    /// does: its frequency <c>w</c> is <c>2 / smoothTime</c>, so that it covers about 60% of the way in that time. This
    /// is the spring's exact solution, which holds for a frame of any length, rather than a step of an integrator,
    /// which goes wrong on a long one. With <c>x</c> its distance from the goal and <c>v</c> its velocity, after a time
    /// <c>t</c>, <c>x</c> is <c>(x0 + (v0 + w x0) t) e^(-w t)</c>, and <c>v</c> is <c>(v0 - w (v0 + w x0) t) e^(-w t)</c>.
    /// </summary>
    public static (Vector2 Position, Vector2 Velocity) CriticallyDamped(
        Vector2 position, Vector2 velocity, Vector2 goal, float smoothTime, float delta)
    {
        float w = 2 / smoothTime;
        float decay = MathF.Exp(-w * delta);
        Vector2 x = position - goal;
        Vector2 a = velocity + w * x;
        return (goal + (x + a * delta) * decay, (velocity - w * delta * a) * decay);
    }
}
