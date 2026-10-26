# Body Check

Design docs and decision records are in [`docs/`](docs/AGENTS.md). Read that before implementing anything.

## Setup and running

- Run `.\scripts\dev-setup.ps1` before the first build. It installs Godot into `Godot/`, and `BodyCheck.Core` doesn't compile without the GodotSharp assembly it brings.
- Godot is `.\Godot\Godot.exe`, not on the PATH.
- Running the game from the command line doesn't build the C# code. Run `dotnet build` first.
- `dotnet test` runs all the tests, including the ones that run headless inside Godot.

## Comments

- **Don't cite decision records in code.** A record is permanent, but a later one can supersede it, and a comment that cites it then goes stale. Where a comment needs the design behind the code, link the design doc that states it, which is kept current; otherwise explain in the comment itself.

## Subsystems in `BodyCheck.Core`

Core is split into subsystems, one per subdirectory of `src/BodyCheck.Core/`, each in a namespace that matches its directory (decision record 012). Each has an `intent.md`; so does the root. Read the relevant ones before adding code to Core.

- **No cycles.** If subsystem A uses B, B must not use A, directly or through others. The root uses no subsystem; every subsystem may use the root.
- **Keep references light.** A subsystem uses another through a few types, not throughout. Every reference is a `using` directive, so a new `using BodyCheck.Core.X;` in a subsystem is a new dependency: check it against the rules above and the `intent.md` files. Doc comments count too: a `<see cref>` into another subsystem is a reference.
- **The root is for shared things only:** utilities and constants that more than one subsystem, or the game, needs. Constants go in `Constants`.
- **Put code where its `intent.md` says it belongs.** If nowhere fits, or the code would need a dependency the rules forbid, stop and raise it with the user: the answer may be to change a boundary, move a type, or add a subsystem.
- **No empty subsystems.** Add a subsystem when there is code for it, with its `intent.md`, and update `docs/designs/architecture.md`.

### Writing an `intent.md`

Three short sections, each a sentence or two:

- **What it is:** what the subsystem holds and what it is for, at a high level.
- **How it relates to the rest:** what it depends on and what depends on it, described by role ("the entities it controls"), without naming other subsystems' types.
- **What it is not for:** what someone might be tempted to put here that belongs elsewhere.

Describe intent, not inventory: don't list the subsystem's types, which the directory already shows. Change an `intent.md` when a subsystem's purpose or boundaries change, in the same change.

## Git

- `main` is protected: nothing can be pushed to it directly. Changes go through a PR, and CI must pass on the PR's branch.
- PRs are squash-merged, so a branch's commits can be messy if they need to be. The PR's title and description become the commit message on `main`.
- A pre-commit hook runs `dotnet format` on the staged `.cs` files.

## CI

CI runs on every PR branch, on Windows:

- **Build and test:** runs `scripts\dev-setup.ps1`, then `dotnet build` and `dotnet test`, in Debug.
- **No changes:** fails if the build or the tests change any file in the repository. Commit what Godot generates, such as the `.uid` file for a new script, and keep the order in which the Godot editor writes `project.godot`.
- **Formatting:** `dotnet format --verify-no-changes` over every file, not only those the pre-commit hook saw.
