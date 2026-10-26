# 031: Tests in the game's project

## Context

Inciting event (2026-10-26): issue #99. Test output was full of C# backtraces: about 21,700 lines at CI's `--verbosity normal`, over 6,000 in a developer's console.

The cause was the test project's view of the game. It was a Godot project of its own, `tests/BodyCheck.Tests`, with its settings copied from the game's and `game/assets` linked into it (`006 §5.1`). The player's wrapper scene, `assets/bob_the_robot/scene.tscn`, has a `CueSounds` node whose script is `res://main/CueSounds.cs` (`021`), which the test project couldn't reach. Every test that made a player printed six errors, each with a backtrace of about 45 frames, and ran with that node's script missing. One more error came at startup, from the main scene setting naming `res://main/Main.tscn`.

Goals: quiet test output, and tests that see the game as the game does.

## 1. Where the tests live

- [x] 1.1 How do the tests reach what asset scenes use from outside `game/assets`? Options:
  - (a) Link `game/main` into the test project too, beside `game/assets`, and leave it out of the test assembly's compile. Small, but each new kind of reference from an asset into the game needs another link (Claude).
  - (b) Add `CueSounds` to the player's model in code, in `AssetLibrary`, rather than in the scene. Works against setting up an entity's sounds in the editor.
  - (c) Turn off Godot's error printing in tests. Hides a real failure.
  - (d) Move the tests into the game's project, `game/tests/`. gdUnit4Net runs Godot in the test project's directory, and Godot loads only the assembly the project names, so the tests compile into the game's assembly. This is gdUnit4's usual layout (Neil asked, Claude laid out the trade-offs).
  - (d) drops the copied `project.godot`, which was a second source of the engine's settings, the junction, and the second import, and makes everything reachable by `res://` path, including the levels (`009 §4.2`). Against it: the game's project references the test packages, the tests are in the game's assembly unless exports leave them out, the autoloads and editor plugins load in tests, and `game/tests/` shows in the editor beside the artists' files.
  - Spiked, 2026-10-26 (Claude), with a probe test of the scene tree. The main scene doesn't run: Godot loads `Main.tscn` at startup but doesn't instantiate it, and the tree has no current scene. The `PhantomCameraManager` autoload is in the tree, which is harmless while no test makes a phantom camera. The editor plugin loads, then unloads, in Godot's start before the tests. Output fell to 481 lines with no errors, and all the tests passed.
  - Decision (Neil): (d).
- [x] 1.2 Keeping tests out of exports. The test packages and settings apply in every configuration but `ExportRelease`, which also leaves `tests/` and the test adapter's runner, `gdunit4_testadapter_v5/`, out of the compile. An `ExportRelease` build's output is the game's assembly, Core's and GodotSharp. Debug exports keep the tests.
- [x] 1.3 Real levels. Decision (Neil): tests read `game/levels` in place, by `res://` path, instead of from a copy in the build's output.
- [x] 1.4 Scripts' IDs. Godot writes a `.uid` file beside each script in its project. The test project's were ignored, since nothing refers to a test class by ID. Decision (Neil): commit the tests', as the game's are, so that one rule covers every script in the game's project.
