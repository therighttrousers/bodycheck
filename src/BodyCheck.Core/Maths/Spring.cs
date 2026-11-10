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

    /// <summary>
    /// The force that makes a body of <paramref name="mass"/>, at <paramref name="position"/> and moving at
    /// <paramref name="velocity"/>, follow a critically damped spring of smoothing time <paramref name="smoothTime"/>
    /// toward <paramref name="goal"/>, as in <see cref="CriticallyDamped"/>, along one axis. It is a PD controller,
    /// <c>K_p (goal - position) - K_d velocity</c>, with <c>K_p = m w²</c> and <c>K_d = 2 m w</c>, which damp it
    /// critically. Being proportional to the mass, it moves a body of any mass alike.
    /// </summary>
    public static float CriticallyDampedForce(float position, float velocity, float goal, float smoothTime, float mass)
    {
        float w = 2 / smoothTime;
        return mass * w * (w * (goal - position) - 2 * velocity);
    }

    /// <summary>
    /// The smoothing time of a critically damped spring that, starting at rest <paramref name="distance"/> from its
    /// goal, is fastest at <paramref name="speed"/>. From rest, its speed is <c>w² x0 t e^(-w t)</c>, which peaks at
    /// <c>t = 1 / w</c>, at <c>w x0 / e</c>, or <c>2 x0 / (e T)</c>.
    /// </summary>
    public static float SmoothTimeForPeakSpeed(float distance, float speed) => 2 * distance / (MathF.E * speed);
}
