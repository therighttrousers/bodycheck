using BodyCheck.Core.Entities;

using Godot;

namespace BodyCheck.Core.Controllers;

/// <param name="XAxis">Horizontal input, from -1 (left) to 1 (right).</param>
/// <param name="YAxis">Vertical input, from -1 (down) to 1 (up).</param>
public readonly record struct PlayerInput(
    float XAxis,
    float YAxis,
    bool JumpHeld,
    bool PhaseHeld = false,
    bool SpawnHeld = false)
{
    public bool CanLook => YAxis != 0 && XAxis == 0 && !JumpHeld && !PhaseHeld && !SpawnHeld;
}

/// <summary>
/// Turns player input into forces, impulses and traction on the entity it controls.
/// </summary>
public sealed class PlayerController : IController<PlayerEntity>
{
    // How far, from 0 to 1, the stick must be pushed for one of the eight directions to count as held.
    private const float DirectionDeadZone = 0.5f;

    private readonly PlayerEntity _entity;
    private readonly Func<PlayerInput> _input;
    private readonly PhaseAction _phase;
    private readonly RunAction _run;
    private readonly JumpAction _jump;
    private readonly SpawnAction _spawn;

    // Timers count down in game time, except where they say otherwise.
    private bool _jumpWasHeld;
    // In real time: how much longer the player counts as on the ground, so that jumping just after running off an edge
    // jumps.
    private float _groundTimeLeft;
    // In real time: how much longer the last press of jump counts, so that pressing it just before landing jumps.
    private float _jumpBufferLeft;
    private bool _spawnWasHeld;

    // The current time in the air, for telemetry: where the feet were on the last tick on the ground.
    private bool _inAir;
    private Vector2 _takeOffFeet;
    private float _highestFeet;
    private float _airTime;

    /// <param name="entity">The player, which the controller controls.</param>
    /// <param name="input">Supplies the player's input, once per tick.</param>
    /// <param name="block">
    /// The player's block, which it spawns. Until then it is disabled and hidden, so that it is out of the world.
    /// </param>
    public PlayerController(PlayerEntity entity, Func<PlayerInput> input, Entity block)
    {
        _entity = entity;
        _input = input;
        _phase = new PhaseAction(entity, PhaseTuning.Player);
        _run = new RunAction(entity, RunTuning.Player);
        _jump = new JumpAction(entity, JumpTuning.Player);
        _spawn = new SpawnAction(entity, block, SpawnTuning.Player);
    }

    public PlayerEntity Entity => _entity;

    /// <summary>The phase being aimed, if any: see <see cref="PhaseAction.Aim"/>.</summary>
    public PhaseAction.PhaseAim? Phase => _phase.Aim;

    /// <summary>The game is paused while a phase is aimed, so that the player can take its time over the direction.</summary>
    public bool RequestsPause => Phase is not null;

    /// <summary>
    /// Aims the phase while the game is paused for it. Releasing phase ends the aim, and the bodies move on the next
    /// tick of play.
    /// </summary>
    public void ApplyPaused(float realDt, IDefer defer)
    {
        PlayerInput input = _input();
        ApplyPhase(input, 0, realDt, defer);
        _jumpWasHeld = input.JumpHeld;
        _spawnWasHeld = input.SpawnHeld;
    }

    public void Apply(float gameDt, float realDt, IDefer defer)
    {
        PlayerInput input = _input();
        float move = Math.Clamp(input.XAxis, -1, 1);

        if (_entity.OnGround)
        {
            _groundTimeLeft = Features.TicksToTime(Features.Current.PlayerCoyoteTicks + 1); // +1 to include this tick
        }

        if (input.JumpHeld && !_jumpWasHeld)
        {
            _jumpBufferLeft = Features.TicksToTime(Features.Current.PlayerJumpBufferTicks);
        }

        if (ApplyPhase(input, gameDt, realDt, defer) is PhaseOutcome.Idle or PhaseOutcome.Cancelled)
        {
            _spawn.Apply(input.SpawnHeld && !_spawnWasHeld, gameDt, defer);

            // A press buffered in the air jumps on landing even if jump has been let go of since, as a tap.
            bool jumps = _jumpBufferLeft > 0 && _groundTimeLeft > 0 && !_jump.Thrusting;
            Vector3 runDeltaV = ApplyRun(move, gameDt, jumps);
            Vector3 jumpDeltaV = ApplyJump(input.JumpHeld, gameDt, jumps);

            // Predicted player velocity after the engine integrates forces this tick. Used for corner correction.
            Vector3 gravDeltaV = _entity.GetGravity() * gameDt;
            Vector3 dragDeltaV = -_entity.Tuning.Drag * _entity.LinearVelocity * gameDt / _entity.Mass;
            Vector3 predictedVelocity = _entity.LinearVelocity + runDeltaV + jumpDeltaV + gravDeltaV + dragDeltaV;
            Telemetry.Report(Metric.PredictedVelocity, predictedVelocity.ToVector2());

            // If rising into the bottom of a tile, possibly nudge the player left or right to clear a corner.
            bool nudged = CornerCorrection.UpwardCorrect(
                entity: _entity,
                velocity: predictedVelocity,
                gameDt: gameDt,
                maxNudge: Features.Current.PlayerUpwardCornerCorrection,
                maxAngle: Features.Current.PlayerUpwardCornerCorrectionAngle,
                defer: defer);

            // When moving horizontally and hasn't been nudged yet, possibly nudge the player up or down to clear
            // ledges, run over gaps in the floor, or climb steps.
            if (!nudged)
            {
                CornerCorrection.ForwardCorrect(
                    entity: _entity,
                    velocity: predictedVelocity,
                    gameDt: gameDt,
                    maxNudge: Features.Current.PlayerForwardCornerCorrection,
                    maxAngle: Features.Current.PlayerForwardCornerCorrectionAngle,
                    defer: defer);
            }
        }

        // Set animation variables
        _entity.IsGrounded = _groundTimeLeft > 0;
        _entity.IsMoving = move != 0;
        _entity.IsJumping = _jump.Thrusting;
        // Face forward when in the air; otherwise use movement direction.
        _entity.Facing = _entity.IsGrounded ? Math.Sign(move) : 0;

        Report(gameDt);

        _groundTimeLeft = Math.Max(0, _groundTimeLeft - realDt);
        _jumpBufferLeft = Math.Max(0, _jumpBufferLeft - realDt);
        _jumpWasHeld = input.JumpHeld;
        _spawnWasHeld = input.SpawnHeld;
    }

