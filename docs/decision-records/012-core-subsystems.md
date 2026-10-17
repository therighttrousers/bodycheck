# 012: Subsystems in `BodyCheck.Core`

## Context

Inciting event (2026-10-17): issue #38. `BodyCheck.Core` is a flat directory of 16 files in one namespace. It will grow, and we want it to stay easy to find our way around, with some constraints on where new code goes.

Goals, from the issue:
- Split Core into subsystems, each in a subdirectory of its own, something like a separate library. Cross-subsystem references are to be kept light.
- No circular dependencies between subsystems. A subsystem may depend on Core's root, but the root depends on no subsystem. The root holds what is shared, such as utilities and constants.
- Each subsystem, and the root, has an `intent.md`: what it holds and is for, how it relates to other subsystems, and what it is not for.
- Guidance on keeping the structure, and on writing `intent.md` files, goes in the root `AGENTS.md`.

Anti-goals: actually separate libraries, or dependency injection between subsystems. This is a first split, to be iterated on.

What was there: the controllers and the entity classes refer to each other only through `ICharacterController`, which the entities use and the controllers implement. `Room` and `TileGrid` don't refer to entities at all. `LdtkReader` builds a `Room`, and refers to `World.TilePixels`. Only `PlayerController` reports to `Telemetry`. In `game/`, `RoomNode` is what joins rooms, entities and controllers.

## 1. The split

- [x] 1.1 Which subsystems? Decision (Claude, agreed by Neil):
  - Core's root: `Telemetry`, and `Constants`.
  - `Entities`: entity classes, `Contacts`, `BodyTuning`, `ICharacterController`. Depends on the root.
  - `Controllers`: the player's, robots' and platforms' controllers, their tuning, `PlayerInput`. Depends on `Entities` and the root.
  - `Rooms`: `Room`, `EntityKind`, `TileGrid` and the libraries of text rooms. Depends on the root.
  - `Levels`: reading LDtk projects into rooms. Depends on `Rooms` and the root.
- [x] 1.2 Controllers in `Entities`, or a subsystem of their own? Decision (Neil): their own. Neil changed entities and controllers together while building the infrastructure; Claude argued that entities are a small set of kinds of body that should settle, while controllers are behaviours, which will keep being added without touching `Entities`. Merging them later is cheap.
- [x] 1.3 Empty subsystems for what is planned, such as `Saves`? Decision (Neil): no, by YAGNI. The `intent.md` files say what each subsystem is not for, which stops misplaced code without placeholders.
- [x] 1.4 How is the structure kept? Decision (Neil): guidance in `AGENTS.md` and review. No test checks the dependencies.

## 2. Namespaces and names

- [x] 2.1 Each subdirectory is a namespace, e.g. `BodyCheck.Core.Entities` (Neil). Every reference between subsystems is then a `using` directive, which is easy to audit; and `.editorconfig` already asks for namespaces that match folders.
- [x] 2.2 A namespace can't usefully share a name with a type in it, so `Rooms`, the library of text rooms, becomes `RoomLibrary`, and `TestRooms` becomes `TestRoomLibrary` to match.
- [x] 2.3 `World.TilePixels` moves to `Constants` in the root, with the level's size in tiles from `LdtkReader`, which `CameraRig` also needs (Neil). `World` is left empty and is deleted; the name is free for the container of rooms (`011 §2.2`). Other constants move into `Constants` when we come across them, not in a search now.

## 3. `RoomNode`

- [~] 3.1 Does `RoomNode` move into Core, as a `Simulation` subsystem that depends on `Rooms`, `Entities` and `Controllers`? It would bring `TileColliders` with it. The game would give it the entities' looks, so that Core doesn't know the game's asset paths. Neil wants to see it first: it is tried as a separate commit, after the rest.
