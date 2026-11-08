# Levels

Relevant decision records:
- [009: The first level loader](../../../docs/decision-records/009-level-loader.md)
- [011: Stitching LDtk levels into one world](../../../docs/decision-records/011-stitched-levels.md)
- [013: The world as a container of rooms](../../../docs/decision-records/013-world-of-rooms.md)
- [017: The playtesting and puzzle design workflow](../../../docs/decision-records/017-playtesting-workflow.md)
- [020: Drawing rooms](../../../docs/decision-records/020-drawing-rooms.md)
- [024: Playtesting feedback](../../../docs/decision-records/024-playtesting-feedback.md)
- [025: Platforms that move along any line](../../../docs/decision-records/025-platforms-on-any-line.md)
- [037: Resizable platforms, and a first try at the game's style](../../../docs/decision-records/037-resizable-platforms-and-first-style.md)
- [043: Triggers, actions and scripts](../../../docs/decision-records/043-triggers-and-scripts.md)
- [044: Dialogue](../../../docs/decision-records/044-dialogue.md)
- [045: Control entities in the world](../../../docs/decision-records/045-control-entities-in-the-world.md)
- [047: `Once`, and routing actions](../../../docs/decision-records/047-once-and-routing.md)
- [049: Doors and their stops](../../../docs/decision-records/049-doors-and-stops.md)
- [052: The talking door](../../../docs/decision-records/052-talking-door.md)
- [057: Tile kinds](../../../docs/decision-records/057-tile-kinds.md)

## What it is

Reading the levels that are authored in LDtk into a world of rooms, one per level, with every value checked as it is read, as [the convention for reading data](../../../docs/designs/architecture.md#conventions) says. It holds the types that describe LDtk's JSON.

## What it is not for

Saving or loading the game's state. Deciding what a room means once it is read. Formats other than LDtk's.

## The LDtk project

The game's maps are the worlds of one LDtk project, `levels/main.ldtk` in the game, with LDtk's multi-worlds on, so that they share its layers, enums and entities (`017 §3.1`).

- `LdtkReader` reads each of the project's worlds into a `World`, named after it, with a `Room` for each level, named after the level's identifier (`013 §4.5`), and rejects a project without multi-worlds. Tiles still merge across levels' edges, since the colliders are built from the world's grid (`011 §2.1`).
- Levels may be any whole number of tiles in size, at any whole number of tiles from each other, at depth 0, as long as no two overlap (`024 §4.1`). Space with no level is empty (`020 §1.1`), and LDtk's `__neighbours` are ignored (`011 §3`). The reader moves each world's levels together so that the leftmost edge of any of them is at its origin's x, and the lowest edge at its y. Each world has one player start.
- Each value of the `SolidGeometry` layer is a tile's kind, a `TileIndex`, and the reader rejects a value that isn't one. What LDtk writes for auto-layering is ignored. LDtk draws the layer with `assets/authoring/ldtk-atlas.png`, a cell for each kind, so that levels look somewhat as they do in the game (`057 §1.1`, `§1.4`, `§3.2`).
- Entities are in two layers, `Entities` for those with bodies and `Control` for the control entities, which LDtk keeps apart by their tags. The layers only help the editor show them: the reader reads both alike, and an entity's kind says whether it has a body (`045 §2.3`). LDtk lets the control entities but `Respawn` lie outside their level, to be placed out of the way.
- Each entity keeps its `iid` as its id, and the place, size and colour of its box in LDtk, which must be whole tiles (`045 §1.1`). The game starts a body with its anchor at the same spot on that box ([models.md](../../../docs/designs/models.md#how-the-game-places-a-model)), and draws the box on the map markers.
- The fields read are a platform's range (`025 §1.1`); a door's `OpenStop` and `CloseStop` (`049 §1.1`); a `Trigger`'s and a `Once`'s `Target` and `Action`; a `Trigger`'s `StartsOn` and `SetOffBy` (`052 §1.2`, `§2.2`); a `Sequence`'s and a `Fanout`'s `Targets` and `Actions`; a `Dialogue`'s `DialogueFile` and `DialogueTitle` (`043 §1.1`); and a `Script`'s `ClassName` and `Entities` (`056 §2.2`); how garbled a line is, its dialogue file says (`044 §3.7`). An `EntityRef` is read as the `iid` it refers to, and a `TriggerAction` or a `TriggerSetOffBy` by name. The reader pairs `Targets` with `Actions` as steps, and rejects them if they aren't the same length (`043 §1.5`, `045 §3.1`).
- What doesn't depend on LDtk is checked by the world's data, which the reader reports as an error in the file: a box's size, a platform's shape, and the routes (`045 §4`).
- The reader is given a function that reads files, so the game reads through Godot's `FileAccess` and tests through `System.IO`.
