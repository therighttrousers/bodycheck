# Architecture

Relevant decision records:
- [001: Godot's 3D physics engine](../decision-records/001-godot-3d-physics.md)
- [002: `BodyCheck.Core` depends on GodotSharp](../decision-records/002-core-depends-on-godotsharp.md)
- [005: Jolt's joint motors for traction](../decision-records/005-jolt-motors-for-traction.md)
- [006: Models, their base point, and animation](../decision-records/006-models-and-animation.md)
- [008: Telemetry, and a debug overlay drawn at the window's resolution](../decision-records/008-telemetry-and-overlay.md)
- [009: The first level loader](../decision-records/009-level-loader.md)
- [010: Camera framing](../decision-records/010-camera-framing.md)
- [011: Stitching LDtk levels into one world](../decision-records/011-stitched-levels.md)
- [012: Subsystems in `BodyCheck.Core`](../decision-records/012-core-subsystems.md)

## Tools

- **Engine:** Godot 4.7.2, .NET build, Forward+ renderer, desktop only. The engine is installed into the repository by `dev-setup.ps1` and is not on the PATH.
- **Language:** C# on .NET 10.
- **Physics:** Godot's 3D physics engine, Jolt, with gameplay locked to the XY plane. The engine detects collisions, resolves contacts and moves bodies. Our rules push bodies with the engine's forces and impulses. A walker's grip is a motor inside the engine, which our rules set each tick (`005`). Drag is ours. See [movement.md](movement.md#physics-engine).
- **Levels:** authored in 2D in LDtk, in `game/levels/`, and turned into 3D at load. The LDtk JSON is read at runtime, and is to be moved to an asset pipeline only if the loading code proves too slow, too complex or too fragile (`000 §1.2`).
- **Camera:** Phantom Camera 0.11.0.3, an addon, in `game/addons/phantom_camera/` without its examples. Out-of-the-box tools are preferred. Its nodes are GDScript classes; C# reaches them through the wrappers it ships, which can't create them, so the game creates them from their scripts. `.editorconfig` treats `game/addons/` as generated code, so formatting and the analyzers leave it as its authors wrote it, and turns off the compiler warnings it has.
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

Core is split into subsystems, each a subdirectory and a namespace, with an `intent.md` that says what belongs there and what doesn't (`012`). Subsystems don't depend on each other in a cycle, and the root depends on none of them. The rules for keeping it so are in the root `AGENTS.md`.

- **Root:** telemetry and `Constants`, which the subsystems and the game share.
- **`Entities`:** entity classes, their tuning, and the controller interface. Depends on the root.
- **`Controllers`:** the player's, robots' and platforms' controllers, and their tuning. Depends on `Entities` and the root.
- **`Rooms`:** rooms, tile grids, and the libraries of text rooms. Depends on the root.
- **`Levels`:** the LDtk reader. Depends on `Rooms` and the root.
- **`Simulation`:** `RoomNode` and the tiles' colliders. Depends on `Rooms`, `Entities` and `Controllers`.

- An `Entity` is one thing in a room that has a body: the player, a robot, a platform, a block. It is the engine's rigid body itself, so what the engine reports about bodies is about entities. Its body values are a `BodyTuning`, and it is given what to draw.
- An entity's origin is the centre of its body, where its collision box is centred. What it draws has its origin at its base, and the entity lowers it by half the body's height (`006 §2.1`).
- An entity owns its motors, so that they live exactly as long as it does. Each kind of body that has a controller is a subclass, which holds that body's motors: `WalkerEntity` has traction, and `SimpleControlledEntity`, which a platform is, has no motors (`005 §3.6`). A plain `Entity` has neither, and is what a block is (`005 §3.8`).
- An `ICharacterController<TEntity>` decides what an entity does, using forces, impulses and the motors of that kind of body. The player and the NPCs use the same interface; the player's controller is given its source of input. The wrong controller for a body doesn't compile.
- The player and a robot are the same class, `WalkerEntity`, with different tuning, looks and controller.
- **Telemetry.** Game code pushes measurements to `Telemetry`, by `Metric`, an enum. Each metric keeps its last value until it is reported again or the telemetry is reset. Whatever wants the measurements reads them: the debug overlay, and tests. Nothing that reports knows what reads, so telemetry puts no constraint on how game code is divided up (`008 §1`).
- Core's classes have no `res://` path, so they are created in code and can't be attached to a scene in the editor.
- A `Room` is a tile grid and a configuration for each entity: its kind, as an `EntityKind`, where it starts, and later whatever else a level says about it. Each kind has its own type of configuration, which `RoomNode` uses to build the entity. Rooms come from two sources: text, and the levels of an LDtk project, stitched into one room (`009 §2.1`, `011 §2.1`).
- `Constants`, in Core's root, holds constants that more than one subsystem or the game needs: `TilePixels`, the size of a tile's face in pixels (`009 §5.1`), and a level's size in tiles. Other constants move there as we come across them (`012 §2.3`).
- **LDtk levels.** `LdtkReader` reads every level of an LDtk project into one `Room`, the world, named after the project's file (`011 §2.1`). Levels are 20x15 tiles, on a grid of that size, at depth 0, in a project with one world; grid cells with no level are solid, and LDtk's `__neighbours` are ignored (`011 §3`). The world has one player start. Every non-zero value of the `SolidGeometry` layer is a solid tile, and what LDtk writes for auto-layering is ignored. Each entity in the `Characters` layer starts with the middle of its base at the middle of the bottom of its box in LDtk; entity fields are ignored (`009 §1`). The reader is given a function that reads files, so the game reads through Godot's `FileAccess` and tests through `System.IO`.
- `RoomNode` is one room, simulated: tiles and entities, with no camera, lights or input of its own, so tests use it as it is. It builds the entities, gives each its looks and its controller, and steps every one each physics tick, in the order of the room's configurations. It is given a function that makes each kind of entity's visual, so Core doesn't know the game's assets; the game's is `EntityVisuals.For` (`012 §3.1`).
- **Test rooms** are a library of named rooms, described as text. `RoomLibrary` has the ones to play in; `TestRoomLibrary` has the ones that tests and probes measure in. Design docs and tests refer to them by name.

## `game/`

The Godot project, which holds the scenes and the other nodes.

- A model is a wrapper scene in `assets/` around an imported glTF file, with its origin at its base. See [models.md](models.md).
- `Main` adds the camera, the lights and the player's input to a `RoomNode`, and switches rooms. `CameraRig` is the camera: a `Camera3D` with a Phantom Camera host, which puts it where the one phantom camera is, and a target that the phantom camera follows. The rig moves the target each frame, above the player, up or down by where the player looks, and clamped to the room's tile grid, which for the world is its bounding box (`010 §1.3`, `§2.1`, `011 §4.1`). It reads the look from the room's `PlayerInput`, which the player's controller reads too, so that function must have no side effects. Its rooms are the world of `levels/main.ldtk`, then the library of text rooms; the first plays unless the command line names another (`009 §4.3`, `011 §3.2`). The game is drawn in a `WorldViewportContainer`, a viewport of its own at low resolution, and `Main`'s other children are drawn at the window's resolution.
- `DebugOverlay`, on F3, shows the frame rate and times and every telemetry value, at the window's resolution (`008 §2.3`).
- `ShapeWireframes`, on F4, draws every box collision shape in the game as a wireframe, at the window's resolution, for comparing bodies with their models. It reads the shapes and the camera from the engine each frame.
- `look/` holds the low-resolution view, the colour pass and the tools for judging the rendering. See [rendering.md](rendering.md).
- `probes/` holds scripted measurements, run headless with `--probe=` and a name.

## Conventions

- **Reading data is typed and validated** (Neil). Data from files is read into types that say what it must be, and anything else is an error, reported with where it is: a missing or null value, a name that isn't one of a fixed set, a number where a name should be. Checks that types can't express are made right after reading. Nothing is silently defaulted or skipped, except parts of a format the game doesn't read. For JSON this is `System.Text.Json`'s source generator over records whose properties are `required`, with nullable annotations respected and enums read by name only (`009 §3`).

## Tests

- **`tests/BodyCheck.Core.Tests`:** unit tests for the parts of the library that need no engine, in xUnit: the tile grid (`005 §3.5`) and the LDtk reader. The game's levels are copied into its output, so a test checks that the world reads.
- **`tests/BodyCheck.Game.Tests`:** tests that need the engine, run inside Godot through gdUnit4Net by plain `dotnet test`. Godot runs headless at a fixed frame rate, so physics ticks faster than real time. The project is a Godot project of its own, whose settings are copied from the game's on every build, without autoloads and editor plugins, since it doesn't have the game's addons. It can't load the game's scenes by `res://` path. It can load the game's assets: its build links `game/assets` into it and runs Godot's import when an asset changes (`006 §5.1`).

## Open questions

- Next, in a PR of its own: `World` becomes a container of `Room`s, a step toward caching, respawn rules and culling, none of which it builds yet. Its name is free: the constants it held are in `Constants` (`011 §2.2`, `012 §2.3`).
- Phantom Camera's lookahead and newer framed-follow features are described for its 2D camera only, and are unverified in 3D. Its limits are 2D only; `CameraRig` clamps instead (`010 §2.1`).
- Exports will need `*.ldtk` and `*.ldtkl` in their filter of non-resource files, since Godot doesn't import them (`009 §4.1`). There are no exports yet.
- Linking `game/levels` into the in-engine test project, for integration tests on real levels, is put off (`009 §4.2`).
- Can the Godot editor attach a script to a scene only when the script is in the Godot project itself? Believed so. Core's node classes are created in code, which doesn't need it (`005 §3.2`).
- gdUnit4Net is used outside its stated support (.NET 8 and 9, Godot 4.4 to 4.5), and `gdUnit4.api` is a prerelease. To be revisited when it releases.
- Do two test projects still earn their keep, now that Core's rules need the engine? `BodyCheck.Core.Tests` holds only the tile-grid and LDtk reader tests. It can be given the Game tests' setup and keep its xUnit tests running beside gdUnit4Net ones (`005 §3.5`).
