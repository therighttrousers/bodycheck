namespace BodyCheck.Core;

/// <summary>
/// What the game is doing: playing; paused for the menu, the map or the player aiming a phase; or playing a cutscene,
/// such as a dialogue, with the world running and the player's controller given no input. Before the pause for a
/// phase, it slows down to it.
/// <para>
/// The game holds the mode. The world steps its controllers only while playing, slowing for a phase and in a cutscene,
/// and on its paused path while phasing, so those are the modes they can see; test for the modes that matter, rather
/// than switching over all of them.
/// </para>
/// </summary>
public enum GameMode { Playing, Menu, Map, EndingMap, SlowingForPhase, Phasing, Cutscene }
