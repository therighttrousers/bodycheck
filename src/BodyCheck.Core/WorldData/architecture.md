# WorldData

Relevant decision records:
- [013: The world as a container of rooms](../../../docs/decision-records/013-world-of-rooms.md)
- [020: Drawing rooms](../../../docs/decision-records/020-drawing-rooms.md)

## What it is

The world as data: its rooms, each a grid of tiles at a place in the world, and which kinds of entity start where (`013 §4.2`). It also holds the libraries of rooms described as text, for playing and for tests. It knows nothing of entities as bodies, only their kinds.

## What it is not for

Anything that runs: engine nodes, bodies or colliders. Reading files in an editor's format. Which room an entity is in once it moves, which is a question about what runs.

## Worlds and rooms

- A `World` is a name and its `Room`s, which don't overlap, with exactly one player start among them (`013 §2.3`, `§3.2`). It is data only: nothing at run time knows which room a tile or an entity is in (`013 §1.1`). Its tiles are the union of its rooms', over their bounding box, with everything outside a room empty (`020 §1.1`), and it lists every room's entity configurations in world coordinates, room by room (`013 §4.4`). Worlds come from two sources: the worlds of an LDtk project, one room per level, and a text room, as a world of one room.
- A `Room` is a name, an origin in the world, in tiles, a tile grid, and a configuration for each entity that starts in it, the player's included. Its tiles and starts are in its own coordinates (`013 §2.1`, `§2.2`).
- An entity's configuration is its kind, as an `EntityKind`, where it starts, and later whatever else a level says about it. Each kind has its own type of configuration, from which an entity of that kind is built.

## Text rooms

Test rooms are a library of named rooms, described as text, each played as a world of one room (`013 §2.4`). `RoomLibrary` has the ones to play in; `TestRoomLibrary` has the ones that tests and probes measure in. Design docs and tests refer to them by name.

## Open questions

- Closing the map: nothing outside the rooms is solid, so a world's rooms must close it with their own tiles, and nothing checks that they do (`020 §1.1`).
