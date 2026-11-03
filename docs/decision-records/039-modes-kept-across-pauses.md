# 039: Pausing for the menu and the map

## Context

Inciting event (2026-11-02): issue #125, keeping the game paused until the camera has zoomed back in from the map. While working on it, Neil found that holding phase and pressing Escape over and over made the player and its target float away: closing the menu went back to playing, the world asked for the pause again, and every slowdown started again from full speed, so the slowdown never ended.

Goals:
- Opening and closing the menu or the map doesn't change what the game was doing.
- No visible jump in the camera between the slowdown and the pause for a phase.

## 1. Modes

- [x] 1.1 Closing the map. Decision (Neil): the game stays paused, in a mode of its own, `EndingMap`, until the camera has moved back to the player. The camera rig tells `GameModeManager` when the player's camera takes over again.
- [x] 1.2 The menu and the map are pauses for the user. Decision (Neil): opening one from playing, slowing for a phase or phasing remembers that mode, and closing it goes back to it. A slowdown goes on where it stopped. This replaces part of `015 §5.12`: opening the menu or the map still runs the game at full speed while it is open, but closing it no longer goes back to playing.
- [x] 1.3 The time scale while phasing. At the end of the slowdown, the factor went back to 1 while the tree was paused. The camera's follow damping runs on game time, so the camera, still catching up to the player, sped up 20 times and snapped into place. Options:
  - A factor of 0 while phasing. Tried: it freezes the camera. Claude, from reading the addon's code, not tested: Phantom Camera's damping divides by the frame's delta when an axis is exactly on its target, so a delta of 0 could make the camera's position NaN.
  - The slowest factor while phasing. The camera creeps on at the pace it had at the end of the slowdown.
  - Decision (Neil): the slowest factor. A camera that creeps on is better than one that speeds up and snaps into place. While the menu or the map is open, the factor is 1, so the camera catches up then.
- [x] 1.4 Slowing and phasing as one mode, with the pause starting at the end of the slowdown. Tried and backed out (Neil): with two modes, `GameModeManager` raises its change event at the right times more easily, and code outside it can tell the slowdown from the pause.
- [x] 1.5 Known gap (Claude): from `EndingMap`, opening the menu and closing it goes straight back to the remembered mode, before the camera has moved back. Decision (Neil): not fixed. A player is very unlikely to notice. A fix would need `GameModeManager` to know what the camera is doing, or a stack of modes with rules for when to push or replace, and both are more than the gap is worth.

## 2. The menu's placeholder

Neil added a placeholder menu, a panel that says the game is paused, to tell the menu's pause from the pause for a phase, and to see what drawing UI at 320x240 takes.

- [x] 2.1 Where it is drawn. First built: a second viewport of its own, at 320x240, scaled up like the game's. Claude: the game's viewport already is a 320x240 image scaled up with nearest-neighbour sampling, and a canvas layer above the colour pass isn't dithered (`008 §2.2`). A second viewport with a transparent background also blends a translucent panel twice, so it comes out darker (from memory, not tested). Decision (Neil): a canvas layer in the game's viewport, above the colour pass. Laid out at the viewport's size and centred, it stays centred with low resolution off, only smaller.
- [x] 2.2 Text and edges. Neil tried the default font aliased: it looked terrible, so it stays antialiased. The panel's corners aren't antialiased. Its translucency doesn't matter for a placeholder; the real menu will almost certainly be opaque.
- [x] 2.3 The aim marker. It was drawn at the window's resolution, over the whole game view, so it showed over the menu when the menu was opened during an aim. Decision (Neil): move it into the game's viewport, under the colour pass and the menu, with its sizes in pixels at 320x240. That fixes the order, and it is a step toward the real effect, which is likely to be part of the game's image (Claude), however it is drawn.
