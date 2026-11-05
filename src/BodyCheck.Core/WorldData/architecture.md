# WorldData

Relevant decision records:
- [013: The world as a container of rooms](../../../docs/decision-records/013-world-of-rooms.md)
- [020: Drawing rooms](../../../docs/decision-records/020-drawing-rooms.md)
- [043: Triggers, actions and scripts](../../../docs/decision-records/043-triggers-and-scripts.md)
- [045: Control entities in the world](../../../docs/decision-records/045-control-entities-in-the-world.md)

## What it is

The world as data: its rooms, each a grid of tiles at a place in the world, and which kinds of entity start where (`013 §4.2`). It also holds the libraries of rooms described as text, for playing and for tests. It knows nothing of entities as bodies, only their kinds.

## What it is not for

Anything that runs: engine nodes, bodies or colliders. Reading files in an editor's format. Which room an entity is in once it moves, which is a question about what runs.

## Worlds and rooms

- A `World` is a name and its `Room`s, which don't overlap, with exactly one player start among them (`013 §2.3`, `§3.2`). It is data only: nothing at run time knows which room a tile or an entity is in (`013 §1.1`). Its tiles are the union of its rooms', over their bounding box, with everything outside a room empty (`020 §1.1`). Worlds come from two sources: the worlds of an LDtk project, one room per level, and a text room, as a world of one room.
- A world holds its rooms' entity configurations in world coordinates, in two lists: `Bodies`, of the entities with bodies, in the order they are built and stepped, and `Controls`, of the control entities. Each is in the order of the rooms, then of each room's own, so that whatever goes through them does so in a known order. `ConfigsById` finds either kind by id (`013 §4.4`, `045 §2.1`, `§2.2`).
- A `Room` is a name, an origin in the world, in tiles, a tile grid, and a configuration for each entity that starts in it, the player's included. Its tiles and starts are in its own coordinates (`013 §2.1`, `§2.2`).
- An entity's configuration is its kind, as an `EntityKind`, its id, unique in its world, where its box is, and whatever else a level says about it. Each kind has its own type of configuration, from which an entity of that kind is built. An entity's box may lie outside its room.

## Routes

The control entities are a small language for wiring story beats, described in `043 §1`: a `Trigger` runs one step when touched, a `Sequence` runs its steps in turn and a `Fanout` all at once, a `Dialogue` presents a title of a dialogue file, and a `Respawn` becomes the player's respawn. A step is a `Step`: an action, from `TriggerAction` in Core's root, and its target's id (`045 §3.1`, `§3.2`).

- `EntityKinds` says, for each kind, whether it has a body, and which actions it responds to: `Default` alone for a kind that does one thing, and otherwise actions without it, so that `Default` never stands for one of several (`043 §1.4`). It is a table, as switches over every kind; a test checks the rule against it.
- What is true of one room, `Room` checks: every box has a positive size, a platform is one tile high with two different ends, a `Sequence` or a `Fanout` has at least one step, and a dialogue's garble is from 0 to 1 (`045 §4.2`).
- What spans rooms, `World` checks: ids are unique, every step's target is in the world and responds to its action, and no route leads back to where it started (`043 §1.3`, `§1.6`, `045 §4.1`). A route may lead into any of the world's rooms.
- Both throw an `ArgumentException`, so that the checks hold whatever the source.

## Text rooms

Test rooms are a library of named rooms, described as text, each played as a world of one room (`013 §2.4`). `RoomLibrary` has the ones to play in; `TestRoomLibrary` has the ones that tests and probes measure in. Design docs and tests refer to them by name.

## Open questions

- Closing the map: nothing outside the rooms is solid, so a world's rooms must close it with their own tiles, and nothing checks that they do (`020 §1.1`).
