using System;

using Godot;

namespace BodyCheck;

/// <summary>What the game is doing: playing, or paused for the menu or the map.</summary>
public enum GameMode { Playing, Menu, Map }

/// <summary>
/// Holds the <see cref="GameMode"/>, and switches it on the "menu" and "map" actions. The tree is paused in every mode
/// but <see cref="GameMode.Playing"/>, so this node processes while it's paused. "menu" opens or closes the menu, and
/// "map" the map; either opens its own from the other. There is no menu to show yet, so the menu is only a pause.
/// </summary>
public partial class GameModeManager : Node
{
    public GameMode GameMode { get; private set; } = GameMode.Playing;

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
            };
        }

        if (newGameMode is not null)
        {
            GameMode = newGameMode.Value;
            GetTree().Paused = GameMode != GameMode.Playing;
            GameModeChanged?.Invoke(GameMode);
        }
    }
}
