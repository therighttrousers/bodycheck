# `game/tests/`

Relevant decision records:
- [031: Tests in the game's project](../../docs/decision-records/031-tests-in-the-game-project.md)

## What it is

All the tests, in gdUnit4Net, which plain `dotnet test` runs. They are part of the game's project, compiled into its assembly, since gdUnit4Net runs them inside Godot, which loads only the project's assembly. The test packages and the tests are only in the editor's build, `Debug`; exports leave them out (`031 §2.3`).

## What it is not for

Measurements that print what they find, for tuning: those are probes.

## How tests run

- A suite marked `[RequireGodotRuntime]` runs inside Godot; the others run without it. gdUnit4Net's analyzer requires the mark on any suite that uses Godot's types, even its vectors, which work without the engine.
- For the suites that run inside it, Godot runs headless at a fixed frame rate, so physics ticks faster than real time. It runs under the game's settings, and can load anything in the game by `res://` path. The main scene is loaded but not run, and the autoloads are in the tree (`031 §1.1`).
- A test reads the game's levels by `res://` path, to check that the world reads.
- `RoomRun` runs a world of one room, with the game's asset library, and steps it with the player's input. Every suite that frees a world frees it with `RoomRun.FreeAndFinalize`, which then runs the garbage collector and waits for its finalizers: Godot can crash at exit if a C# finalizer runs while its .NET shutdown releases the same objects, a race in Godot (godotengine/godot#89188, #92076, #123521).
- gdUnit4's `AssertThrown` checks the innermost exception rather than the one thrown, so a test of a reader, which wraps a parser's exception in its own to name the file, gets the exception with `Exceptions.Thrown` and checks it with ordinary assertions.

## Open questions

- gdUnit4Net is used outside its stated support (.NET 8 and 9, Godot 4.4 to 4.5), and `gdUnit4.api` is a prerelease. To be revisited when it releases.
