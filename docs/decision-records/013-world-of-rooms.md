# 013: The world as a container of rooms

## Context

Inciting event (2026-10-17): work on issue #39, "Infra: spatial subdivision (World contains Rooms)", began. It is the next step that `011 §2.2` set: `World` becomes a container of `Room`s, moving toward the eventual design in small steps.

What is there: `LdtkReader` stitches every level of an LDtk project into one `Room`, with one `TileGrid` and every entity's configuration in world coordinates (`011 §2.1`). `RoomNode` simulates a `Room`: it builds the colliders from the whole grid, merging tiles across level edges, and builds and steps every entity. `CameraRig` clamps to the room's grid, which for the world is its bounding box. Text rooms are `Room`s too, and the game plays the world and the text rooms through the same `RoomNode`. The name `World` is free (`012 §2.3`).

Why `011 §2.1` put this off: caching, respawn and culling may each want a different subdivision, and the hard part, ownership of what crosses a boundary (a block across two levels, a robot on a platform crossing an edge, the player pushing a block through an opening), depends on how caching and pausing work. Neither is designed yet.

Goals:
- A `World` made of `Room`s, one per LDtk level, so that later work has a place to hang per-room rules.
- What a room is responsible for, and what the world is, decided where it can be now.

Anti-goals (as `011 §1.1`): caching, pausing and resuming, respawn, culling. Any rule for ownership that depends on them. Any change in how the game plays: bodies still cross freely, and tiles still merge across level edges.

