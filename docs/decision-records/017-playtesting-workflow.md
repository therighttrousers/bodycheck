# 017: The playtesting and puzzle design workflow

## Context

Inciting event (2026-10-20): work on issue #64, "Work out playtesting and puzzle design workflow; add support". Someone with little coding experience will playtest, design puzzles, and tune gameplay by changing `Features` (`016`), all at the same time.

Goals, from the issue:
- The playtester doesn't need VS Code and doesn't edit code.
- It is easy to try different combinations of features, called *configurations*.
- It is easy to load and play different maps.
- The tuning cycle is short: little time between "I want to try my puzzle with a stronger jump" and trying it.

Anti-goals, from the issue: storing features from the game, and a menu for changing them. Those wait for the developer menu.

What there is now: `Features` is a record whose defaults are its properties' initializers, set with `Features.Set`. `Main` plays the world of `levels/main.ldtk`, then a world of one room for each text room in `RoomLibrary`; the command line names the first to play, and comma and period switch worlds. The game reads LDtk files at run time through Godot's `FileAccess`, so a game run from the repository, not exported, reads the files as they are on disk.

## 1. How the playtester works

- [x] 1.1 What does the playtester run? Options:
  - (a) An exported build. No Godot or .NET needed, but `res://` is packed, so maps and configurations would have to be read from somewhere else, and every code change means a new export.
  - (b) The game from a clone of the repository, started by a script they double-click, which builds and runs it. Maps and configurations are the repository's files, edited in place, and changes are shared through Git.
  - (c) The Godot editor's play button. It builds the C# code, but the editor is more than they need.
  - Leaning (Claude): (b). Their maps and configurations are design work that we want in the repository, with their history, and the game already reads maps from it.
  - Decision (Neil): (b). The script is `play.cmd`, in the repository's root, since Windows opens a `.ps1` in Notepad when it is double-clicked. It builds only the game, then starts it, passing on its arguments.
- [ ] 1.2 How do their changes reach the repository? Git through a GUI, such as GitHub Desktop, on a branch of their own, with PRs as usual? Open (Neil).

## 2. Configurations

- [x] 2.1 Where do they live? Leaning (Claude): one JSON file each, in `game/configurations/`, named after the configuration. In the repository, since they are design work to share. Decision: as leaning. An example, `strong-jump.jsonc`, shows the format. Changed (Neil): the extension is `.jsonc`, so that editors know comments and trailing commas are allowed.
- [x] 2.2 What does a file hold? Options:
  - (a) Every feature, each required, as the convention for reading data says. Adding a feature means editing every file.
  - (b) Only the features that differ from the defaults. A missing one is the default; an unknown name or a value of the wrong type is an error, reported with where it is.
  - Leaning (Claude): (b). A configuration is a set of changes to try, and the defaults are explicit, in one place. It is an exception to "nothing is silently defaulted", but a stated one: the defaults are what a configuration is relative to. The cost: changing a default changes every configuration that doesn't set it.
  - Decision (Neil): (b). Names are the properties' names, as written in `Features`, case and all. Comments and trailing commas are allowed, so that a line can be commented out.
  - Found while implementing (Claude): System.Text.Json's source generator gives an init-only property that the text doesn't name its type's default, 0, not its initializer. `Features.Read` reads the text once to check it, with line numbers in errors, then lays the features it names over the defaults and reads the whole.
- [x] 2.3 How is one chosen? Leaning (Claude): like worlds. The game's configurations are the defaults, then each file in `game/configurations/`, by name. The command line can name one, and two keys switch to the previous and next; the window's title shows it beside the world. Switching configurations doesn't restart the world, since features take effect as they are read (`016 §3.1`). Decision (Claude): as leaning. `--configuration=` and a name on the command line; F6 and F7 switch, since switching is a developer's tool, like F3 and F4. A configuration is read from its file each time it is switched to, so an edited file needs no reload.

## 3. Maps

- [x] 3.1 Which maps? Options:
  - (a) Every LDtk project in `game/levels/`, each a world named after its file. A new map is a new project, made by saving a copy of `main.ldtk`.
  - (b) One project, `main.ldtk`, with LDtk's multi-worlds on, and each of its worlds a world in the game, named after it.
  - Decision (Neil): (b). Separate projects would each have their own layers, enums and entities, which would have to be kept in sync by hand. This supersedes `011 §3`'s one world per project: the reader now requires multi-worlds, so there is one shape to read. Neil converted `main.ldtk` in LDtk.

## 4. Reloading

- [x] 4.1 What does it reload? Leaning (Claude): one developer key, F5, reloads everything from disk, the lists of configurations and maps included, keeps the current configuration and world by name, and restarts the world. One key, so the playtester doesn't have to know what changed. Decision: as leaning.
- [x] 4.2 Should a reload restart the world, or should features apply without a restart, as switching configurations does? A restart is needed for a map's changes, and a puzzle is easier to judge from its start. Decision (Neil): it always restarts the world.
- [x] 4.3 Reload when a file changes on disk, without a key? Shorter still, but more machinery, and a half-saved file would reload. Decision (Neil): not now; the key only.
- [x] 4.4 Errors. A bad file shouldn't stop play. Leaning (Claude): an error in a configuration or a map, at start or on a reload, is shown on screen with where it is, and the game keeps what it had, or the defaults and the text rooms at start. Decision: as leaning. An error in the LDtk project leaves out all its worlds; one in a configuration leaves the features as they were. The errors are shown at the bottom of the window, at its resolution, until the next read succeeds.

## 5. Code

- [x] 5.1 Where does reading a configuration live? Leaning (Claude): in Core's root, beside `Features`, since it is the record's own reading and tests in `BodyCheck.Core.Tests` can check it. Listing files and the keys are the game's, in `Main`. Decision: as leaning. The converter that reads enums by name only moves to the root too, since both the LDtk reader and `Features` use it.

Closed on 2026-10-20. Outcomes are in [architecture.md](../designs/architecture.md); §1.2 is in its open questions.
