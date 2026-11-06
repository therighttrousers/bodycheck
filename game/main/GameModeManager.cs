using System;

using BodyCheck.Core;
using BodyCheck.Core.Scripts;
using BodyCheck.Core.Simulation;

using Godot;

namespace BodyCheck;

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
/// to <see cref="GameModeState.SlowestSlowdownFactor"/> over half a real second, while the world runs and the player
/// aims. Then the mode is <see cref="GameMode.Phasing"/>, and this steps the world's controllers each physics tick in the
/// world's place. The factor stays at the slowest, so that what runs on game time while the tree is paused, such as the
/// camera's damping, goes on at the same pace. When the world stops asking, play resumes at full speed at once, even
/// partway through the slowdown.
/// </para>
/// <para>
/// When the <see cref="World"/> asks for a cutscene, such as for a script or a dialogue, from playing or slowing for a
/// phase, the mode is <see cref="GameMode.Cutscene"/>, at once and at full speed, in a
/// <see cref="GameModeState.Cutscene"/> that carries what the world's holds ask of it, made again whenever that changes,
/// so that what presents it follows. The world runs, and the player's controller acts on no input but a script's, so the
/// player comes to rest unless a script drives it. When the world no longer asks, play resumes.
/// </para>
/// <para>
/// The engine's time scale is the factor times <see cref="Features.GameplaySpeed"/>, set each physics tick, so that a
/// slower game slows down to the pause as the game at speed 1 does, only slower.
/// </para>
/// </summary>
public partial class GameModeManager : Node
{
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

    /// <summary>Raised after the mode changes and the tree is paused or unpaused to match.</summary>
    public event Action<GameModeState>? GameModeChanged;

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
        // Before the phase, so that a cutscene asked for while slowing for one stops the slowdown.
        CutsceneRequest? cutsceneRequest = World?.CutsceneRequest;
        if (GameMode is GameMode.Playing or GameMode.SlowingForPhase && cutsceneRequest is not null)
        {
            SetGameModeState(new GameModeState.Cutscene(cutsceneRequest));
        }
        else if (_modeState is GameModeState.Cutscene cutscene)
        {
            if (cutsceneRequest is null)
            {
                SetGameModeState(GameModeState.Playing);
            }
            // Holds come and go during a cutscene, such as a dialogue's within a script's.
            else if (cutsceneRequest != cutscene.Request)
            {
                SetGameModeState(new GameModeState.Cutscene(cutsceneRequest));
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
        GameModeChanged?.Invoke(_modeState);
    }
}