A future feature to keep in view (Neil): the world as the map. Opening the map hides, pauses or disables everything that moves or is expensive to draw, and zooms far out to show the whole world, instead of an image overlay. Pausing for it is global (the tree's pause), not per room. Drawing for it is per room: a map shows only the rooms the player has explored, and at that zoom a tile is under a pixel at 320x240, so a room may want a cheaper look, such as one merged mesh. So the whole world must stay drawable, and caching could unload a room's entities and colliders but not its look.

Overarching question: how much of the subdivision does this step make real at run time, as opposed to in the data?

Closed on 2026-10-17. Outcomes are in [architecture.md](../designs/architecture.md), and so are the unresolved items: who owns what crosses a room's edge, rooms at run time for the map, and the map itself.

## 1. Scope

- [x] 1.1 Data only, or run time too? Decision (Neil): (a), data only. Options:
  - (a) Data only. `World` holds `Room`s; what runs is one simulation of the whole world, as now. Rooms exist at run time only as rectangles the world can be asked about (which room is this point in?).
  - (b) A node per room at run time, owning its tiles' colliders and the entities that start in it.
  - Leaning (Claude): (a). (b) means entities belong to a room's node, and moving one between nodes when it crosses is the ownership rule `011 §2.1` said not to guess at. Godot's physics doesn't care which node a body is under, so (b) buys nothing until caching or pausing needs it.
  - What (b) would offer: pausing a room by setting its node's `ProcessMode` to `Disabled`, with each body's `DisableMode` saying what that means for physics. With §1.2, the controllers would stop with it.
  - What (b) would force now:
    - Which room owns an entity. Either the room it started in, for good, with no reparenting, so that a block pushed into the next room pauses with its home room, a rule about respawn rather than space. Or the room it is in now, reparented when it crosses, which needs a meaning for "in" for a body across an edge, and for a robot riding a platform across one.
    - Whether reparenting a moving body is safe. It leaves and re-enters the tree, so the engine takes it out of the physics space and puts it back, which may drop its contacts for a tick: a walker's traction, a rider on a platform. To be measured with the riding and seam probes before trusting it.
    - Who owns the colliders. If each room builds its own, tiles stop merging at level edges, and a floor across an edge becomes two boxes meeting at a seam, the kind of internal edge bodies catch on. Either colliders stay world-wide, so that a room doesn't own its tiles, or the seam probe shows the split doesn't catch.
    - Step order: room by room, then configuration order within each room.
  - The map in the context gives rooms a first job at run time that isn't physics ownership: drawing and revealing. That job could justify a node per room later, without settling who owns a block that crosses. Physics ownership waits for the design of respawn and caching.
- [x] 1.2 Controllers in a paused or disabled entity. The simulation steps every entity itself, so pausing or disabling an entity's parent stops its body but not its controller (Neil). Options: each room's node steps its own entities, which ties this to §1.1(b); or the stepper skips any entity whose `CanProcess()` is false. Decision (Claude, agreed by Neil): `CanProcess()`, in this PR. It is Godot's own answer to whether a node is paused or disabled, through the tree's pause or any ancestor's `ProcessMode`, so controllers stop with whatever stops the body, the map's pause included, with no state of our own.

## 2. What a room is

- [x] 2.1 A room's extent. Decision (Neil): a rectangle of tiles at an origin in the world, in tiles, with its own `TileGrid`. Nothing requires 20x15 but the LDtk reader, which already checks it; text rooms are whatever size their text is.
- [x] 2.2 Coordinates: the room's own or the world's? Decision (Neil): the room's own, for its tiles and its entity starts, with the world adding the origin. LDtk already gives a level's contents that way, so the reader loses its arithmetic, and a cache will want a room that loads without knowing where it goes.
- [x] 2.3 The player start. Options: `World` holds it and `Room` holds only the other entities' configurations, so that the reader takes it out of the level it was authored in; or a room keeps every configuration it was authored with, the player's included. Decision (Neil): a room keeps every configuration. The rule that there is exactly one player moves from `Room` to `World`, across all its rooms, which is the responsibility `011 §2.1` named. That is uniform: no configuration is special, and none moves. When save points come, where the player starts will come from a save rather than a room, which is designed then.
- [x] 2.4 Text rooms. A text room is a room, so `Room.FromText` stays. Should the libraries hold rooms, wrapped in a world of one room where one is played, or one-room worlds, renamed `WorldLibrary` and `TestWorldLibrary`? Decision (Neil): rooms. Their names, and the docs' "test rooms", stay, and wrapping is a line at each place that plays one: `Main`, the game tests' runner and the probes.

## 3. What the world is

- [x] 3.1 Tiles. The world's tiles are the union of its rooms', and everywhere outside a room is solid (as `011 §3.3`). Decision (Claude, agreed by Neil): `World.CreateTiles()` builds a fresh world-wide `TileGrid` over the rooms' bounding box, as `Room.CreateTiles()` builds a fresh copy now, so that changes to tiles at run time don't reach the data. The simulation builds its colliders from it and the camera clamps to it, as now, so tiles still merge across level edges, and seams don't change. No `World.IsSolid`: nothing would use it.
  - The grid starts at the world's origin, so rooms' origins are at least 0; the reader moves the levels so that the leftmost and lowest are at 0, as now.
- [x] 3.2 Overlap. Decision (Claude, agreed by Neil): the world checks that rooms don't overlap, so every tile has at most one room. For LDtk the level grid already rules it out (`011 §3.4`); the check is for the world's own invariant.
- [x] 3.3 Which room a point is in: `World.RoomAt(position)`, or null outside every room. Decision (Claude, agreed by Neil): leave it out by YAGNI (as `012 §1.3`), and add it with its first user, such as the map. The rooms are still visible after load as the world's list of rooms, with their names and origins.
- [x] 3.4 How `RoomAt` finds a room: moot without §3.3. When it comes, a scan of the rooms' rectangles works for rooms of any size, and there are few rooms.
- [x] 3.5 A world's name. The window's title and the command line name what plays. Decision (Claude, agreed by Neil): `World` has a name; the LDtk world takes the project's, and a world of one text room takes the room's.

## 4. Subsystems and names

- [x] 4.1 Where `World` goes. Decision (Claude, agreed by Neil): with `Room`, since it is data, like `Room`, and the reader that produces it already depends on that subsystem.
- [x] 4.2 The subsystem's name. With `World` in it, `Rooms` holds the world's data: the world, its rooms, their tiles, their entities' configurations, and the libraries of text rooms. Decision (Neil, agreed by Claude): it becomes `WorldData`, and building the nodes is turning that data into a form the engine runs. No type is named `WorldData`, so `012 §2.2` is kept. Its `intent.md` changes from rooms as data to the world and its rooms as data.
- [x] 4.3 `RoomNode` becomes `WorldNode`, simulating a `World`, and `Simulation/intent.md` changes to match. `CameraRig.Follow` takes a `WorldNode`. Decision (Claude, agreed by Neil): yes; what it simulates is a world, and a test's world of one room is still a world.
- [x] 4.4 How the simulation gets the entities' starts in world coordinates. Options: `WorldNode` adds each room's origin as it builds; or `World.EntityConfigs`, every room's configurations in world coordinates, in the order of the rooms and then of each room's configurations, made with `config with { Start = room.Origin + config.Start }`. Decision (Claude, agreed by Neil): `World.EntityConfigs`. `WorldNode` then builds and steps exactly as `RoomNode` does, `Entities[i]` is still built from `World.EntityConfigs[i]`, and the platform's controller, which holds the height it started at, gets it in world coordinates without a change.
- [x] 4.5 `LdtkReader.ReadWorld` returns a `World`, one `Room` per level, named after the level's identifier, and the world named after the project's file. Decision (Claude, agreed by Neil): yes.
- [x] 4.6 Names in the game. `Main` plays a list of worlds: the LDtk world, then a world of one room for each room in `RoomLibrary`, and the command line names a world. Decision (Claude, agreed by Neil): `Main`'s fields and methods, and the probes' loader, say "world", for accuracy; the input actions `next_room` and `previous_room`, which are defined in code, become `next_world` and `previous_world`. The game tests' `RoomRun` keeps its name, since every test plays one text room.

## 5. Tests

Decision (Claude, agreed by Neil) for every item: kept light, to the mechanisms that are new (Neil prefers few tests).

- [x] 5.1 Reader: the test that stitches two levels becomes one that two levels make two rooms, with their origins and their entity starts in their own coordinates. The test that the game's world reads stays.
- [x] 5.2 `World`, in `BodyCheck.Core.Tests`: overlapping rooms are refused; there is exactly one player across the rooms, so a world whose only player is in its second room reads, and one with none or two is refused; `CreateTiles` and `EntityConfigs` over two rooms with a gap between them put the tiles and the starts at the rooms' origins, with the gap solid.
- [x] 5.3 `CanProcess()` (§1.2): one game test. The player is disabled with its `DisableMode` set to keep it active, so that its body stays in the engine, and given Right; it doesn't drive, and only falls.
- [x] 5.4 Nothing plays differently: the game tests and the probes change only by name. During the work, not as a committed test, every probe's output is compared from before and after; they are deterministic, so they must match exactly.

## 6. Precision far from the origin

- [x] 6.1 Does physics get unreliable far from the origin (Neil)? Decision (Neil): not at any size the world will have; nothing to do. Measured (Neil), by offsetting the room's node in X and Y:
  - 1e9: a black screen. 1e8: mostly black, with some geometry, and moving changes nothing. 1e7: major drawing errors, and the player is stuck in the floor. 1e6: drawing mostly right, but the screen flashes black, with random error in the dither and projected vertices; physics mostly works, with obvious errors.
  - 1e5: looks and plays well, except for flashing black lines between merged tile meshes, which get less noticeable as the offset approaches zero. They are believed to be sparkles at the T-junctions that the wireframes show, which is already an issue of low priority; tile meshes that are many meshes would likely close it without a change to code.
  - So drawing and physics both hold to 1e5, 50 times the 2,000 tiles of a large world.
  - Estimate (Claude), before measuring: Godot's standard builds, the .NET one included, keep positions in single precision, and Jolt in Godot is built to match; double precision needs a custom build of the engine. A float's spacing at distance *d* is about *d* × 1.2×10⁻⁷, so 0.00024 tiles at 2,000 tiles out, the width of a large metroidvania, against Jolt's contact tolerances of about 0.02 tiles. Slow motion holds up too: 0.1 tiles per second is 0.0017 tiles per tick. Jolt is believed to compute contacts relative to a nearby base position, which helps further; not checked in Godot's integration. Nothing here depends on the subdivision.
  - Free if ever needed: centring the world on the origin halves the worst case.
