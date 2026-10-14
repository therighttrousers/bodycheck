# Architecture

Relevant decision records:
- [001: Godot's 3D physics engine](../decision-records/001-godot-3d-physics.md)
- [002: `BodyCheck.Core` depends on GodotSharp](../decision-records/002-core-depends-on-godotsharp.md)
- [005: Jolt's joint motors for traction](../decision-records/005-jolt-motors-for-traction.md)

## Tools

- **Engine:** Godot 4.7.2, .NET build, Forward+ renderer, desktop only. The engine is installed into the repository by `dev-setup.ps1` and is not on the PATH.
- **Language:** C# on .NET 10.
- **Physics:** Godot's 3D physics engine, Jolt, with gameplay locked to the XY plane. The engine detects collisions, resolves contacts and moves bodies. Our rules push bodies with the engine's forces and impulses. A walker's grip is a motor inside the engine, which our rules set each tick (`005`). Drag is ours. See [movement.md](movement.md#physics-engine).
- **Levels:** authored in 2D in LDtk, in `game/levels/`, and turned into 3D at load. The LDtk JSON will be read at runtime at first, and moved to an asset pipeline only if the loading code proves too slow, too complex or too fragile. No loader exists yet.
- **Camera:** Phantom Camera, to be added. Out-of-the-box tools are preferred.
- **Modelling:** Blender.

## Layout

- `game/`: the Godot project (scenes, node scripts, levels, assets)
- `src/BodyCheck.Core/`: game rules as a C# library
- `tests/BodyCheck.Core.Tests/`: xUnit tests for the library
- `tests/BodyCheck.Game.Tests/`: tests that run inside Godot, through gdUnit4Net
- `docs/`: design docs and decision records
- `Godot/`: the engine, installed by `dev-setup.ps1` (not in Git)

## `src/BodyCheck.Core`

The game's rules, as a class library: entities, character controllers, tuning, rooms and tiles. It defines node classes, so its rules need a running engine (`005 §3`, which supersedes that part of `002`). It references the GodotSharp assembly of the installed engine, so `dev-setup.ps1` has to be run before it builds (`002`), and Godot's source generators, without which the engine never calls a node class's overrides.

- An `Entity` is one thing in a room that has a body: the player, a robot, a platform, a block. It is the engine's rigid body itself, so what the engine reports about bodies is about entities. Its body values are a `BodyTuning`, and it is given what to draw.
- An entity owns its motors, so that they live exactly as long as it does. Each kind of body that has a controller is a subclass, which holds that body's motors: `WalkerEntity` has traction, and `SimpleControlledEntity`, which a platform is, has no motors (`005 §3.6`). A plain `Entity` has neither, and is what a block is (`005 §3.8`).
- An `ICharacterController<TEntity>` decides what an entity does, using forces, impulses and the motors of that kind of body. The player and the NPCs use the same interface; the player's controller is given its source of input. The wrong controller for a body doesn't compile.
- The player and a robot are the same class, `WalkerEntity`, with different tuning, looks and controller.
- Core's classes have no `res://` path, so they are created in code and can't be attached to a scene in the editor.
- **Test rooms** are a library of named rooms, described as text. `Rooms.cs` has the ones to play in; `TestRooms.cs` has the ones that tests and probes measure in. Design docs and tests refer to them by name.

## `game/`

The Godot project, which holds the scenes and the other nodes.

- `RoomNode` is one room, simulated: tiles and entities, with no camera, lights or input of its own, so tests use it as it is. It builds the entities, gives each its looks and its controller, and steps every one each physics tick.
- `Main` adds the camera, the lights and the player's input to a `RoomNode`, and switches rooms.
- `look/` holds the colour pass and the tools for judging the rendering. See [rendering.md](rendering.md).
- `probes/` holds scripted measurements, run headless with `--probe=` and a name.

## Tests

- **`tests/BodyCheck.Core.Tests`:** unit tests for the parts of the library that need no engine, in xUnit. That is now only the tile grid (`005 §3.5`).
- **`tests/BodyCheck.Game.Tests`:** tests that need the engine, run inside Godot through gdUnit4Net by plain `dotnet test`. Godot runs headless at a fixed frame rate, so physics ticks faster than real time. The project is a Godot project of its own, whose settings are copied from the game's on every build. It can't load the game's scenes or assets by `res://` path.

## Open questions

- Phantom Camera is unverified for this project: use from C#, and camera limits in 3D. Its limit, lookahead and newer framed-follow features are described for its 2D camera only.
- Can the Godot editor attach a script to a scene only when the script is in the Godot project itself? Believed so. Core's node classes are created in code, which doesn't need it (`005 §3.2`).
- gdUnit4Net is used outside its stated support (.NET 8 and 9, Godot 4.4 to 4.5), and `gdUnit4.api` is a prerelease. To be revisited when it releases.
- Do two test projects still earn their keep, now that Core's rules need the engine? `BodyCheck.Core.Tests` holds only the tile-grid tests. It can be given the Game tests' setup and keep its xUnit tests running beside gdUnit4Net ones (`005 §3.5`).
