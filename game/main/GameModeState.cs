using System;

using BodyCheck.Core;
using BodyCheck.Core.Scripts;

namespace BodyCheck;

/// <summary>
/// What the game is doing, as <see cref="GameModeManager"/> holds it: its <see cref="GameMode"/>, whether it pauses the
/// tree, and as one for the menu and the map, which go back to the state they were opened from, and how far it slows the
/// game. Some modes carry more, such as a slowdown's progress or a cutscene's request. It is the game's, for what Core
/// doesn't read, such as how a cutscene is presented. A state with more to it is made anew when that changes, and
/// <see cref="GameModeManager.GameModeChanged"/> says so.
/// </summary>
public abstract class GameModeState
{
    /// <summary>How long, in real seconds, the game takes to slow down to the pause for a phase.</summary>
    private const float SlowdownTime = 0.5f;

    /// <summary>
    /// The <see cref="SlowdownFactor"/> the slowdown for a phase falls to, and stays at while phasing, short of 0 to avoid
    /// very small steps.
    /// </summary>
    public const double SlowestSlowdownFactor = 0.05;

    public static readonly GameModeState Playing = new Simple(mode: GameMode.Playing, uiPause: false, pause: false);
    public static readonly GameModeState Menu = new Simple(mode: GameMode.Menu, uiPause: true, pause: true);
    public static readonly GameModeState Map = new Simple(mode: GameMode.Map, uiPause: true, pause: true);
    public static readonly GameModeState EndingMap = new Simple(mode: GameMode.EndingMap, uiPause: true, pause: true);
    public static readonly GameModeState Phasing = new Simple(
        mode: GameMode.Phasing, uiPause: false, pause: true, slowdownFactor: SlowestSlowdownFactor);

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

    /// <summary>A cutscene, presented as the world's holds on it ask, joined.</summary>
    public sealed class Cutscene(CutsceneRequest request) : GameModeState
    {
        public override GameMode Mode => GameMode.Cutscene;
        public override bool UiPause => false;
        public override bool Pause => false;
        public override double SlowdownFactor => 1;
        public CutsceneRequest Request { get; } = request;
    }
}
