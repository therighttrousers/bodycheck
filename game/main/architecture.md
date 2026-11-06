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
- [044: Dialogue](../../docs/decision-records/044-dialogue.md)

## What it is

The main scene, `Main.tscn`, and what it puts around a world: the camera, the lights, the player's input, the game's modes, sound, the debug tools, and the playtester's controls. It also gives the world what to draw for each kind of entity.

## What it is not for

The game's rules, or how the image is made: the low-resolution view and the colour pass are in `look/`.

## `Main`

`Main` adds the camera, the lights and the player's input to a `WorldNode`, and switches worlds. It makes each world pausable, since the game's viewport, which the world is in, processes always (see below). Its worlds are the worlds of `levels/main.ldtk`, then a world for each text room in the library; the first plays unless the command line names another, and comma and period switch between them (`009 §4.3`, `011 §3.2`, `013 §4.6`). The game is drawn in a `WorldViewportContainer`, a viewport of its own at low resolution, and `Main`'s other children are drawn at the window's resolution.

`AssetLibrary` is the game's asset library: it makes each kind of entity's visual and gives the tile's mesh, so that Core doesn't know the game's assets (`012 §3.1`, `020 §2.2`).

With `--probe=` and a name, `Main` runs that probe instead of playing; its code for that is under `#if TOOLS`, since probes are only in the editor's build (`031 §2.3`).

## The camera

`CameraRig` is the camera: a `Camera3D` with a Phantom Camera host, which puts it where the phantom camera of highest priority is, and two phantom cameras, the player's and the map's. Phantom Camera's nodes are GDScript classes; C# reaches them through the wrappers it ships, which can't create them, so the rig creates them from their scripts.

- The player's phantom camera follows a target, which the rig moves each frame, above the player, up or down by where the player looks, and clamped to the world's tile grid, its rooms' bounding box (`010 §1.3`, `§2.1`, `011 §4.1`).
- The rig reads the look from the world's `PlayerInput`, which the player's controller reads too, so that function must have no side effects. It looks only while `PlayerInput.CanLook`, after a hold, easing a look amount with a `Tween` that it kills when the look stops (`014`). Looking up or down is the player's hands, so its delay and pan run on real time (`030 §3.1`).
- The camera's follow damping, a smoothing time of 0.15 seconds, runs on game time, so that the camera lags a moving player by the same distance at any speed. It isn't scaled while the player looks, since following a look and snapping back from it barely differ at reasonable speeds (`030 §4.4`).
- The rig processes while the tree is paused, so that the camera can move, and the player doesn't look up or down then.

## The game's modes

`GameModeManager` holds the game's mode: playing, the menu or the map, on Escape and Tab or the gamepad's Start and Back, phasing, while the world asks for a pause, during which it steps the world's controllers on their paused path, and dialogue. Every mode but playing and slowing for a phase pauses the tree, which stops the entities' controllers too (`013 §1.2`).

