# `game/bodycheck/overlays/`

Relevant decision records:
- [015: The phase ability](../../../docs/decision-records/015-phase.md)
- [039: Pausing for the menu and the map](../../../docs/decision-records/039-modes-kept-across-pauses.md)
- [052: The talking door](../../../docs/decision-records/052-talking-door.md)

## What it is

What is drawn over the world, in the game's viewport, and shown or hidden by the game's mode: the letterbox, the pause menu, the interact prompt and the marker for a phase being aimed. See [rendering.md](../../../docs/designs/rendering.md) for how they are drawn.

## What it is not for

The dialogue's text box, which is the dialogue's, or what is drawn at the window's resolution for debugging. Nor deciding when each is shown, which the top-level scene does from the mode.

## The overlays

- `Letterbox` draws black bars across the top and bottom of the game's viewport, sliding in while its `Shown` is set and out when not, which is set while a cutscene's request asks for black bars. They stay as they are under the menu, since the tree is paused, and are hidden in the map.
- `PauseMenuOverlay`, the menu, is a placeholder: a panel that says the game is paused, shown in the menu mode only.
- `InteractPrompt` shows that the player can interact: while the world's `InteractTrigger` isn't null, it draws `assets/prompts/interact.png`, a placeholder speech bubble, centred over the top of that trigger's area, where it is now, so that it follows a trigger that moves, at whole pixels, under the colour pass and the menu. It is shown while playing only (`052 §3`).
- `PhaseAimMarker` is a placeholder, under the colour pass and the menu, for showing a phase being aimed: a box around the target, and arrows from the center of mass of the player and the target for the velocity each would leave with (`015 §5.7`, `039 §2.3`).
