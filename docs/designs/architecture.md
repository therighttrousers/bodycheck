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
- [013: The world as a container of rooms](../decision-records/013-world-of-rooms.md)
- [014: Less disruptive looking](../decision-records/014-less-disruptive-looking.md)
- [015: The phase ability](../decision-records/015-phase.md)
- [016: Features](../decision-records/016-features.md)

## Tools

- **Engine:** Godot 4.7.2, .NET build, Forward+ renderer, desktop only. The engine is installed into the repository by `dev-setup.ps1` and is not on the PATH.
- **Language:** C# on .NET 10.
- **Physics:** Godot's 3D physics engine, Jolt, with gameplay locked to the XY plane. The engine detects collisions, resolves contacts and moves bodies. Our rules push bodies with the engine's forces and impulses. A walker's grip is a motor inside the engine, which our rules set each tick (`005`). Drag is ours. Positions are in single precision, as in Godot's standard builds; both play and drawing hold up to 1e5 tiles from the origin, far beyond any world's size (`013 §6.1`). See [movement.md](movement.md#physics-engine).
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

The game's rules, as a class library: entities, character controllers, tuning, the world, its rooms and their tiles. It defines node classes, so its rules need a running engine (`005 §3`, which supersedes that part of `002`). It references the GodotSharp assembly of the installed engine, so `dev-setup.ps1` has to be run before it builds (`002`), and Godot's source generators, without which the engine never calls a node class's overrides.

Core is split into subsystems, each a subdirectory and a namespace, with an `intent.md` that says what belongs there and what doesn't (`012`). Subsystems don't depend on each other in a cycle, and the root depends on none of them. The rules for keeping it so are in the root `AGENTS.md`.

- **Root:** telemetry, `Constants`, `Features`, `PhysicsLayer`, and extension members on Godot's vector types, which the subsystems and the game share.
- **`Entities`:** entity classes, their tuning, and the controller interface. Depends on the root.
- **`Controllers`:** the player's, robots' and platforms' controllers. Depends on `Entities` and the root.
- **`WorldData`:** the world, its rooms, tile grids, and the libraries of text rooms, as data (`013 §4.2`). Depends on the root.
- **`Levels`:** the LDtk reader. Depends on `WorldData` and the root.
- **`Simulation`:** `WorldNode` and the tiles' colliders, which turn the world's data into what the engine runs. Depends on `WorldData`, `Entities` and `Controllers`.

