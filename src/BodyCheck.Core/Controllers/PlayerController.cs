using BodyCheck.Core.Entities;

using Godot;

namespace BodyCheck.Core.Controllers;

/// <summary>
/// Turns player input into forces, impulses and traction on the entity it controls.
/// </summary>
public sealed class PlayerController : IController<PlayerEntity>
{
    // How far, from 0 to 1, the stick must be pushed for one of the eight directions to count as held.
    private const float DirectionDeadZone = 0.5f;

    private readonly PlayerEntity _entity;
    private IWorldView _world = null!;
    private readonly PhaseAction _phase;
    private readonly RunAction _run;
    private readonly JumpAction _jump;
    private readonly SpawnAction _spawn;
    private readonly CornerCorrectionAction _cornerCorrection;

    // Timers count down in game time, except where they say otherwise.
    private bool _jumpWasHeld;
    // In real time: how much longer the player counts as on the ground, so that jumping just after running off an edge
    // jumps.
    private float _groundTimeLeft;
    // In real time: how much longer the last press of jump counts, so that pressing it just before landing jumps.
    private float _jumpBufferLeft;
    // The direction for phasing: the one held, or the last one held while _directionTimeLeft > 0.
    private Vector3 _direction;
    // In real time: how much longer the last direction held still counts for phasing, once let go of, so that letting go
    // of it just before releasing phase, or with it, still phases that way.
    private float _directionTimeLeft;
    private bool _spawnWasHeld;

    // The current time in the air, for telemetry: where the feet were on the last tick on the ground.
    private bool _inAir;
    private Vector2 _takeOffFeet;
    private float _highestFeet;
    private float _airTime;

    /// <param name="entity">The player, which the controller controls.</param>
    public PlayerController(PlayerEntity entity)
    {
        _entity = entity;
        _phase = new PhaseAction(entity, PhaseTuning.Player);
        _run = new RunAction(entity, RunTuning.Player);
        _jump = new JumpAction(entity, JumpTuning.Player);
        _spawn = new SpawnAction(entity, SpawnTuning.Player);
        _cornerCorrection = new CornerCorrectionAction(entity, CornerCorrectionTuning.Player);
    }

    public PlayerEntity Entity => _entity;

    /// <summary>Keeps the world, which gives the player's input, and the block the player spawns.</summary>
    public void Ready(IWorldView world) => _world = world;

    /// <summary>
    /// What a script presses for the player, which the controller acts on in a cutscene in place of the player's input.
    /// It goes back to none when a cutscene ends, so that a script can't leave the player running.
    /// </summary>
    public PlayerInput ScriptedInput { get; set; }

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
        PlayerInput input = GetInput();
        ApplyPhase(input, 0, realDt, defer);
        _jumpWasHeld = input.JumpHeld;
        _spawnWasHeld = input.SpawnHeld;
    }

    public void Apply(float gameDt, float realDt, IDefer defer)
    {
        PlayerInput input = GetInput();
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
            _spawn.Apply(_world.PlayerBlock, input.SpawnHeld && !_spawnWasHeld, gameDt, defer);

            // A press buffered in the air jumps on landing even if jump has been let go of since, as a tap.
            bool jumps = _jumpBufferLeft > 0 && _groundTimeLeft > 0 && !_jump.Thrusting;
            Vector3 runDeltaV = ApplyRun(move, gameDt, jumps);
            Vector3 jumpDeltaV = ApplyJump(input.JumpHeld, gameDt, jumps);

            // Predicted player velocity after the engine integrates forces this tick. Used for corner correction.
            Vector3 gravDeltaV = _entity.GetGravity() * gameDt;
            Vector3 dragDeltaV = -_entity.Tuning.Drag * _entity.LinearVelocity * gameDt / _entity.Mass;
            Vector3 predictedVelocity = _entity.LinearVelocity + runDeltaV + jumpDeltaV + gravDeltaV + dragDeltaV;
            Telemetry.Report(Metric.PredictedVelocity, predictedVelocity.ToVector2());

            _cornerCorrection.Apply(predictedVelocity, gameDt, defer);
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

    /// <summary>
    /// The player's input this tick: the world's <see cref="IWorldView.PlayerInput"/>, or in a cutscene,
    /// <see cref="ScriptedInput"/>, which is none unless a script sets it, so that the player comes to rest while the
    /// world runs.
    /// </summary>
    private PlayerInput GetInput() =>
        _world.GameMode is GameMode.Cutscene ? ScriptedInput : _world.PlayerInput;

    /// <summary>
    /// At the edges of a cutscene, makes what the controller remembers of the player's input fit the new mode. Entering
    /// one, it forgets every leniency timer and buffered press, so that nothing pressed before it, such as a direction
    /// held while aiming a phase, acts during it, and counts no button as held, so that a script's first press counts.
    /// Leaving one, a button already held is not a new press, so that the jump that ends a dialogue doesn't also jump:
    /// only presses made after the cutscene count; and the scripted input goes back to none. Only at the edges, so that
    /// a script that runs and jumps the player during a cutscene keeps its own.
    /// </summary>
    public void GameModeChanged(GameMode from, GameMode to)
    {
        if (to is GameMode.Cutscene && from is not GameMode.Cutscene)
        {
            _direction = Vector3.Zero;
            _directionTimeLeft = 0;
            _groundTimeLeft = 0;
            _jumpBufferLeft = 0;
            _jumpWasHeld = false;
            _spawnWasHeld = false;
        }
        else if (from is GameMode.Cutscene && to is not GameMode.Cutscene)
        {
            PlayerInput input = _world.PlayerInput;
            _jumpWasHeld = input.JumpHeld;
            _spawnWasHeld = input.SpawnHeld;
            ScriptedInput = default;
        }
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
        PhaseOutcome outcome = _phase.Apply(input.PhaseHeld, DirectionHeld(input, realDt), gameDt, defer);
        if (outcome is PhaseOutcome.Phased or PhaseOutcome.Cancelled)
        {
            // A phase ends a jump's thrust, coyote time and buffered press, and so does cancelling one: the aim took them.
            _groundTimeLeft = 0;
            _jump.Interrupt();
            _jumpBufferLeft = 0;
        }
        return outcome;
    }

    /// <summary>
    /// The direction for phasing: one of the eight directions, from the input, as a unit vector in the plane of play,
    /// or zero if none. Once a direction is let go of, it still counts for
    /// <see cref="Features.PlayerPhaseDirectionBufferTicks"/>, for choosing a phase's target as well as for its
    /// release.
    /// </summary>
    private Vector3 DirectionHeld(PlayerInput input, float realDt)
    {
        var held = new Vector2(input.XAxis, input.YAxis);
        var direction = held.Length() >= DirectionDeadZone ? Vector2.EightWay(held).ToVector3() : Vector3.Zero;
        if (direction != Vector3.Zero)
        {
            _direction = direction;
            _directionTimeLeft = Features.TicksToTime(Features.Current.PlayerPhaseDirectionBufferTicks + 1); // +1 to include this tick
        }
        else
        {
            _directionTimeLeft = Math.Max(0, _directionTimeLeft - realDt);
            if (_directionTimeLeft == 0)
            {
                _direction = Vector3.Zero;
            }
        }
        return _direction;
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
