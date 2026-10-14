# Unnamed 2D Metroidvania

TODO: Write

# Initial dev setup

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
3. Run `.\dev-setup.ps1`.
4. Open the repo folder in VS Code and install the extensions recommended for this repository.

# Layout

- `game/`: the Godot project (scenes, node scripts, levels, assets)
- `src/BodyCheck.Core/`: game rules as a C# library
- `tests/BodyCheck.Core.Tests/`: xUnit tests for the library
- `tests/BodyCheck.Game.Tests/`: tests that run inside Godot, through gdUnit4Net
- `docs/`: design docs and decision records
- `Godot/`: the engine, installed by `dev-setup.ps1` (not in Git)

# Common tasks

- Build everything: `dotnet build`
- Run the unit tests: `dotnet test`
- Open the editor: `.\Godot\Godot.exe --path game --editor`
- Run the game: `dotnet build`, then `.\Godot\Godot.exe --path game`. Running from the command line doesn't build the C# code first; pressing Play in the editor does.

# Controls

- Move: A/D, arrow keys, or gamepad left stick or D-pad
- Jump: Space, or gamepad A. Hold to jump higher.
- Previous or next test room: ','/'.' or gamepad left/right shoulder button

Rendering switches, for judging the look. The window's title shows their state. The room named "Look" has shapes to judge it on.

- 1: low resolution on or off
- 2: next low-resolution size
- 3: next anti-aliasing level
- 4: colour pass on or off
- 5: dither on or off
- 6: the palette, in place of the game, on or off
- [ and ]: the dither's lightness spread, down and up
- ; and ': the dither's chroma spread, down and up

# Test rooms

The named test rooms are in `src/BodyCheck.Core/Rooms.cs`. To start in a particular room, pass its name after `--`:
`.\Godot\Godot.exe --path game -- "Robot ride"`