- An `Entity` is one thing in a room that has a body: the player, a robot, a platform, a block. It is the engine's rigid body itself, so what the engine reports about bodies is about entities. Its body values are a `BodyTuning`, and it is given what to draw.
- An entity's origin is the centre of its body, where its collision box is centred. What it draws has its origin at its base, and the entity lowers it by half the body's height (`006 §2.1`).
- An entity owns its motors, so that they live exactly as long as it does. Each kind of body that has a controller is a subclass, which holds that body's motors: `WalkerEntity` has traction, and `SimpleControlledEntity`, which a platform is, has no motors (`005 §3.6`). A plain `Entity` has neither, and is what a block is (`005 §3.8`).
- Every entity can move through other entities, but not through tiles, before the engine's next step, and can find the colliders that overlap a shape, as of the last step. The player's controller uses them to phase: a `PhaseAction` holds a phase being aimed, with its rules for the grab, the placement and the impulses (`015 §4.1`, `§5.20`, `§5.21`).
- An `ICharacterController<TEntity>` decides what an entity does, using forces, impulses and the motors of that kind of body. The player and the NPCs use the same interface; the player's controller is given its source of input. The wrong controller for a body doesn't compile.
- **A controller can ask for the game to pause**, to decide something with everything else standing still: the player's does while a phase is aimed. While the game is paused for that, controllers run on a second path beside the normal one: the game calls `WorldNode.StepPaused`, which calls each `Entity.StepPaused`, which calls the controller's `ApplyPaused`. A controller asks through `RequestsPause`, which its entity passes on, and `WorldNode.PauseRequested` says whether any does. Both are default interface members that do nothing, so only controllers that pause have them. Nothing moves on this path: the engine doesn't step (`015 §5.10`).
- The player and a robot are the same class, `WalkerEntity`, with different tuning, looks and controller.
- **Telemetry.** Game code pushes measurements to `Telemetry`, by `Metric`, an enum. Each metric keeps its last value until it is reported again or the telemetry is reset. Whatever wants the measurements reads them: the debug overlay, and tests. Nothing that reports knows what reads, so telemetry puts no constraint on how game code is divided up (`008 §1`).
- Core's classes have no `res://` path, so they are created in code and can't be attached to a scene in the editor.
- A `World` is a name and its `Room`s, which don't overlap, with exactly one player start among them (`013 §2.3`, `§3.2`). It is data only: nothing at run time knows which room a tile or an entity is in (`013 §1.1`). It builds a fresh `TileGrid` over its rooms' bounding box, which starts at its origin, with everything outside a room solid (`013 §3.1`), and lists every room's entity configurations in world coordinates, room by room (`013 §4.4`). Worlds come from two sources: the levels of an LDtk project, one room per level, and a text room, as a world of one room.
- A `Room` is a name, an origin in the world, in tiles, a tile grid, and a configuration for each entity that starts in it, the player's included. Its tiles and starts are in its own coordinates (`013 §2.1`, `§2.2`). An entity's configuration is its kind, as an `EntityKind`, where it starts, and later whatever else a level says about it. Each kind has its own type of configuration, which `WorldNode` uses to build the entity.
- `Constants`, in Core's root, holds constants that more than one subsystem or the game needs: `TilePixels`, the size of a tile's face in pixels (`009 §5.1`), and a level's size in tiles. Other constants move there as we come across them (`012 §2.3`).
- **Features.** `Features`, in Core's root, is a record of the values that shape gameplay: the tuning of the player's running, jumping and phasing, which includes the choice of `PhaseThrow`, and of the robots' and platforms' controllers. Every name is prefixed with what it tunes: `Player`, `Robot` or `Platform` (`016 §3.6`). `Features.Current` is the only source of each: what uses a feature reads it there, when it uses it. `Features.Set` replaces it and raises `Features.Changed`, and `Features.Reset` puts back the defaults, which are the properties' initializers; tests that change features reset them. A feature is a simple value that nothing derives from or caches, except an action in progress, such as a jump or a phase being aimed, which goes on with the values it started with, so a change takes effect at the next one: changes are eventually consistent, with that bound (`016 §3.1`). Graphics settings, the camera's values and debug toggles are not features (`016 §3.2`, `§3.4`).
- `PhysicsLayer`, in Core's root, names the engine's physics layers, as flags: static tiles, liquids, triggers, props such as blocks, and characters, which include platforms. The game's project settings name the same layers, and a test checks that the two agree. An entity's layer is part of its `BodyTuning`, and its mask is every layer that has a collider: tiles and both kinds of entity. Queries should try to use layers instead of filtering what they find.
- **LDtk levels.** `LdtkReader` reads an LDtk project into a `World`, named after the project's file, with a `Room` for each level, named after the level's identifier (`013 §4.5`). Tiles still merge across levels' edges, since the colliders are built from the world's grid (`011 §2.1`). Levels are 20x15 tiles, on a grid of that size, at depth 0, in a project with one world; grid cells with no level are solid, and LDtk's `__neighbours` are ignored (`011 §3`). The reader moves the levels so that the leftmost and lowest are at the world's origin. The world has one player start. Every non-zero value of the `SolidGeometry` layer is a solid tile, and what LDtk writes for auto-layering is ignored. Each entity in the `Characters` layer starts with the middle of its base at the middle of the bottom of its box in LDtk; entity fields are ignored (`009 §1`). The reader is given a function that reads files, so the game reads through Godot's `FileAccess` and tests through `System.IO`.
- `WorldNode` is one world, simulated: tiles and entities, with no camera, lights or input of its own, so tests use it as it is. It builds the colliders from the world's grid, builds the entities, gives each its looks and its controller, and steps every one each physics tick, in the order of the world's configurations. It skips an entity that can't process, so that a controller stops with whatever pauses or disables its body: the tree's pause, or the `ProcessMode` of the entity or an ancestor (`013 §1.2`). It is given a function that makes each kind of entity's visual, so Core doesn't know the game's assets; the game's is `EntityVisuals.For` (`012 §3.1`).
- **Test rooms** are a library of named rooms, described as text, each played as a world of one room (`013 §2.4`). `RoomLibrary` has the ones to play in; `TestRoomLibrary` has the ones that tests and probes measure in. Design docs and tests refer to them by name.

## `game/`

The Godot project, which holds the scenes and the other nodes.

