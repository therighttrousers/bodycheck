# 002: `BodyCheck.Core` depends on GodotSharp

## Context

Inciting event: `001 §6.1` switched the game to Godot's physics engine. Character controllers now act on engine bodies, so they can no longer live in a library with no Godot dependency (`000 §1.3`). Without them, and without the simulation, `BodyCheck.Core` would shrink to a small utility library.

Goal: decide what `BodyCheck.Core` is for after the migration, and how it reaches Godot's types.

Constraint: GodotSharp's types are of two kinds. Value types such as `Vector3` work in any .NET process. Anything derived from a Godot object, which includes every node, works only inside a running engine.

## 1. What Core holds

- [x] 1.1 Where do the character controllers live? Decision: B.
  - Option A: move them to `game/`. Core keeps only what needs no engine: rooms, the tile grid, tuning records, the LDtk loader.
  - Option B (Neil): Core depends on GodotSharp and keeps the controllers.
  - Rationale: Core stays the home of the game's rules, and uses one set of vector types, with no conversion at the boundary.
- [x] 1.2 How do controllers in Core reach a body, when nodes don't work outside the engine? Decision: through a body type in Core that the game's body node feeds. First decided as an interface that the node implements; built as a class that the node owns, for the reasons at the end of this item.
  - The interface offers what the controllers use now: velocity, contacts, support, traction, and adding a force or an impulse.
  - It has no way to apply a force straight to the engine, so a controller can't bypass traction. That mistake was made twice in the spike (`001 §2.1`).
  - The traction stage is arithmetic over bodies' velocities and needs no engine, so it moves to Core too, and gets plain unit tests with fake bodies.
  - Tests of what a controller achieves (jump height, riding) need real contacts, and stay in `BodyCheck.Game.Tests`.
  - As built (Claude, 2026-10-12): `Body` is a plain class in Core. The game's `BodyNode` owns one, copies the engine's position, velocity and contacts into it each tick, and gives the engine the impulse that `World.Step` works out. Reasons for a class over an interface: the step needs members that controllers shouldn't see, which an interface would have to expose or split in two; and a unit test can use a real `Body` instead of a fake one.
- [x] 1.3 Where do node classes live? Decision: in `game/`. To be checked: Claude's belief that the editor can attach a script to a scene only when the script is in the Godot project itself.

## 2. How Core gets GodotSharp

- [x] 2.1 Decision: B, as built on 2026-10-12.
  - Option A: the `GodotSharp` NuGet package. Its version has to be kept in step with the engine by hand.
  - Option B (Neil): an assembly reference to `Godot\GodotSharp\Api\Debug\GodotSharp.dll`, in the engine that `dev-setup.ps1` installs. The version always matches the engine.
  - `BodyCheck.Core` and `BodyCheck.Core.Tests` both reference it. Core doesn't copy it to its output, because the engine supplies its own copy at run time. The tests do, because they run outside the engine.
  - Checked: the solution builds with no warnings; a plain xUnit test can do vector arithmetic with Godot's types; the engine tests pass; the game runs. The formatting check passes on a clone with no engine installed, which is how CI's formatting job runs.
  - Cost: Core no longer builds until `dev-setup.ps1` has been run. The engine tests already needed that.

## 3. Order of work

- [x] 3.1 Core's old simulation uses the names `Body`, `World` and `Traction`, which the new code wants. Decision (Neil): delete the old simulation first. Nobody else is working on the project, so nothing is interrupted. Options: delete the old simulation first, which leaves the main scene broken until the new one replaces it; or build the new code under other names and rename at the end.
- [x] 3.2 Vectors in Core are Godot's `Vector2`, not `Vector3`: the rules work in the XY plane, and a controller then can't push a body along Z. `BodyNode` converts. (Claude, as built; the spike's controllers used `Vector3`.)

Closed on 2026-10-12. Outcomes are in [high-level-design.md](../designs/high-level-design.md).
