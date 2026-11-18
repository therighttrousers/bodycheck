# `game/bodycheck/modes/`

Relevant decision records:
- [013: The world as a container of rooms](../../../docs/decision-records/013-world-of-rooms.md)
- [015: The phase ability](../../../docs/decision-records/015-phase.md)
- [021: Sound](../../../docs/decision-records/021-sound.md)
- [022: Actions at the end of a tick](../../../docs/decision-records/022-end-of-tick-actions.md)
- [030: Gameplay speed](../../../docs/decision-records/030-gameplay-speed.md)
- [039: Pausing for the menu and the map](../../../docs/decision-records/039-modes-kept-across-pauses.md)
- [050: The cutscene mode](../../../docs/decision-records/050-cutscene-mode.md)
- [051: The scripted run at the spawn](../../../docs/decision-records/051-scripted-run-at-the-spawn.md)

## What it is

The game's mode, which `GameModeManager` holds: playing, the menu or the map, on Escape and Tab or the gamepad's Start and Back, phasing, while the world asks for a pause, during which it steps the world's controllers on their paused path, and cutscenes. Every mode but playing, slowing for a phase and cutscenes pauses the tree, which stops the entities' controllers too (`013 §1.2`). `GameModeState` is a mode with what goes with it, and `Muffler` makes each mode sound as it should.

## What it is not for

How a mode is presented, such as the pause menu, the letterbox or the dialogue box, or what holds a cutscene, which is Core's: the mode's state is for what Core doesn't read (`051 §3.2`).

## The modes

- **What stops while the game is paused.** The game's viewport pauses with the game, as does all of it that inherits its process mode, the world and the dialogue box included; what must run while paused, such as the camera rig, processes always. A container passes input on to its viewport only while it processes, so nothing in the game's viewport gets input while paused. When dialogue paused the game, the viewport processed always so that the box got input; cutscenes don't pause, so it no longer does (`050 §4.2`).
- **A tick is either played or paused, never both.** `GameModeManager` steps after the world, by its physics priority, so on the tick it pauses or unpauses the world, the world's turn has already passed (`022 §1.1`).
- **Gameplay speed.** Each physics tick, `GameModeManager` sets the engine's time scale to the feature `GameplaySpeed`, times the slowdown's factor (`030 §1.1`).
- **Slowing for a phase.** Before phasing, it slows the game down to the pause: its `SlowdownFactor` falls from 1 to `SlowestSlowdownFactor`, 0.05, over 0.5 real seconds, and the engine's time scale falls with it, which keeps 60 physics ticks a second and shortens each. It stops short of 0 to avoid very small steps (`015 §5.12`, `030 §1.2`). While phasing, the factor stays at the slowest, so that the camera, whose damping runs on game time, creeps on at the same pace rather than speeding up and snapping into place (`039 §1.3`). Probes run with nothing pausing for them, so their player aims while the world runs.
- **The menu and the map** are pauses for the user: opening one remembers the mode it was opened from, and closing it goes back to that mode, so a slowdown goes on where it stopped. While either is open, the factor is 1 (`039 §1.2`). Closing the map keeps the game paused, in `EndingMap`, until the camera's tween back is done: the camera rig tells `GameModeManager` when the player's camera takes over again. Opening the menu during the tween back and closing it skips the rest of the wait, which is accepted (`039 §1.1`, `§1.5`).
- **Cutscenes**, which dialogue plays in, don't pause the game: the world, its controllers, its scripts, animations and the camera run as in play, and the player's controller acts on no input but a script's, so that the player comes to rest by the normal path unless a script drives it. The same rules hold for every cutscene, whether the player talks or is scripted. Sound isn't muffled (`050 §1.2`, `§2.1`, `§3.3`). At its point in the tick, from playing or slowing for a phase, `GameModeManager` switches to the cutscene mode, at once and at full speed, when the world asks for a cutscene with its `CutsceneRequest`, which Core's runners of scripts and dialogues hold one for; when the world no longer asks, it goes back to playing. The mode's state, a `GameModeState.Cutscene`, carries the request, and is made again whenever the request changes, as holds come and go, so that what presents the cutscene follows (`051 §3.6`). It knows nothing of dialogue (`051 §3.1`, `§3.5`). A phase being aimed is cancelled, since the player's controller gets none of the player's input. The menu and the map, opened from it, go back to it.

## Muffling

`Muffler` muffles every sound with the low-pass filter on `Master`, which the bus layout leaves off: each frame, it sets the cutoff from the game's mode and `GameModeManager`'s slowdown factor, open while playing and in a cutscene, falling in octaves with the factor while slowing for a phase, most of the way early on, and closed while paused for any reason, at the cutoff the layout gives the filter (`021 §2.2`, `§2.5`).

## Open questions

- Input across the game's modes (issue #174). The player's input is polled with `Input.IsActionPressed`, which sees a held key whether or not a node handled its press, so a key that closes the menu or the map can reach play on the next played tick. The jump that ends a dialogue doesn't, since the player's controller counts a button held as a cutscene ends as no new press (`050 §4.3`); input that owns its own presses would do that for every mode, and for the camera and scripted input too. Input is read in several places, in two ways.
