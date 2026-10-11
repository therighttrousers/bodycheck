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
1. Run `git clone <repo-url>`; `cd` to the repo
2. Run `.\install_godot.ps1`

# Layout

- `game/`: the Godot project (scenes, node scripts, levels, assets)
- `src/BodyCheck.Core/`: game rules as a plain C# library with no Godot dependency
- `tests/BodyCheck.Core.Tests/`: xUnit tests for the library
- `docs/`: design docs and decision records
- `Godot/`: the engine, installed by `install_godot.ps1` (not in Git)

# Common tasks

- Build everything: `dotnet build`
- Run the unit tests: `dotnet test`
- Open the editor: `.\Godot\Godot.exe --path game --editor`
- Run the game: `dotnet build`, then `.\Godot\Godot.exe --path game`. Running from the command line doesn't build the C# code first; pressing Play in the editor does.

# Controls

- Move: A/D, arrow keys, or gamepad left stick or D-pad
- Jump: Space, or gamepad A. Hold to jump higher.
- Next or previous test room: Page Down or Page Up, or gamepad right or left shoulder button

# Test rooms

The named test rooms are in `src/BodyCheck.Core/Rooms.cs`. To start in a particular room, pass its name after `--`:
`.\Godot\Godot.exe --path game -- "Robot ride"`
