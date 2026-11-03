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

## What it is

Reading the levels that are authored in LDtk into a world of rooms, one per level, with every value checked as it is read, as [the convention for reading data](../../../docs/designs/architecture.md#conventions) says. It holds the types that describe LDtk's JSON.

## What it is not for

Saving or loading the game's state. Deciding what a room means once it is read. Formats other than LDtk's.

## The LDtk project

The game's maps are the worlds of one LDtk project, `levels/main.ldtk` in the game, with LDtk's multi-worlds on, so that they share its layers, enums and entities (`017 §3.1`).

- `LdtkReader` reads each of the project's worlds into a `World`, named after it, with a `Room` for each level, named after the level's identifier (`013 §4.5`), and rejects a project without multi-worlds. Tiles still merge across levels' edges, since the colliders are built from the world's grid (`011 §2.1`).
- Levels may be any whole number of tiles in size, at any whole number of tiles from each other, at depth 0, as long as no two overlap (`024 §4.1`). Space with no level is empty (`020 §1.1`), and LDtk's `__neighbours` are ignored (`011 §3`). The reader moves each world's levels together so that the leftmost edge of any of them is at its origin's x, and the lowest edge at its y. Each world has one player start.
- Every non-zero value of the `SolidGeometry` layer is a solid tile, and what LDtk writes for auto-layering is ignored.
- Entities with bodies are in the `Characters` layer, and those without, such as respawns, in the `ControlEntities` layer. Each entity keeps the place, size and colour of its box in LDtk, which must be whole tiles; a platform's must also be one tile high (`037 §1.2`). The game starts a body with its anchor at the same spot on that box ([models.md](../../../docs/designs/models.md#how-the-game-places-a-model)), and draws the box on the map markers. Entity fields are ignored, except a platform's range (`009 §1`, `025 §1.1`).
- The reader is given a function that reads files, so the game reads through Godot's `FileAccess` and tests through `System.IO`.
