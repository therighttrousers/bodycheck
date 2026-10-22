using System;

using BodyCheck.Core.Simulation;

using Godot;

namespace BodyCheck;

/// <summary>
/// What the game is doing: playing, or paused for the menu, the map, or the player aiming a phase. Before the pause
/// for a phase, it slows down to it.
/// </summary>
public enum GameMode { Playing, Menu, Map, SlowingForPhase, Phasing }

/// <summary>
/// Holds the <see cref="GameMode"/>, and switches it on the "menu" and "map" actions. The tree is paused in every mode
/// but <see cref="GameMode.Playing"/> and <see cref="GameMode.SlowingForPhase"/>, so this node processes while it's
/// paused. "menu" opens or closes the menu, and
/// "map" the map; either opens its own from the other. There is no menu to show yet, so the menu is only a pause.
/// <para>
/// When the <see cref="World"/> asks for a pause, for the player aiming a phase, the game first slows down to it: the
/// mode is <see cref="GameMode.SlowingForPhase"/>, and the engine's time scale falls in a straight line from 1 to
/// <see cref="SlowestTimeScale"/> over <see cref="SlowdownTime"/> real seconds, while the world runs and the player aims. Then the mode is
/// <see cref="GameMode.Phasing"/>, the time scale is 1 again, and this steps the world's controllers each physics
/// tick in the world's place. When the world stops asking, play resumes at full speed at once, even partway through the
/// slowdown. The menu and the map can still be opened; closing them goes back to playing, and so to the phase again.
/// </para>
/// </summary>
public partial class GameModeManager : Node
{
    /// <summary>How long, in real seconds, the game takes to slow down to the pause for a phase.</summary>
    private const float SlowdownTime = 0.5f;

    /// <summary>The time scale the slowdown for a phase falls to, short of 0 to avoid very small steps.</summary>
    public const double SlowestTimeScale = 0.05;

    private Tween? _slowdown;

    public GameMode GameMode { get; private set; } = GameMode.Playing;

    /// <summary>The world being played, whose controllers may ask for a pause.</summary>
    public WorldNode? World { get; set; }

    /// <summary>Raised after the mode changes and the tree is paused or unpaused to match.</summary>
    public event Action<GameMode>? GameModeChanged;

    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;
        // Before the world, so that play resumes on the tick a phase is released while paused: the world then steps,
        // and moves the bodies before the engine's step, as the phase expects. After it, the engine would step once
        // with the phase's velocities from where the bodies were, before the move, as into the target.
        ProcessPhysicsPriority = -1;
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        GameMode? newGameMode = null;

        if (@event.IsActionPressed("menu"))
        {
            GetViewport().SetInputAsHandled();
            newGameMode = GameMode switch
            {
                GameMode.Playing => GameMode.Menu,
                GameMode.Menu => GameMode.Playing,
                GameMode.Map => GameMode.Menu,
                GameMode.SlowingForPhase or GameMode.Phasing => GameMode.Menu,
            };
        }
        else if (@event.IsActionPressed("map"))
        {
            GetViewport().SetInputAsHandled();
            newGameMode = GameMode switch
            {
                GameMode.Playing => GameMode.Map,
                GameMode.Menu => GameMode.Map,
                GameMode.Map => GameMode.Playing,
                GameMode.SlowingForPhase or GameMode.Phasing => GameMode.Map,
            };
        }

        if (newGameMode is not null)
        {
            SetGameMode(newGameMode.Value);
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        if (World is null)
        {
            return;
        }

        if (GameMode == GameMode.Phasing)
        {
            World.StepPaused();
        }

        if (GameMode is GameMode.SlowingForPhase or GameMode.Phasing && !World.PauseRequested)
        {
            SetGameMode(GameMode.Playing);
        }
        else if (GameMode == GameMode.Playing && World.PauseRequested)
        {
            SetGameMode(GameMode.SlowingForPhase);
        }
    }

    private void SetGameMode(GameMode gameMode)
    {
        // Leaving the slowdown, by any way, goes back to full speed.
        _slowdown?.Kill();
        _slowdown = null;
        Engine.TimeScale = 1;

        GameMode = gameMode;
        GetTree().Paused = GameMode is not (GameMode.Playing or GameMode.SlowingForPhase);
        if (GameMode == GameMode.SlowingForPhase)
        {
            // In real time, since the tween would otherwise slow itself down.
            _slowdown = CreateTween().SetIgnoreTimeScale();
            _slowdown.TweenMethod(Callable.From<double>(scale => Engine.TimeScale = scale), 1.0, SlowestTimeScale, SlowdownTime);
            _slowdown.TweenCallback(Callable.From(() => SetGameMode(GameMode.Phasing)));
        }
        GameModeChanged?.Invoke(GameMode);
    }
}
