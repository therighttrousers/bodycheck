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

    // Each source of input's recent input, both sampled on every tick the controller applies, whatever the game's mode,
    // so that each one's presses are of its own input: a button the user holds through a cutscene is no new press when
    // it ends, and a script's first press counts.
    private RecentInput _recentUserInput;
    private RecentInput _recentScriptedInput;

    // Forgotten as a cutscene starts, so that nothing pressed before it acts during it.
    private Leniency _leniency;

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

    /// <summary>Keeps the world, which gives the game's mode and the block the player spawns.</summary>
    public void Ready(IWorldView world) => _world = world;

    /// <summary>
    /// Supplies what the user is pressing, whatever the game's mode, read when the controller needs it, so that it keeps
    /// no copy. The player stands idle if this is null, as in tests and probes that give it no input.
    /// </summary>
    public Func<PlayerInput>? UserInputSource { get; set; }

    /// <summary>
    /// What a script presses for the player, which the controller acts on in a cutscene in place of the user's input.
    /// It goes back to none when a cutscene ends, so that a script can't leave the player running.
    /// </summary>
    public PlayerInput ScriptedInput { get; set; }

    /// <summary>
    /// Whether interact was pressed on the controller's last tick: held then, and not on the tick before, in the input
    /// it acts on. As for every button, one the user holds through a cutscene is no new press when it ends, so that a
    /// cutscene the player starts by interacting doesn't start again at once.
    /// </summary>
    public bool InteractJustPressed => ActiveRecentInput.InteractJustPressed;

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
        SampleInput();
        ApplyPhase(ActiveRecentInput.Current, 0, realDt, defer);
    }

    public void Apply(float gameDt, float realDt, IDefer defer)
    {
        SampleInput();
        RecentInput recent = ActiveRecentInput;
        PlayerInput input = recent.Current;
        float move = Math.Clamp(input.XAxis, -1, 1);

        if (_entity.OnGround)
        {
            _leniency.GroundTimeLeft = Features.TicksToTime(Features.Current.PlayerCoyoteTicks + 1); // +1 to include this tick
        }

        if (recent.JumpJustPressed)
        {
            _leniency.JumpBufferLeft = Features.TicksToTime(Features.Current.PlayerJumpBufferTicks);
        }

        if (ApplyPhase(input, gameDt, realDt, defer) is PhaseOutcome.Idle or PhaseOutcome.Cancelled)
        {
            _spawn.Apply(_world.PlayerBlock, recent.SpawnJustPressed, gameDt, defer);

            // A press buffered in the air jumps on landing even if jump has been let go of since, as a tap.
            bool jumps = _leniency.JumpBufferLeft > 0 && _leniency.GroundTimeLeft > 0 && !_jump.Thrusting;
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
        _entity.IsGrounded = _leniency.GroundTimeLeft > 0;
        _entity.IsMoving = move != 0;
        _entity.IsJumping = _jump.Thrusting;
        // Face forward when in the air; otherwise use movement direction.
        _entity.Facing = _entity.IsGrounded ? Math.Sign(move) : 0;

        Report(gameDt);

        _leniency.GroundTimeLeft = Math.Max(0, _leniency.GroundTimeLeft - realDt);
        _leniency.JumpBufferLeft = Math.Max(0, _leniency.JumpBufferLeft - realDt);
    }

    /// <summary>
    /// The input the controller acts on: the user's, from <see cref="UserInputSource"/>, or in a cutscene,
    /// <see cref="ScriptedInput"/>, which is none unless a script sets it, so that the player comes to rest while the
    /// world runs. Reading it has no side effects, so the camera reads it too.
    /// </summary>
    public PlayerInput Input => _world.GameMode is GameMode.Cutscene ? ScriptedInput : UserInput;

    private PlayerInput UserInput => UserInputSource?.Invoke() ?? default;

    // The recent input of the source the controller acts on, as Input chooses it.
    private RecentInput ActiveRecentInput =>
        _world.GameMode is GameMode.Cutscene ? _recentScriptedInput : _recentUserInput;

    private void SampleInput()
    {
        _recentUserInput = _recentUserInput.Next(UserInput);
        _recentScriptedInput = _recentScriptedInput.Next(ScriptedInput);
    }

    /// <summary>
    /// At the edges of a cutscene. Entering one, it forgets every leniency timer and buffered press, so that nothing
    /// pressed before it, such as a direction held while aiming a phase, acts during it. Leaving one, the scripted input
    /// goes back to none, so that a script can't leave the player running. Only at the edges, so that a script that runs
    /// and jumps the player during a cutscene keeps its own. Which presses are new needs nothing here, since each source
    /// of input keeps its own.
    /// </summary>
    public void GameModeChanged(GameMode from, GameMode to)
    {
        if (to is GameMode.Cutscene && from is not GameMode.Cutscene)
        {
            _leniency = default;
        }
        else if (from is GameMode.Cutscene && to is not GameMode.Cutscene)
        {
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
            _leniency.GroundTimeLeft = 0;
            _jump.Interrupt();
            _leniency.JumpBufferLeft = 0;
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
            _leniency.Direction = direction;
            _leniency.DirectionTimeLeft = Features.TicksToTime(Features.Current.PlayerPhaseDirectionBufferTicks + 1); // +1 to include this tick
        }
        else
        {
            _leniency.DirectionTimeLeft = Math.Max(0, _leniency.DirectionTimeLeft - realDt);
            if (_leniency.DirectionTimeLeft == 0)
            {
                _leniency.Direction = Vector3.Zero;
            }
        }
        return _leniency.Direction;
    }

    /// <param name="jumps">Whether the entity jumps on this tick.</param>
    private Vector3 ApplyRun(float move, float gameDt, bool jumps)
    {
        (Vector3 deltaV, bool slips) = _run.Apply(move, grounded: _leniency.GroundTimeLeft > 0, jumps, gameDt);
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
            _leniency.GroundTimeLeft = 0;
            _leniency.JumpBufferLeft = 0;
        }
        return deltaV;
    }

    /// <summary>
    /// One source of input on the tick it was last sampled and the tick before, and so which buttons were just pressed:
    /// held on the one, and not on the other.
    /// </summary>
    private readonly record struct RecentInput(PlayerInput Previous, PlayerInput Current)
    {
        public bool JumpJustPressed => Current.JumpHeld && !Previous.JumpHeld;
        public bool SpawnJustPressed => Current.SpawnHeld && !Previous.SpawnHeld;
        public bool InteractJustPressed => Current.InteractHeld && !Previous.InteractHeld;

        /// <summary>This, a tick later, when the source gives <paramref name="input"/>.</summary>
        public RecentInput Next(PlayerInput input) => new(Current, input);
    }

    /// <summary>
    /// Leniency for the player's fingers, each timer in real time. Coyote time also says when the player runs with its
    /// forces for the ground.
    /// </summary>
    private struct Leniency
    {
        // How much longer the player counts as on the ground, so that jumping just after running off an edge jumps.
        public float GroundTimeLeft;
        // How much longer the last press of jump counts, so that pressing it just before landing jumps.
        public float JumpBufferLeft;
        // The direction for phasing: the one held, or the last one held while DirectionTimeLeft > 0.
        public Vector3 Direction;
        // How much longer the last direction held still counts for phasing, once let go of, so that letting go of it
        // just before releasing phase, or with it, still phases that way.
        public float DirectionTimeLeft;
    }
}
