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
- Run the game from Godot's editor: `.\Godot\Godot.exe --path game --editor`, then press the `▶` button to build and run

Development doesn't make much use of Godot's editor because the game world is programmatically generated from tile-based maps, and because agents are much better at writing config files and code than using the editor.

## Controls

- Move: A/D, arrow keys, or gamepad left stick or D-pad
- Look up or down: W/S, arrow keys, or gamepad left stick or D-pad
- Jump: Space, or gamepad A. Hold to jump higher.
- Map: Tab, or gamepad Back. Pauses the game and zooms out to show the whole world; press again to zoom back in.
- Pause: Escape, or gamepad Start. There's no menu yet.
- Previous or next world: ','/'.' or gamepad left/right shoulder button
- F3: debug overlay on or off, with measurements for tuning and level design. Pass `--overlay` after `--` to start with it on.
- F4: collision shapes as wireframes on or off. Pass `--shapes` after `--` to start with them on.

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

## Rooms

The game's rooms are the levels of `game/levels/main.ldtk`, made in [LDtk](https://ldtk.io/), followed by the named test rooms in `src/BodyCheck.Core/Rooms/RoomLibrary.cs`. The first level plays by default. To start in a particular room, pass its name after `--`:
`.\Godot\Godot.exe --path game -- "Robot ride"`
