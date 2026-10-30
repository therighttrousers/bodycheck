using BodyCheck.Core.Entities;

using Godot;

namespace BodyCheck.Core.Controllers;

/// <summary>
/// How a <see cref="JumpAction"/> is tuned. Each value is a function that reads it from the features in effect when it
/// is used, so that a change to them takes effect at once. See the player's, in <see cref="Features"/>, for what each
/// means.
/// </summary>
public sealed record JumpTuning
{
    public required Func<float> Speed { get; init; }
    public required Func<float> ThrustTime { get; init; }
    public required Func<float> ThrustFrac { get; init; }

    public static JumpTuning Player { get; } = new()
    {
        Speed = () => Features.Current.PlayerJumpSpeed,
        ThrustTime = () => Features.Current.PlayerJumpThrustTime,
        ThrustFrac = () => Features.Current.PlayerJumpThrustFrac,
    };
}

/// <summary>
/// Jumping: an impulse off what the entity stands on, then thrust for as long as jump is held, up to the thrust time.
/// When to jump is the controller's to say, such as after the player's coyote time or a buffered press.
/// </summary>
/// <param name="entity">The walker that jumps.</param>
/// <param name="tuning">How the action is tuned.</param>
public sealed class JumpAction(WalkerEntity entity, JumpTuning tuning)
{
    // How the entity's ground moved on its last tick on it, which a jump's speed is relative to.
    private Vector3 _lastGroundVelocity = Vector3.Zero;

    // In game time: how much longer the jump's thrust may last, after this tick.
    private float _thrustTimeLeft;

    /// <summary>
    /// Whether the jump's thrust time has yet to run out, from the tick of the jump until the time is up or something ends
    /// it, whether or not jump is held. A jump can't begin until it has.
    /// </summary>
    public bool Thrusting => _thrustTimeLeft > 0;

    /// <param name="jumps">Whether the entity jumps on this tick.</param>
    /// <param name="held">Whether jump is held, which keeps up the thrust.</param>
    /// <param name="gameDt">The tick's length in game time.</param>
    /// <returns>
    /// The change in the entity's velocity from the jump's impulse or thrust, which shows only after the engine's step.
    /// What the entity carries is pushed up with it, so it shares the change.
    /// </returns>
    public Vector3 Apply(bool jumps, bool held, float gameDt)
    {
        if (entity.OnGround)
        {
            _lastGroundVelocity = entity.GroundVelocity;
        }
        Vector3 deltaV = Step(jumps, held, gameDt);
        _thrustTimeLeft = Math.Max(0, _thrustTimeLeft - gameDt);
        return deltaV;
    }

    /// <summary>Ends the jump's thrust, as when something else takes over the entity, such as a phase.</summary>
    public void Interrupt()
    {
        _thrustTimeLeft = 0;
    }

    private Vector3 Step(bool jumps, bool held, float gameDt)
    {
        if (jumps)
        {
            // The jump speed is relative to the ground, as it moved on the last tick on it: whatever the entity
            // gained or lost against it since, from falling in coyote ticks or being pushed off it, is undone.
            var relativeVelocityY = entity.LinearVelocity.Y - _lastGroundVelocity.Y;
            var impulse = new Vector3(0, entity.Mass * (tuning.Speed() - relativeVelocityY), 0);
            entity.ApplyCentralImpulse(impulse);
            entity.Support?.ApplyCentralImpulse(-impulse);
            _thrustTimeLeft = tuning.ThrustTime() + gameDt; // + gameDt to include this tick

            entity.RaiseCue(EntityCue.Jumped);

            // Thrust starts on the next tick.
            return impulse / entity.LoadedMass;
        }

        if (!held)
        {
            return Vector3.Zero;
        }

        // If stopped rising (e.g. hit a ceiling), cancel thrust.
        if (entity.LinearVelocity.Y <= 0)
        {
            _thrustTimeLeft = 0;
        }

        if (_thrustTimeLeft > 0)
        {
            // The last tick of thrust gets only the part of it that the thrust time covers, so that the thrust adds the
            // same speed whatever the ticks' length in game time.
            float share = Math.Min(1, _thrustTimeLeft / gameDt);
            var force = new Vector3(0, share * tuning.ThrustFrac() * entity.Weight, 0);
            entity.ApplyCentralForce(force);
            entity.Support?.ApplyCentralForce(-force);
            return force * gameDt / entity.LoadedMass;
        }
        return Vector3.Zero;
    }
}
