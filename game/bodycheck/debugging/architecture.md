# `game/bodycheck/debugging/`

Relevant decision records:
- [008: Telemetry, and a debug overlay drawn at the window's resolution](../../../docs/decision-records/008-telemetry-and-overlay.md)
- [058: A map of where the camera may be](../../../docs/decision-records/058-camera-map.md)

## What it is

The tools for debugging, tuning and playtesting that draw over the game: `DebugOverlay`, `CollisionDebug`, `CameraDebug` and `ErrorOverlay`.

## What it is not for

The tools for judging the rendering, which are the look's, or anything shown to a player in a release build.

## The tools

Paused, the debug tools still work: they process while the tree is paused, as do the rendering switches. The top-level scene has the keys that show them, and keeps whether each shows as it loads worlds.

- `CameraDebug`, on F1, shows how the player's camera is framed, for debugging and tuning the framing and the painting: the world's `CameraMap`, on a quad behind the tiles' front layer, and the wanted framing and the view's centre as framed, which the camera rig reports to telemetry each frame, drawn through the tiles. One is added to each world, with the map of the rig's framing. It makes the map's texture the first time it shows (`058 §2.3`).
- F2 shows the world's map markers.
- 2 presents the debug dialogue, `assets/dialogue/debug.dialogue`, from its title `start`, through Core's `DialogueRunner`, as a level's `Dialogue` entity does, so that it plays in a cutscene and queues after any dialogue already presented, and prints when what waits for it has resumed. Its lines have a wait, a speed change, two garble tags, and responses whose branches meet again.
- `DebugOverlay`, on F3, shows the frame rate and times and every telemetry value, at the window's resolution (`008 §2.3`).
- `CollisionDebug`, on F4, shows the collisions in the game as Godot does with visible collision shapes on: every enabled collision shape as Godot's own debug wireframe, a mesh added to the shape, for comparing bodies with their models, and every contact point as a small diamond, in Godot's colour for contacts, drawn through the bodies. The contacts are those of every body that reports them, which every entity does, so a contact between two entities shows as a point on each. Godot's own contact points can't be used, since the engine's API for them isn't exposed to scripts. It reads the shapes and contacts from the engine each frame while showing, and doesn't process while not.
- `ErrorOverlay` shows an error in a file the playtester edits, such as a map or a configuration, at the bottom of the window, at its resolution.
