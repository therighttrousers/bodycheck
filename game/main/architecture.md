# `game/main/`

Relevant decision records:
- [008: Telemetry, and a debug overlay drawn at the window's resolution](../../docs/decision-records/008-telemetry-and-overlay.md)
- [009: The first level loader](../../docs/decision-records/009-level-loader.md)
- [010: Camera framing](../../docs/decision-records/010-camera-framing.md)
- [011: Stitching LDtk levels into one world](../../docs/decision-records/011-stitched-levels.md)
- [012: Subsystems in `BodyCheck.Core`](../../docs/decision-records/012-core-subsystems.md)
- [013: The world as a container of rooms](../../docs/decision-records/013-world-of-rooms.md)
- [014: Less disruptive looking](../../docs/decision-records/014-less-disruptive-looking.md)
- [015: The phase ability](../../docs/decision-records/015-phase.md)
- [017: The playtesting and puzzle design workflow](../../docs/decision-records/017-playtesting-workflow.md)
- [020: Drawing rooms](../../docs/decision-records/020-drawing-rooms.md)
- [021: Sound](../../docs/decision-records/021-sound.md)
- [022: Actions at the end of a tick](../../docs/decision-records/022-end-of-tick-actions.md)
- [024: Playtesting feedback](../../docs/decision-records/024-playtesting-feedback.md)
- [030: Gameplay speed](../../docs/decision-records/030-gameplay-speed.md)
- [031: Tests in the game's project](../../docs/decision-records/031-tests-in-the-game-project.md)
- [035: One shape for actions](../../docs/decision-records/035-one-shape-for-actions.md)
- [039: Pausing for the menu and the map](../../docs/decision-records/039-modes-kept-across-pauses.md)
- [042: The camera clamped by tiles](../../docs/decision-records/042-camera-clamped-by-tiles.md)
- [044: Dialogue](../../docs/decision-records/044-dialogue.md)
- [050: The cutscene mode](../../docs/decision-records/050-cutscene-mode.md)
- [051: The scripted run at the spawn](../../docs/decision-records/051-scripted-run-at-the-spawn.md)
- [052: The talking door](../../docs/decision-records/052-talking-door.md)
- [053: Cancelling tasks](../../docs/decision-records/053-cancelling-tasks.md)
- [057: Tile kinds](../../docs/decision-records/057-tile-kinds.md)
- [058: A map of where the camera may be](../../docs/decision-records/058-camera-map.md)

## What it is

The main scene, `Main.tscn`, and what it puts around a world: the camera, the lights, the player's input, the game's modes, sound, the debug tools, and the playtester's controls. It also gives the world what to draw for each kind of entity.

## What it is not for

The game's rules, or how the image is made: the low-resolution view and the colour pass are in `look/`.

## `Main`

`Main` adds the camera, the lights, the player's input and the game's mode to a `WorldNode`, and switches worlds. Its worlds are the worlds of `levels/main.ldtk`, then a world for each text room in the library; the first plays unless the command line names another, and comma and period switch between them (`009 §4.3`, `011 §3.2`, `013 §4.6`). The game is drawn in a `WorldViewportContainer`, a viewport of its own at low resolution, and `Main`'s other children are drawn at the window's resolution.

`AssetLibrary` is the game's asset library: it makes each kind of entity's visual and gives each kind of tile's mesh, so that Core doesn't know the game's assets (`012 §3.1`, `020 §2.2`). It maps each kind of tile to its wrapper scene in `assets/tiles/`, and loads each kind's mesh, and each darkened copy, once (`057 §3.1`).

With `--probe=` and a name, `Main` runs that probe instead of playing; its code for that is under `#if TOOLS`, since probes are only in the editor's build (`031 §2.3`).

## The camera

`CameraRig` is the camera: a `Camera3D` with a Phantom Camera host, which puts it where the phantom camera of highest priority is, and two phantom cameras, the player's and the map's. Phantom Camera's nodes are GDScript classes; C# reaches them through the wrappers it ships, which can't create them, so the rig creates them from their scripts.

- The player's phantom camera follows a target, with no damping of its own, which the rig moves each frame: `CameraFraming` moves it toward the point of its `CameraMap` nearest the wanted framing, above the player, and up or down by where the player looks, as a critically damped spring (`010 §1.3`, `§2.1`, `042 §5.3`, `058 §7`). `CameraFraming` is built from the world when the rig starts to follow it, and the rig gives it the wanted framing, the view's size and the frame's game time each frame, and reports to telemetry what it wanted, the goal and the view's centre. See [rendering.md](../../docs/designs/rendering.md#scale) for how it frames.
- The rig reads the look from the world's `PlayerInput`, which the player's controller reads too, so the function that supplies it must have no side effects. It looks only while the game is playing, not while paused or in a cutscene. It looks only while `PlayerInput.CanLook`, after a hold, easing a look amount with a `Tween` that it kills when the look stops (`014`). Looking up or down is the player's hands, so its delay and pan run on real time (`030 §3.1`).
- The framing's spring runs on game time, so that the camera lags a moving player by the same distance at any speed. Its smoothing times are features, so that a configuration can tune them (`030 §4.4`, `058 §7.2`).
- The rig processes while the tree is paused, so that the camera can move, and the player doesn't look up or down then.

## The game's modes

`GameModeManager` holds the game's mode: playing, the menu or the map, on Escape and Tab or the gamepad's Start and Back, phasing, while the world asks for a pause, during which it steps the world's controllers on their paused path, and cutscenes. Every mode but playing, slowing for a phase and cutscenes pauses the tree, which stops the entities' controllers too (`013 §1.2`).

- **What stops while the game is paused.** The game's viewport pauses with the game, as does all of it that inherits its process mode, the world and the dialogue box included; what must run while paused, such as the camera rig, processes always. A container passes input on to its viewport only while it processes, so nothing in the game's viewport gets input while paused. When dialogue paused the game, the viewport processed always so that the box got input; cutscenes don't pause, so it no longer does (`050 §4.2`).
- **A tick is either played or paused, never both.** `GameModeManager` steps after the world, by its physics priority, so on the tick it pauses or unpauses the world, the world's turn has already passed (`022 §1.1`).
- **Gameplay speed.** Each physics tick, `GameModeManager` sets the engine's time scale to the feature `GameplaySpeed`, times the slowdown's factor (`030 §1.1`).
- **Slowing for a phase.** Before phasing, it slows the game down to the pause: its `SlowdownFactor` falls from 1 to `SlowestSlowdownFactor`, 0.05, over 0.5 real seconds, and the engine's time scale falls with it, which keeps 60 physics ticks a second and shortens each. It stops short of 0 to avoid very small steps (`015 §5.12`, `030 §1.2`). While phasing, the factor stays at the slowest, so that the camera, whose damping runs on game time, creeps on at the same pace rather than speeding up and snapping into place (`039 §1.3`). Probes run with nothing pausing for them, so their player aims while the world runs.
- **The menu and the map** are pauses for the user: opening one remembers the mode it was opened from, and closing it goes back to that mode, so a slowdown goes on where it stopped. While either is open, the factor is 1 (`039 §1.2`). The menu is a placeholder, `PauseMenuOverlay`, a panel that says the game is paused, which `Main` shows in the menu mode only (see [rendering.md](../../docs/designs/rendering.md)).
- **Cutscenes**, which dialogue plays in, don't pause the game: the world, its controllers, its scripts, animations and the camera run as in play, and the player's controller acts on no input but a script's, so that the player comes to rest by the normal path unless a script drives it. The same rules hold for every cutscene, whether the player talks or is scripted. Sound isn't muffled (`050 §1.2`, `§2.1`, `§3.3`). `DialogueBox` is the game's dialogue presenter, which Core's `IDialoguePresenter` describes, and `Main` gives it to each world it builds: presenting a dialogue starts it at once if none is presented, and otherwise queues it, to start as the one before ends, so that two asked for in one tick, such as by a `Fanout`, play one after the other, with a `ReentrantTask` that queues; each is presented by an async method on the world's tick, which ends when the dialogue does (`044 §2.5`, `051 §3.5`, `053 §3.1`); whether a file holds a title, which the world checks when it is built, is whether Dialogue Manager's resource for the file lists it among its cues. At its point in the tick, from playing or slowing for a phase, `GameModeManager` switches to the cutscene mode, at once and at full speed, when the world asks for a cutscene with its `CutsceneRequest`, which Core's runners of scripts and dialogues hold one for; when the world no longer asks, it goes back to playing. The mode's state, a `GameModeState.Cutscene`, carries the request, and is made again whenever the request changes, as holds come and go, so that what presents the cutscene follows (`051 §3.6`). It knows nothing of dialogue (`051 §3.1`, `§3.5`). What holds a cutscene is Core's: the game's mode state is for what Core doesn't read (`051 §3.2`). A phase being aimed is cancelled, since the player's controller gets none of the player's input. `Main` also gives each world it builds the game's `ScriptLibrary`. The menu and the map, opened from it, go back to it. When a dialogue ends, its task finishes in that tick, so what waits for it resumes then. `Main` drops a world it replaces, such as on F5, once the new one is in place, which stops the old one's tick, so that its scripts and dialogues unwind: the box drops a dialogue whose method unwinds, and the new world's dialogues don't wait for it (`053 §2.3`, `§3.1`).
- **The world as the map** (Neil). In the map, the map's phantom camera takes priority: it is over the middle of the world, back to where the whole world fits in the view, and the host tweens the camera there and back, in 0.25 real seconds whatever the gameplay speed: the host's tween runs on game time, so the rig multiplies its duration by the speed (`030 §3.1`). Closing the map keeps the game paused, in `EndingMap`, until the tween back is done: the rig tells `GameModeManager` when the player's camera takes over again. Opening the menu during the tween back and closing it skips the rest of the wait, which is accepted (`039 §1.1`, `§1.5`). The world itself is the map; nothing else is drawn for it.

`Letterbox` draws black bars across the top and bottom of the game's viewport, sliding in while its `Shown` is set and out when not, which `Main` sets while the cutscene's request asks for black bars. They stay as they are under the menu, since the tree is paused, and are hidden in the map. See [rendering.md](../../docs/designs/rendering.md) for how they are drawn.

`DialogueBox` is also the text box, in the game's viewport, on a layer above the colour pass, along its bottom edge, which `Main` shows in the cutscene mode only, and which shows itself only while a dialogue is presented or waits, so that a cutscene with no dialogue, such as a script's, has no box: the speaker's avatar, in a border, if `assets/avatars/` has one by the speaker's name, the speaker, the line, wrapped, and once the line has typed out, the responses, with the chosen one marked. The box itself, its panel and its margins, is a placeholder. Its font and size are the default of the theme `assets/fonts/dialogue_theme.tres`, so the font sets every line's height. The line is a `GarbledDialogueLabel`, Dialogue Manager's `DialogueLabel` with its scene's settings set in code, which types the line out, handles its inline tags and runs its inline mutations, and shows it garbled by `Garble`, by the fraction in the line's garble tag, from the line's ID and a step it counts up every half second, which shifts the garble (see [dialogue.md](../../docs/designs/dialogue.md)). Jump skips the typing, or once the line has typed out, goes on, and up and down choose a response. Whether the line has typed out comes from the label's `FinishedTyping`, not its `IsTyping`, which is false while an inline wait is awaited partway through the line. It gets each line from Dialogue Manager, which answers on Godot's frames with a .NET `Task`, a frame or more after it is asked, and waits for it with `ITick.UntilCompleted`, so that the line shows in the first tick after it comes; the player's going on from a line, which comes off the tick, in `_UnhandledInput`, is waited for with `ITick.While` (`044 §1.4`, `053 §3.2`). A dialogue file that doesn't exist, a garble tag that isn't valid, or an error in Dialogue Manager, fails the dialogue's task, which fails the world's tick.

`InteractPrompt` shows that the player can interact: while the world's `InteractTrigger` isn't null, it draws `assets/prompts/interact.png`, a placeholder speech bubble, centred over the top of that trigger's box, at whole pixels, in the game's viewport, under the colour pass and the menu. `Main` shows it while playing only (`052 §3`).

`PhaseAimMarker` is a placeholder, in the game's viewport, under the colour pass and the menu, for showing a phase being aimed: a box around the target, and arrows from the center of mass of the player and the target for the velocity each would leave with (`015 §5.7`, `039 §2.3`).

## Sound

- The bus layout, `default_bus_layout.tres`, has `Sfx` and `Music` buses, which send to `Master`.
- `CueSounds`, in a character's scene, or in a door's look, which `AssetLibrary` builds in code with placeholder sounds, plays the sounds for its entity's cues (see [models.md](../../docs/designs/models.md#sounds)). Characters' sounds are `AudioStreamPlayer3D`s, which the camera hears from where it is. It can only because `WorldViewportContainer` turns on 3D audio listening in the game's viewport, which the camera is in: a `SubViewport` leaves it off, and then no 3D sound is heard at all. Entities' sounds pause with the tree.
- `LaserBeam`, in the robot's scene, draws the robot's laser from its state each tick, and plays its sound from when it locks until it stops firing, and stops it if the laser gives up first: a sound that lasts as long as a stage follows the stage, not a cue (`035 §6.1`).
- The music, which has no place in the world, is an `AudioStreamPlayer` in `Main.tscn`, which processes always, so that it plays on while the game is paused, looping by its import settings (`021 §5.1`).
- `Muffler` muffles every sound with the low-pass filter on `Master`, which the layout leaves off: each frame, it sets the cutoff from the game's mode and `GameModeManager`'s slowdown factor, open while playing and in a cutscene, falling in octaves with the factor while slowing for a phase, most of the way early on, and closed while paused for any reason, at the cutoff the layout gives the filter (`021 §2.2`, `§2.5`).

## Playtesting

A playtester plays from a clone of the repository, started by `play.cmd`, which builds the game and runs it, or by `godot-editor.cmd`, which builds it and opens the Godot editor, whose Game tab has a toolbar that changes the game's speed, and edits maps in LDtk and configurations in a text editor (`017`).

- A *configuration* is a set of features: the defaults, or a JSONC file (JSON with comments and trailing commas) in `configurations/`, named after the file, that names only the features it changes. `Main` plays one, the defaults unless the command line names another with `--configuration=`; '[' and ']' switch between them without restarting the world, reading each file as it is switched to. The window's title names the world and the configuration.
- F5 reads the LDtk project and the list of configurations again, puts the current configuration in effect again, and restarts the current world, keeping both by name, with the player at the world's current respawn: the last one the player came within 2 tiles of, box to box, the feature `PlayerRespawnReach`. ';' and "'" restart the world at its previous and next respawn, in its levels' order. `Main` remembers each world's current respawn by its LDtk `iid`, which survives edits to the map; if it is gone, the player starts at the world's player start (`024 §5.1`).
- An error in a file doesn't stop play: `ErrorOverlay` shows it at the bottom of the window, at its resolution, and the game goes on without the project's worlds, or with the features it had.

## Debug tools

Paused, the debug tools still work: the `DebugOverlay`, the `CollisionDebug`, the `CameraDebug` and the rendering switches process while the tree is paused.

- `CameraDebug`, on F1, shows how the player's camera is framed, for debugging and tuning the framing and the painting: the world's `CameraMap`, on a quad behind the tiles' front layer, and the wanted framing and the view's centre as framed, which `CameraRig` reports to telemetry each frame, drawn through the tiles. `Main` adds one to each world, with the map of the rig's framing, and keeps whether it shows as it loads worlds. It makes the map's texture the first time it shows (`058 §2.3`).
- F2 shows the world's map markers. `Main` keeps whether they show as it loads worlds.
- 2 presents the debug dialogue, `dialogue/debug.dialogue`, from its title `start`, through Core's `DialogueRunner`, as a level's `Dialogue` entity does, so that it plays in a cutscene and queues after any dialogue already presented, and prints when what waits for it has resumed. Its lines have a wait, a speed change, two garble tags, and responses whose branches meet again.
- `DebugOverlay`, on F3, shows the frame rate and times and every telemetry value, at the window's resolution (`008 §2.3`).
- `CollisionDebug`, on F4, shows the collisions in the game as Godot does with visible collision shapes on: every enabled collision shape as Godot's own debug wireframe, a mesh added to the shape, for comparing bodies with their models, and every contact point as a small diamond, in Godot's colour for contacts, drawn through the bodies. The contacts are those of every body that reports them, which every entity does, so a contact between two entities shows as a point on each. Godot's own contact points can't be used, since the engine's API for them isn't exposed to scripts. It reads the shapes and contacts from the engine each frame while showing, and doesn't process while not.

## Open questions

- Input across the game's modes (issue #174). The player's input is polled with `Input.IsActionPressed`, which sees a held key whether or not a node handled its press, so a key that closes the menu or the map can reach play on the next played tick. The jump that ends a dialogue doesn't, since the player's controller counts a button held as a cutscene ends as no new press (`050 §4.3`); input that owns its own presses would do that for every mode, and for the camera and scripted input too. Input is read in several places, in two ways.

- The rest of the world as the map (Neil): the map pauses the tree and zooms out, but doesn't yet hide what moves or is expensive to draw, or show only the rooms the player has explored. Rooms already draw their own tiles (`020 §2.1`), so this would be revealing the explored ones, perhaps with a cheaper look each, and it means caching could unload a room's entities and colliders but not its look (`013`, Context). The zoom's tween is linear in distance, which does most of the visible zooming early; an exponential transition may feel more even at the sizes of a whole game, 16x16 to 32x32 rooms.
- Phantom Camera's lookahead and newer framed-follow features are described for its 2D camera only, and are unverified in 3D. Its limits are 2D only; `CameraRig` moves the target instead (`010 §2.1`).
- The game can hit the same race at exit as [the tests](../tests/architecture.md). A crash then comes after the last frame, so it loses nothing that was supposed to persist, as long as nothing that must happen is left to a finalizer: .NET doesn't promise to run them when a process exits. When the menu gets a way to quit, quitting and closing the window should share one path that saves and flushes files explicitly, then runs the garbage collector and waits for its finalizers, then quits.