- **What stops while the game is paused.** A node that processes passes input on to the viewports in it only while it processes, and the dialogue box, in the game's viewport, needs input while the game is paused for dialogue. So `WorldViewportContainer` processes always, and so does everything in the game's viewport that inherits its process mode. What must stop while paused says so itself: the world is pausable, and the entities in it inherit that. Making the viewport pausable instead stopped the map's zoom-out. This is to be redesigned with the rest of the input (issue #174).
- **A tick is either played or paused, never both.** `GameModeManager` steps after the world, by its physics priority, so on the tick it pauses or unpauses the world, the world's turn has already passed (`022 §1.1`).
- **Gameplay speed.** Each physics tick, `GameModeManager` sets the engine's time scale to the feature `GameplaySpeed`, times the slowdown's factor (`030 §1.1`).
- **Slowing for a phase.** Before phasing, it slows the game down to the pause: its `SlowdownFactor` falls from 1 to `SlowestSlowdownFactor`, 0.05, over 0.5 real seconds, and the engine's time scale falls with it, which keeps 60 physics ticks a second and shortens each. It stops short of 0 to avoid very small steps (`015 §5.12`, `030 §1.2`). While phasing, the factor stays at the slowest, so that the camera, whose damping runs on game time, creeps on at the same pace rather than speeding up and snapping into place (`039 §1.3`). Probes run with nothing pausing for them, so their player aims while the world runs.
- **The menu and the map** are pauses for the user: opening one remembers the mode it was opened from, and closing it goes back to that mode, so a slowdown goes on where it stopped. While either is open, the factor is 1 (`039 §1.2`). The menu is a placeholder, `PauseMenuOverlay`, a panel that says the game is paused, which `Main` shows in the menu mode only (see [rendering.md](../../docs/designs/rendering.md)).
- **Dialogue** pauses the game (`044 §2.1`). `DialogueBox` is the game's dialogue presenter, which Core's `IDialoguePresenter` describes: presenting a dialogue only queues it, and returns a task made with the tick's `NewCompletion`. At its point in the tick, from playing or slowing for a phase, `GameModeManager` switches to the dialogue mode, at once, and starts each queued dialogue in turn; when none is left, it goes back to playing, and a phase still being aimed slows down again from the start (`044 §2.5`, `§2.6`). The menu and the map, opened from it, go back to it. When a dialogue ends, the box finishes its task, so what waits for it resumes in the next played tick (`044 §2.4`).
- **The world as the map** (Neil). In the map, the map's phantom camera takes priority: it is over the middle of the world, back to where the whole world fits in the view, and the host tweens the camera there and back, in 0.25 real seconds whatever the gameplay speed: the host's tween runs on game time, so the rig multiplies its duration by the speed (`030 §3.1`). Closing the map keeps the game paused, in `EndingMap`, until the tween back is done: the rig tells `GameModeManager` when the player's camera takes over again. Opening the menu during the tween back and closing it skips the rest of the wait, which is accepted (`039 §1.1`, `§1.5`). The world itself is the map; nothing else is drawn for it.

`DialogueBox` is also the text box, in the game's viewport, on a layer above the colour pass, along its bottom edge, which `Main` shows in the dialogue mode only: the speaker's avatar, if `assets/avatars/` has one by the speaker's name, the speaker, the line, wrapped, and once the line has typed out, the responses, with the chosen one marked. The box itself, its panel and its margins, is a placeholder. Its font and size are the default of the theme `assets/fonts/dialogue_theme.tres`, so the font sets every line's height. The line is a `GarbledDialogueLabel`, Dialogue Manager's `DialogueLabel` with its scene's settings set in code, which types the line out, handles its inline tags and runs its inline mutations, and shows it garbled by `Garble`, by the fraction in the line's garble tag, from the line's ID and a step it counts up every half second, which shifts the garble (see [dialogue.md](../../docs/designs/dialogue.md)). Jump skips the typing, or once the line has typed out, goes on, and up and down choose a response. It gets each line from Dialogue Manager, which answers on Godot's frames with a .NET `Task`, a frame or more after it is asked; that is fine for the box, which only draws, while the world is paused (`044 §1.4`). A dialogue file that doesn't exist, or an error in Dialogue Manager, fails the dialogue's task, which fails the world's next played tick.

`PhaseAimMarker` is a placeholder, in the game's viewport, under the colour pass and the menu, for showing a phase being aimed: a box around the target, and arrows from the center of mass of the player and the target for the velocity each would leave with (`015 §5.7`, `039 §2.3`).

## Sound

- The bus layout, `default_bus_layout.tres`, has `Sfx` and `Music` buses, which send to `Master`.
- `CueSounds`, in a character's scene, or in a door's look, which `AssetLibrary` builds in code with placeholder sounds, plays the sounds for its entity's cues (see [models.md](../../docs/designs/models.md#sounds)). Characters' sounds are `AudioStreamPlayer3D`s, which the camera hears from where it is. It can only because `WorldViewportContainer` turns on 3D audio listening in the game's viewport, which the camera is in: a `SubViewport` leaves it off, and then no 3D sound is heard at all. Entities' sounds pause with the tree.
- `LaserBeam`, in the robot's scene, draws the robot's laser from its state each tick, and plays its sound from when it locks until it stops firing, and stops it if the laser gives up first: a sound that lasts as long as a stage follows the stage, not a cue (`035 §6.1`).
- The music, which has no place in the world, is an `AudioStreamPlayer` in `Main.tscn`, which processes always, so that it plays on while the game is paused, looping by its import settings (`021 §5.1`).
- `Muffler` muffles every sound with the low-pass filter on `Master`, which the layout leaves off: each frame, it sets the cutoff from the game's mode and `GameModeManager`'s slowdown factor, open while playing, falling in octaves with the factor while slowing for a phase, most of the way early on, and closed while paused for any reason, at the cutoff the layout gives the filter (`021 §2.2`, `§2.5`).

