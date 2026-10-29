using BodyCheck.Core.Entities;

namespace BodyCheck.Core.Controllers;

/// <summary>
/// How a <see cref="PatrolBehaviour"/> is tuned. Each value is a function that reads it from the features in effect when
/// it is used, so that a change to them takes effect at once. See the robot's, in <see cref="Features"/>, for what each
/// means.
/// </summary>
public sealed record PatrolTuning
{
    public required Func<float> StallSpeed { get; init; }
    public required Func<float> StallTime { get; init; }
    public required Func<float> StallHeat { get; init; }
    public required Func<float> StallMaxHeat { get; init; }
    public required Func<float> StallCooldownRate { get; init; }

    public static PatrolTuning Robot { get; } = new()
    {
        StallSpeed = () => Features.Current.RobotStallSpeed,
        StallTime = () => Features.Current.RobotStallTime,
        StallHeat = () => Features.Current.RobotStallHeat,
        StallMaxHeat = () => Features.Current.RobotStallMaxHeat,
        StallCooldownRate = () => Features.Current.RobotStallCooldownRate,
    };
}

/// <summary>Where a patrol would go, and which way it would face.</summary>
/// <param name="Move">How the walker should move, between -1 and 1.</param>
/// <param name="Facing">The way it should face: -1 for left, 1 for right, or 0, toward the camera.</param>
public readonly record struct PatrolIntent(float Move, float Facing);

/// <summary>
/// Patrolling: walking back and forth on tiles, turning round when stalled, and resting when stalled too often. It only
/// reads its walker, and says where to go, for the controller to act on.
/// </summary>
/// <param name="entity">The walker that patrols.</param>
/// <param name="tuning">How the behaviour is tuned.</param>
public sealed class PatrolBehaviour(WalkerEntity entity, PatrolTuning tuning)
{
    private const float ArbitraryFirstMove = 1;

    private float _move;
    private readonly StallDetector _stall = new();
    private readonly OverheatDetector _stallHeat = new();

    /// <summary>
    /// Patrols, or, if it doesn't on this tick, stands still. Off tiles, such as in the air or on another entity, it
    /// stands still and faces the camera, looking both ways. When stalled, it turns round, and when it has stalled too
    /// often, it rests facing the camera until it has cooled down.
    /// </summary>
    /// <param name="patrols">Whether it patrols on this tick, rather than standing still for something else.</param>
    /// <param name="facing">The way the walker faces now, which it goes on in.</param>
    /// <param name="gameDt">The tick's length in game time.</param>
    public PatrolIntent Apply(bool patrols, float facing, float gameDt)
    {
        _stallHeat.CoolDown(tuning.StallCooldownRate() * gameDt);

        if (!patrols)
        {
            // Stand still for whatever else is going on, then go on the way it faces.
            _move = 0;
            _stall.Reset();
            return new PatrolIntent(0, facing);
        }

        if (!entity.OnGround || entity.Support is not null)
        {
            _move = 0;
            _stall.Reset();
            return new PatrolIntent(0, 0);
        }

        bool contactLeft = entity.Contacts.HasFlag(Contacts.Left);
        bool contactRight = entity.Contacts.HasFlag(Contacts.Right);

        bool stalled = _stall.Update(
            entity.LinearVelocity.X * _move,
            gameDt,
            tuning.StallSpeed(),
            tuning.StallTime());

        if (stalled)
        {
            _stallHeat.HeatUp(tuning.StallHeat(), tuning.StallMaxHeat());
        }

        if (_stallHeat.Overheated)
        {
            facing = _move = 0;
            _stall.Reset();
        }
        else if (_move == 0)
        {
            facing = _move = contactLeft ? (contactRight ? 0 : 1) : contactRight ? -1 : facing != 0 ? facing : ArbitraryFirstMove;
            _stall.Reset();
        }
        else if (_move < 0 && stalled)
        {
            facing = _move = contactRight ? 0 : 1;
            _stall.Reset();
        }
        else if (_move > 0 && stalled)
        {
            facing = _move = contactLeft ? 0 : -1;
            _stall.Reset();
        }
        return new PatrolIntent(_move, facing);
    }
}
