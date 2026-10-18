using System;

using BodyCheck.Core.Simulation;

using Godot;

namespace BodyCheck;

/// <summary>What the game is doing: playing, or paused for the menu, the map, or the player aiming a phase.</summary>
public enum GameMode { Playing, Menu, Map, Phasing }

/// <summary>
/// Holds the <see cref="GameMode"/>, and switches it on the "menu" and "map" actions. The tree is paused in every mode
/// but <see cref="GameMode.Playing"/>, so this node processes while it's paused. "menu" opens or closes the menu, and
/// "map" the map; either opens its own from the other. There is no menu to show yet, so the menu is only a pause.
/// <para>
/// While the <see cref="World"/> asks for a pause, for the player aiming a phase, the mode is
/// <see cref="GameMode.Phasing"/>, and this steps the world's controllers each physics tick in its place. The menu and
/// the map can still be opened then; closing them goes back to playing, and so to phasing again.
/// </para>
/// </summary>
public partial class GameModeManager : Node
{
    public GameMode GameMode { get; private set; } = GameMode.Playing;

    /// <summary>The world being played, whose controllers may ask for a pause.</summary>
    public WorldNode? World { get; set; }

    /// <summary>Raised after the mode changes and the tree is paused or unpaused to match.</summary>
    public event Action<GameMode>? GameModeChanged;

    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;
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
                GameMode.Phasing => GameMode.Menu,
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
                GameMode.Phasing => GameMode.Map,
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
            World.StepPaused((float)delta);
            if (!World.PauseRequested)
            {
                SetGameMode(GameMode.Playing);
            }
        }
        else if (GameMode == GameMode.Playing && World.PauseRequested)
        {
            SetGameMode(GameMode.Phasing);
        }
    }

    private void SetGameMode(GameMode gameMode)
    {
        GameMode = gameMode;
        GetTree().Paused = GameMode != GameMode.Playing;
        GameModeChanged?.Invoke(GameMode);
    }
}
