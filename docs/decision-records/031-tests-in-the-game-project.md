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

## 2. Release exports

Checking 1.2 needed an export, which the game hadn't had (Neil).

- [x] 2.1 How the game is exported. Decision (Neil): by `scripts/build-release.ps1`, which runs Godot's console executable headless with the "Windows Desktop" preset, into `build/windows_release_x86_64/`, and fails on Godot's exit code. CI runs it after the tests, before the check for changed files. The plain executable is a GUI program, which PowerShell neither waits for nor gets an exit code from, so an export through it could never fail CI (Claude). Exporting from the editor isn't supported: the editor stores the last export path in the preset, and doesn't create the directories a path needs, so the preset's export path is empty. The export templates come in one archive of about 1.1 GB, from GitHub's releases, like Godot itself; `scripts/dev-setup.ps1` installs only the Windows templates. Caching the download in CI waits until it proves slow.
- [x] 2.2 What the export holds. A pack explorer found `probes/`, `tests/` and `gdunit4_testadapter_v5/` in the first export (Neil). Decision (Neil): the preset leaves those out, and LDtk's backups, and includes `levels/` and `configurations/`. The build outputs are embedded, with debug symbols, for readable stack traces from release builds.
- [x] 2.3 Probes and tests in exports. Excluding `probes/` from the pack still left the probes compiled in, since `Main` starts and ticks them. Neither probes nor tests have a use in any export: probes are measurements a developer runs from the repository, on the editor's binary, and nothing needs tests in a debug export either; §1.2 kept them there only because its condition named `ExportRelease` (Claude). Decision (Neil): only the editor's build, `Debug`, has the tests, the test packages and the probes; `ExportDebug` and `ExportRelease` leave them all out. Godot defines `TOOLS` in exactly that configuration, so `Main`'s probe code is under `#if TOOLS`. This supersedes §1.2's keeping the tests in debug exports.
