# Unnamed 2.5D Metroidvania

A metroidvania in low-resolution 3D, in the style of mid-1990s games, in which the player is a maintenance robot whose job is to switch on all of a facility's defenses, so the world grows more dangerous with each one. Gameplay emphasizes physics-based movement and physics puzzles.

This README is written for game developers, not players.

See
- [docs/designs/high-level-design.md](docs/designs/high-level-design.md) for more high-level details and a table of contents.
- [docs/designs/architecture.md](docs/designs/architecture.md) for tech details, directory structure, and system components.

## Initial dev setup

Prerequisites:
- Windows
- Git with LFS extension to efficiently handle large binaries
  - Download Git from https://git-scm.com/, install
  - Download Git LFS from https://git-lfs.com/, install, run `git lfs install` in a command line once
- A GitHub account
- GitHub CLI
- .NET SDK >=10
- If you want to edit the code, Visual Studio Code
- If you want to edit the map, version 1.5.3 of the [LDtk editor](https://ldtk.io/download/)
- If you want to edit models, Blender
- A parent directory to clone the repository into, **which must not contain spaces anywhere in its path**

Steps:
1. Open a terminal where `dotnet --version` prints a version >=10.0.0.
2. Run `gh auth login`, and choose options to log in to GitHub.com, using HTTPS, with a web browser.
3. Change to some `<parent-directory>`.
4. Run `git clone https://github.com/therighttrousers/bodycheck.git`.
5. Run `cd bodycheck`.
6. Run `.\dev-setup.ps1` to install Godot locally, install C# dependencies, and set up pre-commit hooks.
7. If you want to edit the code:
   - Run `code` to open a new VS Code window. (You can close the terminal after this.)
   - In VS Code, open the `<parent-directory>\bodycheck` directory.
   - Install the extensions recommended for this repository.
   - Press Ctrl+backtick to open a new terminal in VS Code.
8. Run `dotnet build && dotnet test` to build everything and run the tests.
9. Run `play.cmd` to launch the game.
10. If you want to edit the map:
    - Launch LDtk.
    - Open `<parent-directory>\bodycheck\game\levels\main.ldtk`.

## Common tasks

- Build everything: `dotnet build`
- Run the unit tests: build, then run `dotnet test`
- Build and run the game: run `play.cmd` or double-click it in File Explorer
- Run the game from Godot's editor: double-click or run `godot-editor.cmd`, then press the `▶` button to build and run

## Godot editor

A pinned version of Godot is installed (by `dev-setup.ps1`) in the `Godot` directory of the repo root. Please don't use a different version of Godot to develop the game. To pass arguments to Godot itself, run `.\Godot\Godot.exe --path game <more-arguments> -- <game-arguments>`; otherwise you can just run `play.cmd` or `godot-editor.cmd`.

Most day-to-day coding doesn't use Godot's editor much because the game world is programmatically generated from (tile-based) LDtk maps, and because agents are much better at writing code than using the editor. However, they can adeptly create and edit `project.godot` and `*.tscn` (scene) files. When they do, the Godot editor will prompt you to reload the project.

We primarily use the editor for
- Importing models and transforming them.
- Creating `AnimationTree` state machines.
- Starting the game with Godot's debug toolbar, which allows dynamically changing the timescale (pause, step, slow, fast).

## Controls

- Move/look:
  - W/A/S/D (left hand)
  - Arrow keys (right hand)
  - Gamepad left stick
  - Gamepad D-pad
- Jump/phase/boost:
  - J/K/L (right hand)
  - C/X/Z (left hand)
  - Gamepad A/X/RT (these are Xbox names; uses buttons in the same positions on other controllers)
- Map: Tab, or gamepad Back. Pauses the game and zooms out to show the whole world; press again to zoom back in.
- Pause: Escape, or gamepad Start. There's no menu yet.
- Previous or next world: ','/'.' or gamepad left/right shoulder button
- F5: read the worlds and configurations again, and restart the world
- F6/F7: previous or next configuration
- F3: debug overlay on or off, with measurements for tuning and level design. Pass `--overlay` after `--` to start with it on.
- F4: collision shapes as wireframes on or off. Pass `--shapes` after `--` to start with them on.

Hold the jump button to jump higher.

Phase goes through the robot, block or platform nearest the player. Holding it with one in reach, or coming within reach of one while holding it, slows the game to a pause, with arrows to aim with; release it while holding a direction to phase that way, or with no direction to cancel.

Rendering switches:
- 1: low resolution on or off
- 2: next low-resolution size
- 3: next anti-aliasing level
- 4: colour pass on or off
- 5: dither on or off
- 6: the palette, in place of the game, on or off
- [ and ]: the dither's lightness spread, down and up
- ; and ': the dither's chroma spread, down and up

The window's title shows the rendering switches' state.

## Worlds

The game's worlds are the worlds of `game/levels/main.ldtk`, made in [LDtk](https://ldtk.io/) with its multi-worlds on, so that they all share its layers and entities, followed by a world for each named test room in `src/BodyCheck.Core/WorldData/RoomLibrary.cs`. The first plays by default. To start in a particular one, pass its name after `--`:
`.\Godot\Godot.exe --path game -- Phase`, or `play.cmd "My cool test room"`. Put quotes around multi-word room names.

To make a new world for a puzzle, add a world to `main.ldtk` in LDtk. Save in LDtk, then press F5 in the game to play the change.

## Configurations

A configuration is a set of gameplay values to try, such as a stronger jump. Each is a JSONC file (JSON with comments) in `game/configurations/` that names only the values it changes; see `strong-jump.jsonc`. The values and their defaults are the properties of `src/BodyCheck.Core/Features.cs`. The game starts with the defaults, unless a configuration is named after `--` with `--configuration=`, for example `play.cmd --configuration=strong-jump`. F6 and F7 switch between configurations, and the window's title shows which is playing. Edit a file in any text editor, then press F5, or switch to it, to play the change. A mistake in a file is shown at the bottom of the window, and the game goes on without it.