    /// <summary>Reports the player's measurements to <see cref="Telemetry"/>, as of the engine's last step.</summary>
    private void Report(float gameDt)
    {
        var feet = new Vector2(_entity.Position.X, _entity.Position.Y - _entity.HalfSize.Y);
        var velocity = new Vector2(_entity.LinearVelocity.X, _entity.LinearVelocity.Y);
        Telemetry.Report(Metric.Feet, feet);
        Telemetry.Report(Metric.Velocity, velocity);
        Telemetry.Report(Metric.GroundSpeed, _entity.GroundSpeed);
        Telemetry.Report(Metric.OnGround, _entity.OnGround);

        if (!_entity.OnGround)
        {
            if (!_inAir)
            {
                _inAir = true;
                _highestFeet = _takeOffFeet.Y;
                _airTime = 0;
            }
            _highestFeet = MathF.Max(_highestFeet, feet.Y);
            _airTime += gameDt;
            return;
        }

        if (_inAir)
        {
            _inAir = false;
            Telemetry.Report(Metric.AirRise, _highestFeet - _takeOffFeet.Y);
            Telemetry.Report(Metric.AirRun, MathF.Abs(feet.X - _takeOffFeet.X));
            Telemetry.Report(Metric.AirLand, feet.Y - _takeOffFeet.Y);
            Telemetry.Report(Metric.AirTime, _airTime);
        }

        // The engine still reports the floor contact for a tick after a jump, when the feet have already
        // risen. Thrust time counts from the jump, so it marks it.
        if (!_jump.Thrusting)
        {
            _takeOffFeet = feet;
        }
    }

    /// <summary>
    /// Begins, holds, aims and releases a phase. It takes the place of running and jumping on this tick while it is aimed,
    /// and on the tick it is made, when the bodies move after the entity steps and the contacts are from before it.
    /// </summary>
    /// <param name="gameDt">The tick's length in game time: 0 while the game is paused.</param>
    /// <param name="realDt">The tick's length in real time.</param>
    /// <param name="defer">An object that allows running actions later in the tick.</param>
    private PhaseOutcome ApplyPhase(PlayerInput input, float gameDt, float realDt, IDefer defer)
    {
        PhaseOutcome outcome = _phase.Apply(input.PhaseHeld, DirectionHeld(input), gameDt, realDt, defer);
        if (outcome is PhaseOutcome.Phased or PhaseOutcome.Cancelled)
        {
            // A phase ends a jump's thrust, coyote time and buffered press, and so does cancelling one: the aim took them.
            _groundTimeLeft = 0;
            _jump.Interrupt();
            _jumpBufferLeft = 0;
        }
        return outcome;
    }

    /// <summary>One of the eight directions, from the input, as a unit vector in the plane of play, or zero if none.</summary>
    private static Vector3 DirectionHeld(PlayerInput input)
    {
        var held = new Vector2(input.XAxis, input.YAxis);
        return held.Length() >= DirectionDeadZone ? Vector2.EightWay(held).ToVector3() : Vector3.Zero;
    }

    /// <param name="jumps">Whether the entity jumps on this tick.</param>
    private Vector3 ApplyRun(float move, float gameDt, bool jumps)
    {
        (Vector3 deltaV, bool slips) = _run.Apply(move, grounded: _groundTimeLeft > 0, jumps, gameDt);
        if (slips)
        {
            _entity.SlipAfterEntitySteps();
        }
        return deltaV;
    }

    /// <summary>Jumps, or keeps up a jump's thrust. A jump uses up coyote time and the buffered press.</summary>
    /// <returns>The change in the player's velocity from the jump, which shows only after the engine's step.</returns>
    /// <param name="jumps">Whether the player jumps on this tick.</param>
    private Vector3 ApplyJump(bool jumpHeld, float gameDt, bool jumps)
    {
        Vector3 deltaV = _jump.Apply(jumps, jumpHeld, gameDt);
        if (jumps)
        {
            _groundTimeLeft = 0;
            _jumpBufferLeft = 0;
        }
        return deltaV;
    }
}