- A model is a wrapper scene in `assets/` around an imported glTF file, with its origin at its base. See [models.md](models.md).
- `Main` adds the camera, the lights and the player's input to a `WorldNode`, and switches worlds. `CameraRig` is the camera: a `Camera3D` with a Phantom Camera host, which puts it where the phantom camera of highest priority is, and two phantom cameras, the player's and the map's. The player's follows a target, which the rig moves each frame, above the player, up or down by where the player looks, and clamped to the world's tile grid, its rooms' bounding box (`010 §1.3`, `§2.1`, `011 §4.1`). The rig reads the look from the world's `PlayerInput`, which the player's controller reads too, so that function must have no side effects. It looks only while `PlayerInput.CanLook`, after a hold, easing a look amount with a `Tween` that it kills when the look stops (`014`). `Main`'s worlds are the world of `levels/main.ldtk`, then a world for each text room in the library; the first plays unless the command line names another, and comma and period switch between them (`009 §4.3`, `011 §3.2`, `013 §4.6`). The game is drawn in a `WorldViewportContainer`, a viewport of its own at low resolution, and `Main`'s other children are drawn at the window's resolution.
- **The world as the map** (Neil). `GameModeManager` holds the game's mode: playing, the menu or the map, on Escape and Tab or the gamepad's Start and Back, and phasing, while the world asks for a pause, during which it steps the world's controllers on their paused path. Before phasing, it slows the game down to the pause: the engine's time scale falls from 1 to 0.05 over 0.5 real seconds, which keeps 60 physics ticks a second and shortens each. It stops short of 0 to avoid very small steps (`015 §5.12`). Probes run with nothing pausing for them, so their player aims while the world runs. Every mode but playing pauses the tree, which stops the entities' controllers too (`013 §1.2`). There is no menu yet, so the menu is only a pause. In the map, the map's phantom camera takes priority: it is over the middle of the world, back to where the whole world fits in the view, and the host tweens the camera there and back. The world itself is the map; nothing else is drawn for it. The rig processes while the tree is paused, so that the camera can move, and the player doesn't look up or down then.
- **Paused**, the debug tools still work: the `DebugOverlay`, the `ShapeWireframes` and the rendering switches process while the tree is paused.
- `PhaseAimMarker` is a placeholder, at the window's resolution, for showing a phase being aimed: a box around the target, and arrows from the center of mass of the player and the target for the velocity each would leave with (`015 §5.7`).
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

- Graphics settings' lifecycle and persistence: where they live, how they are saved per machine, and whether they get a record of their own like `Features` (`016 §3.3`).
- Who owns what crosses a room's edge, such as a block pushed through an opening or a robot riding a platform across one, waits for the design of respawn, caching and pausing. A node per room would let Godot's `ProcessMode` pause a room, but entities would then belong to a room's node, which needs that rule, and colliders built per room would stop tiles merging at rooms' edges (`013 §1.1`).
- The rest of the world as the map (Neil): the map pauses the tree and zooms out, but doesn't yet hide what moves or is expensive to draw, or show only the rooms the player has explored. That would give rooms their first job at run time, drawing and revealing the explored ones, perhaps with a cheaper look each, and it means caching could unload a room's entities and colliders but not its look (`013`, Context). The zoom's tween is linear in distance, which does most of the visible zooming early; an exponential transition may feel more even at the sizes of a whole game, 16x16 to 32x32 rooms.
- Phantom Camera's lookahead and newer framed-follow features are described for its 2D camera only, and are unverified in 3D. Its limits are 2D only; `CameraRig` clamps instead (`010 §2.1`).
- Exports will need `*.ldtk` and `*.ldtkl` in their filter of non-resource files, since Godot doesn't import them (`009 §4.1`). There are no exports yet.
- Linking `game/levels` into the in-engine test project, for integration tests on real levels, is put off (`009 §4.2`).
- Can the Godot editor attach a script to a scene only when the script is in the Godot project itself? Believed so. Core's node classes are created in code, which doesn't need it (`005 §3.2`).
- gdUnit4Net is used outside its stated support (.NET 8 and 9, Godot 4.4 to 4.5), and `gdUnit4.api` is a prerelease. To be revisited when it releases.
- Do two test projects still earn their keep, now that Core's rules need the engine? `BodyCheck.Core.Tests` holds only the tile-grid and LDtk reader tests. It can be given the Game tests' setup and keep its xUnit tests running beside gdUnit4Net ones (`005 §3.5`).
