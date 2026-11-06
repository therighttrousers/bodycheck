using System;

using BodyCheck.Core;
using BodyCheck.Core.Simulation;

using Godot;

namespace BodyCheck;

/// <summary>
/// What the game is doing: playing; paused for the menu, the map or the player aiming a phase; or playing a cutscene,
/// such as a dialogue, with the world running and the player's controller given no input. Before the pause for a
/// phase, it slows down to it.
/// </summary>
public enum GameMode { Playing, Menu, Map, EndingMap, SlowingForPhase, Phasing, Cutscene }

/// <summary>
/// Holds the <see cref="GameMode"/>, and switches it on the "menu" and "map" actions. The tree is paused in every mode
/// but <see cref="GameMode.Playing"/>, <see cref="GameMode.SlowingForPhase"/> and <see cref="GameMode.Cutscene"/>, so
/// this node processes while it's paused. "menu" opens or closes the menu, and "map" the map; either opens its own from the other. Closing the map
/// goes through <see cref="GameMode.EndingMap"/>, still paused, until the camera reports with
/// <see cref="NotLookingAtMap"/> that it has moved back.
/// <para>
/// The menu and the map remember the mode they were opened from, and closing them goes back to it: a slowdown goes on
/// where it stopped. While either is open, the <see cref="SlowdownFactor"/> is 1.
/// </para>
/// <para>
/// When the <see cref="World"/> asks for a pause, for the player aiming a phase, the game first slows down to it: the
/// mode is <see cref="GameMode.SlowingForPhase"/>, and the <see cref="SlowdownFactor"/> falls in a straight line from 1
/// to <see cref="SlowestSlowdownFactor"/> over <see cref="SlowdownTime"/> real seconds, while the world runs and the player
/// aims. Then the mode is <see cref="GameMode.Phasing"/>, and this steps the world's controllers each physics tick in the
/// world's place. The factor stays at the slowest, so that what runs on game time while the tree is paused, such as the
/// camera's damping, goes on at the same pace. When the world stops asking, play resumes at full speed at once, even
/// partway through the slowdown.
/// </para>
/// <para>
/// When a dialogue waits in the <see cref="Dialogue"/> box, from playing or slowing for a phase, the mode is
/// <see cref="GameMode.Cutscene"/>, at once and at full speed, and this starts each waiting dialogue in turn. The world
/// runs, and the game gives the player's controller no input, so the player comes to rest. When no dialogue is left,
/// play resumes.
/// </para>
/// <para>
/// The engine's time scale is the factor times <see cref="Features.GameplaySpeed"/>, set each physics tick, so that a
/// slower game slows down to the pause as the game at speed 1 does, only slower.
/// </para>
/// </summary>
public partial class GameModeManager : Node
{
    /// <summary>How long, in real seconds, the game takes to slow down to the pause for a phase.</summary>
    private const float SlowdownTime = 0.5f;

    /// <summary>
    /// The <see cref="SlowdownFactor"/> the slowdown for a phase falls to, and stays at while phasing, short of 0 to avoid
    /// very small steps.
    /// </summary>
    public const double SlowestSlowdownFactor = 0.05;

    private GameModeState? _savedModeState;
    private GameModeState _modeState = GameModeState.Playing;

    public GameMode GameMode => _modeState.Mode;

    /// <summary>
    /// How far the slowdown for a phase has slowed the game: from 1, not at all, to <see cref="SlowestSlowdownFactor"/>.
    /// It falls in <see cref="GameMode.SlowingForPhase"/>, stays at the slowest in <see cref="GameMode.Phasing"/>, and is 1
    /// in every other mode.
    /// </summary>
    public double SlowdownFactor => _modeState.SlowdownFactor;

    /// <summary>The world being played, whose controllers may ask for a pause.</summary>
    public WorldNode? World { get; set; }

    /// <summary>What presents dialogue, which the game plays a cutscene for.</summary>
    public DialogueBox? Dialogue { get; set; }

