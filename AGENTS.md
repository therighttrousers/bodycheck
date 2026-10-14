# Body Check

Design docs and decision records are in [`docs/`](docs/AGENTS.md). Read that before implementing anything.

## Setup and running

- Run `.\dev-setup.ps1` before the first build. It installs Godot into `Godot/`, and `BodyCheck.Core` doesn't compile without the GodotSharp assembly it brings.
- Godot is `.\Godot\Godot.exe`, not on the PATH.
- Running the game from the command line doesn't build the C# code. Run `dotnet build` first.
- `dotnet test` runs all the tests, including the ones that run headless inside Godot.

## Git

- `main` is protected: nothing can be pushed to it directly. Changes go through a PR, and CI must pass on the PR's branch.
- PRs are squash-merged, so a branch's commits can be messy if they need to be. The PR's title and description become the commit message on `main`.
- A pre-commit hook runs `dotnet format` on the staged `.cs` files.

## CI

CI runs on every PR branch, on Windows:

- **Build and test:** runs `dev-setup.ps1`, then `dotnet build` and `dotnet test`, in Debug.
- **No changes:** fails if the build or the tests change any file in the repository. Commit what Godot generates, such as the `.uid` file for a new script, and keep the order in which the Godot editor writes `project.godot`.
- **Formatting:** `dotnet format --verify-no-changes` over every file, not only those the pre-commit hook saw.
