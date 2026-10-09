# Unnamed 2D Metroidvania

TODO: Write

# Initial dev setup

Prerequisites:
- Windows
- Git
- .NET SDK 10
- Visual Studio Code

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
