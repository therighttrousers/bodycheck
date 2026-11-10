using BodyCheck.Core.Entities;
using BodyCheck.Core.Maths;

using Godot;

namespace BodyCheck.Core.Controllers;

/// <summary>
/// Moves a platform back and forth along a line between two points, in any direction. It thrusts three ways: against
/// gravity, cancelling its own weight; along the line, toward the end it is heading for; and across the line, back
/// onto it. The last two are capped, so a heavy enough load overpowers them. It publishes how much of them goes to
/// fighting what pushes the platform around, as <see cref="PlatformEntity.Strain"/>.
/// </summary>
public sealed class PlatformController : IController<PlatformEntity>
{
    private readonly PlatformEntity _entity;
    private readonly Vector2 _rangeA;
    private readonly Vector2 _rangeB;
    private readonly float _length;
    /// <summary>The direction from A to B.</summary>
    private readonly Vector2 _along;
    /// <summary>A direction perpendicular to the line.</summary>
    private readonly Vector2 _across;
    private bool _towardB = true;
    private readonly StallDetector _stall = new(new StallTuning
    {
        MinSpeed = () => Features.Current.PlatformStallSpeed,
        MaxTime = () => Features.Current.PlatformStallTime,
    });
    // The last tick's drive, the way it was heading, and what it had to work with, to tell what the drive achieved.
    private float _lastDrive;
    private Vector2 _lastHeading;
    private Vector2 _lastVelocity;
    private float _lastDt;

    /// <param name="entity">The platform, which the controller controls.</param>
    /// <param name="rangeA">One end of the line that the platform's center moves along.</param>
    /// <param name="rangeB">
    /// The other end, which the platform heads for first. Must not be the same as <paramref name="rangeA"/>.
    /// </param>
    public PlatformController(PlatformEntity entity, Vector2 rangeA, Vector2 rangeB)
    {
        _entity = entity;
        _rangeA = rangeA;
        _rangeB = rangeB;
        _length = rangeA.DistanceTo(rangeB);
        _along = (rangeB - rangeA) / _length;
        _across = _along.Orthogonal();
    }

    public PlatformEntity Entity => _entity;

    public void Apply(float gameDt, float realDt, IDefer defer)
    {
        Features features = Features.Current;
        var position = new Vector2(_entity.Position.X, _entity.Position.Y);
        var velocity = new Vector2(_entity.LinearVelocity.X, _entity.LinearVelocity.Y);

        // Where the platform is along the line, as a fraction: 0 at A, 1 at B. It turns around a little before each
        // end, so that it doesn't touch a wall there.
        float fraction = (position - _rangeA).Dot(_along) / _length;
        float turnFraction = features.PlatformTurnDistance / _length;
        bool atEnd = _towardB ? fraction >= 1 - turnFraction : fraction <= turnFraction;

        // It also turns around when something stops it on the way, such as the player. Only between the places where
        // it turns, though: beyond them, the end it is heading for is the way back to its line.
        bool between = fraction > turnFraction && fraction < 1 - turnFraction;
        bool stalled = _stall.Update(velocity.Dot(_towardB ? _along : -_along), gameDt);

        if (atEnd || (stalled && between))
        {
            _towardB = !_towardB;
            _stall.Reset();
        }

        // The caps don't depend on the gravity scale, so a platform pushes as hard while a grab holds it.
        float unscaledWeight = _entity.Mass * Constants.Gravity;

        // On-axis or off-axis maximum thrust, as a fraction of the platform's weight.
        float maxThrustFrac = features.PlatformCapacityPerTile / features.PlatformMassPerTile;

        // Drive toward the end it is heading for at a target speed: top speed, or less near the end, so that braking
        // at a constant rate would stop it there. The braking curve aims past the end by the turn distance, so the
        // target speed is still above zero where the platform turns, and a load can't keep it from getting there.
        Vector2 heading = _towardB ? _along : -_along;
        float distanceLeft = ((_towardB ? _rangeB : _rangeA) - position).Dot(heading);
        float braking = features.PlatformBrakeFrac * maxThrustFrac * Constants.Gravity;
        float targetSpeed = MathF.Min(
            features.PlatformTopSpeed,
            MathF.Sqrt(2 * braking * MathF.Max(0, distanceLeft + features.PlatformTurnDistance)));
        float maxThrust = maxThrustFrac * unscaledWeight;
        float drive = Math.Clamp(
            _entity.Mass * features.PlatformDriveGain * (targetSpeed - velocity.Dot(heading)), -maxThrust, maxThrust);

        float hold = Math.Clamp(
            Spring.CriticallyDampedForce(
                position.Dot(_across),
                velocity.Dot(_across),
                goal: _rangeA.Dot(_across),
                features.PlatformSmoothTime,
                _entity.Mass),
            -maxThrust,
            maxThrust);

        Vector2 thrust = drive * heading + hold * _across;
        _entity.ApplyCentralForce(new Vector3(thrust.X, thrust.Y, 0) - _entity.Mass * _entity.GetGravity());

        float strain = MathF.Min(1, MathF.Max(MathF.Abs(hold), DriveFought(velocity)) / maxThrust);
        _entity.Strain = Smoothing.Exponential(_entity.Strain, strain, features.PlatformStrainTime, gameDt);
        _lastDrive = drive;
        _lastHeading = heading;
        _lastVelocity = velocity;
        _lastDt = gameDt;

        if (_entity.Above.Any(e => e is PlayerEntity))
        {
            Telemetry.Report(Metric.PlatformStrain, _entity.Strain);
        }
    }

    /// <summary>
    /// How much of the last tick's drive went to fighting something: what it pushed with, less what accelerating the
    /// platform took. Zero if something helped, such as a load on a platform going down.
    /// </summary>
    private float DriveFought(Vector2 velocity)
    {
        if (_lastDt == 0)
        {
            return 0;
        }
        float acceleration = (velocity - _lastVelocity).Dot(_lastHeading) / _lastDt;
        return MathF.Max(0, _lastDrive - _entity.Mass * acceleration);
    }
}
