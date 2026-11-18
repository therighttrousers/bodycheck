# `game/bodycheck/camera/`

Relevant decision records:
- [010: Camera framing](../../../docs/decision-records/010-camera-framing.md)
- [013: The world as a container of rooms](../../../docs/decision-records/013-world-of-rooms.md)
- [014: Less disruptive looking](../../../docs/decision-records/014-less-disruptive-looking.md)
- [020: Drawing rooms](../../../docs/decision-records/020-drawing-rooms.md)
- [030: Gameplay speed](../../../docs/decision-records/030-gameplay-speed.md)
- [039: Pausing for the menu and the map](../../../docs/decision-records/039-modes-kept-across-pauses.md)
- [042: The camera clamped by tiles](../../../docs/decision-records/042-camera-clamped-by-tiles.md)
- [058: A map of where the camera may be](../../../docs/decision-records/058-camera-map.md)
- [059: The camera's lead](../../../docs/decision-records/059-camera-lead.md)
- [060: The camera's height through a jump](../../../docs/decision-records/060-camera-height-through-a-jump.md)

## What it is

The camera: `CameraRig`, which moves it, `CameraFraming`, which frames the player, and `CameraMap`, where the view's centre may be. See [rendering.md](../../../docs/designs/rendering.md#scale) for how it frames.

## What it is not for

How the image is made from what the camera sees, which is the look's, or drawing the map for debugging.

## The rig

`CameraRig` is the camera: a `Camera3D` with a Phantom Camera host, which puts it where the phantom camera of highest priority is, and two phantom cameras, the player's and the map's. Phantom Camera's nodes are GDScript classes; C# reaches them through the wrappers it ships, which can't create them, so the rig creates them from their scripts.

- The player's phantom camera follows a target, with no damping of its own, which the rig moves each frame: `CameraFraming` moves it toward the point of its `CameraMap` nearest the wanted framing, above where the player last stood, ahead of the way the player last pressed, and up or down by where the player looks, as a critically damped spring (`010 §1.3`, `§2.1`, `042 §5.3`, `058 §7`). `CameraFraming` is built from the world when the rig starts to follow it, and the rig gives it the wanted framing, the view's size and the frame's game time each frame, and reports to telemetry what it wanted, the goal and the view's centre.
- The rig reads the look from the input the player's controller acts on, its `Input`. It looks only while the game is playing, not while paused or in a cutscene. It looks only while that input `CanLook`, after a hold: `StepLook` adds up how long the player has looked, reset when the look stops, and the look amount eases in from that (`014`, `060 §1`). Looking up or down is the player's hands, so its delay and pan run on real time (`030 §3.1`).
- The rig keeps the anchor that holds the view's height through a jump, from the player's height and whether they are on the ground, in every mode, since it follows the world and not input (`060 §2`). Each of the wanted framing's offsets, the look, the lead and the hold, is stepped by a method of its own, and `MoveTarget` only adds them up: the look eases the wanted height from the lifted anchor toward the player's height plus the look's offset (`060 §2.7`).
- The framing's spring runs on game time, so that the camera lags a moving player by the same distance at any speed. Its smoothing times are features, so that a configuration can tune them (`030 §4.4`, `058 §7.2`).
- The rig keeps the lead itself, as a spring of its own, from the horizontal input the player's controller acts on, its `Input`: the user's while playing, and the controller's `ScriptedInput` in a cutscene. It reads it only while playing and in a cutscene; in other modes the lead holds, since the input either does nothing or aims a phase (`059 §1.3`, `§1.6`). Its spring runs on game time, as the framing's does, and its distance, smoothing times and the delay before it goes back are features.
- The rig processes while the tree is paused, so that the camera can move, and the player doesn't look up or down then.

## The world as the map

(Neil.) In the map, the map's phantom camera takes priority: it is over the middle of the world, back to where the whole world fits in the view, and the host tweens the camera there and back, in 0.25 real seconds whatever the gameplay speed: the host's tween runs on game time, so the rig multiplies its duration by the speed (`030 §3.1`). The rig tells `GameModeManager` when the player's camera takes over again, which ends the map's mode (`039 §1.1`). The world itself is the map; nothing else is drawn for it.

## Open questions

- The rest of the world as the map (Neil): the map pauses the tree and zooms out, but doesn't yet hide what moves or is expensive to draw, or show only the rooms the player has explored. Rooms already draw their own tiles (`020 §2.1`), so this would be revealing the explored ones, perhaps with a cheaper look each, and it means caching could unload a room's entities and colliders but not its look (`013`, Context). The zoom's tween is linear in distance, which does most of the visible zooming early; an exponential transition may feel more even at the sizes of a whole game, 16x16 to 32x32 rooms.
- Phantom Camera's lookahead and newer framed-follow features are described for its 2D camera only, and are unverified in 3D. Its limits are 2D only; `CameraRig` moves the target instead (`010 §2.1`).