    /// <summary>Raised after the mode changes and the tree is paused or unpaused to match.</summary>
    public event Action<GameMode>? GameModeChanged;

    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;
        // After the world, so that a tick is either played or paused, never both: on the tick this pauses or unpauses
        // the world, the world's turn has already come and gone.
        ProcessPhysicsPriority = WorldNode.PhysicsPriority + 1;
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event.IsActionPressed("menu"))
        {
            GetViewport().SetInputAsHandled();
            SetGameModeState(GameMode switch
            {
                GameMode.Playing => GameModeState.Menu,
                GameMode.Menu => null,
                GameMode.Map => GameModeState.Menu,
                GameMode.EndingMap => GameModeState.Menu,
                GameMode.SlowingForPhase => GameModeState.Menu,
                GameMode.Phasing => GameModeState.Menu,
                GameMode.Cutscene => GameModeState.Menu,
            });
        }
        else if (@event.IsActionPressed("map"))
        {
            GetViewport().SetInputAsHandled();
            SetGameModeState(GameMode switch
            {
                GameMode.Playing => GameModeState.Map,
                GameMode.Menu => GameModeState.Map,
                GameMode.Map => GameModeState.EndingMap,
                GameMode.EndingMap => GameModeState.Map,
                GameMode.SlowingForPhase => GameModeState.Map,
                GameMode.Phasing => GameModeState.Map,
                GameMode.Cutscene => GameModeState.Map,
            });
        }
    }

    public void NotLookingAtMap()
    {
        if (GameMode == GameMode.EndingMap)
        {
            SetGameModeState(null);
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        // Before the phase, so that a dialogue asked for while slowing for one stops the slowdown.
        if (Dialogue is not null)
        {
            if (GameMode is GameMode.Playing or GameMode.SlowingForPhase && Dialogue.HasWaiting)
            {
                SetGameModeState(GameModeState.Cutscene);
            }
            if (GameMode == GameMode.Cutscene && !Dialogue.IsPresenting)
            {
                if (Dialogue.HasWaiting)
                {
                    Dialogue.StartNext();
                }
                else
                {
                    SetGameModeState(GameModeState.Playing);
                }
            }
        }

        if (World is not null)
        {
            if (GameMode == GameMode.Phasing)
            {
                World.StepPaused();
            }
            else if (_modeState is GameModeState.SlowingForPhase slowing &&
                slowing.Step(1f / Engine.GetPhysicsTicksPerSecond()))
            {
                SetGameModeState(GameModeState.Phasing);
            }

            if (GameMode is GameMode.SlowingForPhase or GameMode.Phasing && !World.PauseRequested)
            {
                SetGameModeState(GameModeState.Playing);
            }
            else if (GameMode == GameMode.Playing && World.PauseRequested)
            {
                SetGameModeState(new GameModeState.SlowingForPhase());
            }
        }

        Engine.TimeScale = SlowdownFactor * Features.Current.GameplaySpeed;
    }

    private void SetGameModeState(GameModeState? newModeState)
    {
        if (newModeState is null)
        {
            _modeState = _savedModeState ?? GameModeState.Playing;
            _savedModeState = null;
        }
        else if (!_modeState.UiPause && newModeState.UiPause)
        {
            _savedModeState = _modeState;
            _modeState = newModeState;
        }
        else
        {
            _modeState = newModeState;
        }

        GetTree().Paused = _modeState.Pause;
        GameModeChanged?.Invoke(GameMode);
    }

    private abstract class GameModeState
    {
        public static readonly GameModeState Playing = new Simple(mode: GameMode.Playing, uiPause: false, pause: false);
        public static readonly GameModeState Menu = new Simple(mode: GameMode.Menu, uiPause: true, pause: true);
        public static readonly GameModeState Map = new Simple(mode: GameMode.Map, uiPause: true, pause: true);
        public static readonly GameModeState EndingMap = new Simple(mode: GameMode.EndingMap, uiPause: true, pause: true);
        public static readonly GameModeState Phasing = new Simple(
            mode: GameMode.Phasing, uiPause: false, pause: true, slowdownFactor: SlowestSlowdownFactor);
        public static readonly GameModeState Cutscene = new Simple(mode: GameMode.Cutscene, uiPause: false, pause: false);

        private GameModeState() { }

        public abstract GameMode Mode { get; }
        public abstract bool UiPause { get; }
        public abstract bool Pause { get; }
        public abstract double SlowdownFactor { get; }

        private sealed class Simple(GameMode mode, bool uiPause, bool pause, double slowdownFactor = 1) : GameModeState
        {
            public override GameMode Mode => mode;
            public override bool UiPause => uiPause;
            public override bool Pause => pause;
            public override double SlowdownFactor => slowdownFactor;
        }

        public sealed class SlowingForPhase : GameModeState
        {
            private double _slowdown;

            public override GameMode Mode => GameMode.SlowingForPhase;
            public override bool UiPause => false;
            public override bool Pause => false;
            public override double SlowdownFactor => (1 - _slowdown) * (1 - SlowestSlowdownFactor) + SlowestSlowdownFactor;

            public bool Step(double realDt)
            {
                _slowdown = Math.Min(1, _slowdown + realDt / SlowdownTime);
                return _slowdown >= 1;
            }
        }
    }
}
