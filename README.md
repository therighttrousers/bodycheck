# Unnamed 2.5D Metroidvania

A metroidvania in low-resolution 3D, in the style of mid-1990s games, in which the player is a maintenance robot whose job is to switch on all of a facility's defenses, so the world grows more dangerous with each one. Gameplay emphasizes physics-based movement and physics puzzles.

See [docs/designs/high-level-design.md](docs/designs/high-level-design.md) for more details.

This README is written for game developers, not players.

## Initial dev setup

Prerequisites:
- Windows
- Git with LFS extension to efficiently handle large binaries
  - Download Git from https://git-scm.com/, install
  - Download Git LFS from https://git-lfs.com/, install, run `git lfs install` once
- .NET SDK 10
- Visual Studio Code
- If you want to edit the map, the [LDtk editor](https://ldtk.io/download/)

Steps:
1. Run `git clone <repo-url>`.
2. `cd` to the repo directory.
3. Run `.\dev-setup.ps1` to install Godot locally, restore dependencies, and set up pre-commit hooks.
4. Open the repo folder in VS Code and install the extensions recommended for this repository.
5. Press Ctrl+`` ` `` (Ctrl+backtick) to open a terminal in VS Code.
6. Run `dotnet build && dotnet test` to build everything and run the tests.

See [docs/designs/architecture.md](docs/designs/architecture.md) for tech details, directory structure, and system components.

## Common tasks

- Build everything: `dotnet build`
- Run the unit tests: `dotnet test`
- Run the game from the terminal: `dotnet build && .\Godot\Godot.exe --path game` - note that building first is required
- Run the game without a terminal: double-click `play.cmd`, which builds the game and runs it
- Run the game from Godot's editor: double-click `godot-editor.cmd`, or run `.\Godot\Godot.exe --path game --editor`, then press the `▶` button to build and run. The editor's Game tab has a toolbar that changes the game's speed, which a game started any other way doesn't get

Development doesn't make much use of Godot's editor because the game world is programmatically generated from tile-based maps, and because agents are much better at writing config files and code than using the editor.

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

The debug overlay, the wireframes and the rendering switches below work while the game is paused.

Rendering switches, for judging the look. The window's title shows their state. The room named "Look" has shapes to judge it on.

- 1: low resolution on or off
- 2: next low-resolution size
- 3: next anti-aliasing level
- 4: colour pass on or off
- 5: dither on or off
- 6: the palette, in place of the game, on or off
- [ and ]: the dither's lightness spread, down and up
- ; and ': the dither's chroma spread, down and up

## Worlds

The game's worlds are the worlds of `game/levels/main.ldtk`, made in [LDtk](https://ldtk.io/) with its multi-worlds on, so that they all share its layers and entities, followed by a world for each named test room in `src/BodyCheck.Core/WorldData/RoomLibrary.cs`. The first plays by default. To start in a particular one, pass its name after `--`:
`.\Godot\Godot.exe --path game -- Phase`, or `play.cmd "My cool test room"`. Put quotes around multi-word room names.

To make a new world for a puzzle, add a world to `main.ldtk` in LDtk. Save in LDtk, then press F5 in the game to play the change.

## Configurations

A configuration is a set of gameplay values to try, such as a stronger jump. Each is a JSONC file (JSON with comments) in `game/configurations/` that names only the values it changes; see `strong-jump.jsonc`. The values and their defaults are the properties of `src/BodyCheck.Core/Features.cs`. The game starts with the defaults, unless a configuration is named after `--` with `--configuration=`, for example `play.cmd --configuration=strong-jump`. F6 and F7 switch between configurations, and the window's title shows which is playing. Edit a file in any text editor, then press F5, or switch to it, to play the change. A mistake in a file is shown at the bottom of the window, and the game goes on without it.
