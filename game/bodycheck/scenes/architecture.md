# `game/bodycheck/scenes/`

Relevant decision records:
- [009: The first level loader](../../../docs/decision-records/009-level-loader.md)
- [011: Stitching LDtk levels into one world](../../../docs/decision-records/011-stitched-levels.md)
- [013: The world as a container of rooms](../../../docs/decision-records/013-world-of-rooms.md)
- [017: The playtesting and puzzle design workflow](../../../docs/decision-records/017-playtesting-workflow.md)
- [021: Sound](../../../docs/decision-records/021-sound.md)
- [024: Playtesting feedback](../../../docs/decision-records/024-playtesting-feedback.md)
- [031: Tests in the game's project](../../../docs/decision-records/031-tests-in-the-game-project.md)
- [053: Cancelling tasks](../../../docs/decision-records/053-cancelling-tasks.md)
- [063: World state, and talking to entities](../../../docs/decision-records/063-world-state-and-talking-to-entities.md)

## What it is

The scripts of the game's top-level scenes, the ones Godot switches between, each of which puts the game's other parts together. Their scenes are in `game/scenes/`, named for their scripts in snake case. For now there is one, `Play`, on `play.tscn`, the main scene. A main menu, or a cutscene played on its own rather than in a world, would go here.

## What it is not for

Anything a top-level scene uses that could be built and tested without it, such as a mode, the camera or an overlay: those are parts of their own, which these scenes put together. Nor for cutscenes in the world, which are scripts and dialogues.

## `Play`

`Play` adds the camera, the environments and the game's mode to a `WorldNode`, gives its player's controller the user's input, and switches worlds. Its worlds are the worlds of `levels/main.ldtk` (`065 §1.7`); if the project has an error, the error is shown and the worlds read before are kept, or, at the start, the only world is an empty room. The first plays unless the command line names another, and comma and period switch between them (`009 §4.3`, `011 §3.2`, `013 §4.6`). The game is drawn in a `WorldViewportContainer`, a viewport of its own at low resolution, and `Play`'s other children are drawn at the window's resolution.

- **What it gives each world.** It gives each world it builds the game's asset library, the game's `ScriptLibrary`, the dialogue box as its dialogue presenter, and an environment mixer. It fills in a `GameState` from each world's data before it builds the world, and the world reads it from then on (`063 §1.4`). It gives the dialogue box the world and its `GameState`.
- **What it shows by mode.** It shows the pause menu in the menu mode only, the dialogue box in the cutscene mode only, the interact prompt while playing only, and sets the letterbox's `Shown` while the cutscene's request asks for black bars.
- **Replacing a world.** `Play` drops a world it replaces, such as on F5, once the new one is in place, which stops the old one's tick, so that its scripts and dialogues unwind: the dialogue box drops a dialogue whose method unwinds, and the new world's dialogues don't wait for it (`053 §2.3`, `§3.1`).
- **Music.** The music, which has no place in the world, is an `AudioStreamPlayer` in `play.tscn`, which processes always, so that it plays on while the game is paused, looping by its import settings (`021 §5.1`).
- **Probes.** With `--probe=` and a name, `Play` runs that probe instead of playing; its code for that is under `#if TOOLS`, since probes are only in the editor's build (`031 §2.3`).

## Playtesting

A playtester plays from a clone of the repository, started by `play.cmd`, which builds the game and runs it, or by `godot-editor.cmd`, which builds it and opens the Godot editor, whose Game tab has a toolbar that changes the game's speed, and edits maps in LDtk and configurations in a text editor (`017`).

- A *configuration* is a set of features: the defaults, or a JSONC file (JSON with comments and trailing commas) in `game/configurations/`, named after the file, that names only the features it changes. `Play` plays one, the defaults unless the command line names another with `--configuration=`; '[' and ']' switch between them without restarting the world, reading each file as it is switched to. The window's title names the world and the configuration.
- F5 reads the LDtk project and the list of configurations again, puts the current configuration in effect again, and restarts the current world, keeping both by name, with the player at the world's current respawn: the last one the player came within 2 tiles of, box to box, the feature `PlayerRespawnReach`. ';' and "'" restart the world at its previous and next respawn, in its levels' order. `Play` remembers each world's current respawn by its LDtk `iid`, which survives edits to the map; if it is gone, the player starts at the world's player start (`024 §5.1`).
- An error in a file doesn't stop play: the error overlay shows it, and the game goes on without the project's worlds, or with the features it had.
- `Play` also has the debug keys, which [`debugging/`](../debugging/architecture.md) describes.

## Open questions

- The game can hit the same race at exit as [the tests](../tests/architecture.md). A crash then comes after the last frame, so it loses nothing that was supposed to persist, as long as nothing that must happen is left to a finalizer: .NET doesn't promise to run them when a process exits. When the menu gets a way to quit, quitting and closing the window should share one path that saves and flushes files explicitly, then runs the garbage collector and waits for its finalizers, then quits.