## Playtesting

A playtester plays from a clone of the repository, started by `play.cmd`, which builds the game and runs it, or by `godot-editor.cmd`, which builds it and opens the Godot editor, whose Game tab has a toolbar that changes the game's speed, and edits maps in LDtk and configurations in a text editor (`017`).

- A *configuration* is a set of features: the defaults, or a JSONC file (JSON with comments and trailing commas) in `configurations/`, named after the file, that names only the features it changes. `Main` plays one, the defaults unless the command line names another with `--configuration=`; '[' and ']' switch between them without restarting the world, reading each file as it is switched to. The window's title names the world and the configuration.
- F5 reads the LDtk project and the list of configurations again, puts the current configuration in effect again, and restarts the current world, keeping both by name, with the player at the world's current respawn: the last one the player came within 2 tiles of, box to box, the feature `PlayerRespawnReach`. ';' and "'" restart the world at its previous and next respawn, in its levels' order. `Main` remembers each world's current respawn by its LDtk `iid`, which survives edits to the map; if it is gone, the player starts at the world's player start (`024 §5.1`).
- An error in a file doesn't stop play: `ErrorOverlay` shows it at the bottom of the window, at its resolution, and the game goes on without the project's worlds, or with the features it had.

## Debug tools

Paused, the debug tools still work: the `DebugOverlay`, the `CollisionDebug` and the rendering switches process while the tree is paused.

- F2 shows the world's map markers. `Main` keeps whether they show as it loads worlds.
- 2 presents the debug dialogue, `dialogue/debug.dialogue`, from its title `start`, and prints when what waits for it has resumed. Its lines have a wait, a speed change, two garble tags, and responses whose branches meet again.
- `DebugOverlay`, on F3, shows the frame rate and times and every telemetry value, at the window's resolution (`008 §2.3`).
- `CollisionDebug`, on F4, shows the collisions in the game as Godot does with visible collision shapes on: every enabled collision shape as Godot's own debug wireframe, a mesh added to the shape, for comparing bodies with their models, and every contact point as a small diamond, in Godot's colour for contacts, drawn through the bodies. The contacts are those of every body that reports them, which every entity does, so a contact between two entities shows as a point on each. Godot's own contact points can't be used, since the engine's API for them isn't exposed to scripts. It reads the shapes and contacts from the engine each frame while showing, and doesn't process while not.

## Open questions

- Input across the game's modes (issue #174). The player's input is polled with `Input.IsActionPressed`, which sees a held key whether or not a node handled its press, so the jump that ends a dialogue, or a key that closes the menu or the map, can reach play on the next played tick. Input is read in several places, in two ways, and the game's viewport processes always only so that its UI gets input.

- The rest of the world as the map (Neil): the map pauses the tree and zooms out, but doesn't yet hide what moves or is expensive to draw, or show only the rooms the player has explored. Rooms already draw their own tiles (`020 §2.1`), so this would be revealing the explored ones, perhaps with a cheaper look each, and it means caching could unload a room's entities and colliders but not its look (`013`, Context). The zoom's tween is linear in distance, which does most of the visible zooming early; an exponential transition may feel more even at the sizes of a whole game, 16x16 to 32x32 rooms.
- Phantom Camera's lookahead and newer framed-follow features are described for its 2D camera only, and are unverified in 3D. Its limits are 2D only; `CameraRig` clamps instead (`010 §2.1`).
- The game can hit the same race at exit as [the tests](../tests/architecture.md). A crash then comes after the last frame, so it loses nothing that was supposed to persist, as long as nothing that must happen is left to a finalizer: .NET doesn't promise to run them when a process exits. When the menu gets a way to quit, quitting and closing the window should share one path that saves and flushes files explicitly, then runs the garbage collector and waits for its finalizers, then quits.
