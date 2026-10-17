# 011: Stitching LDtk levels into one world

## Context

Inciting event (2026-10-17): work on issue #32, stitching LDtk levels together, began. Neil added `Level_1` to `game/levels/main.ldtk`, to the right of `Level_0`, with an opening between them. `Level_1` has a robot and a platform, and no player start.

What was there: `LdtkReader` reads each level into a `Room` of its own (`009 §1.1`), and a `Room` must have exactly one player start, so `Level_1` doesn't load. `Main` lists the levels, then the text rooms, and plays one at a time. `CameraRig` clamps the view to the room's tile grid (`010 §2.1`).

Goal: walk from `Level_0` into `Level_1` in the game, as one seamless world. Anti-goal: anything that a larger world will need but two levels don't. Scope is to be kept small (Neil).

Closed on 2026-10-17. Outcomes are in [architecture.md](../designs/architecture.md) and [rendering.md](../designs/rendering.md), and so is the next step (§2.2).

## 1. Scope

- [x] 1.1 Out of scope (Neil): caching, so everything is loaded at once; pausing and resuming entities; a respawn policy.
- [x] 1.2 Levels are 20x15 tiles, aligned to a grid (Neil). Every level is 320x240 pixels, at a `worldX` and `worldY` that are multiples of 320 and 240.

## 2. How the world is held

- [x] 2.1 One `Room` for the whole world, or a container of rooms, one per level? Decision (Neil): one `Room`. The reader stitches every level into one tile grid, with entity starts in world coordinates, and the game plays it as it plays any room. `RoomNode`, the colliders and the entities don't change, bodies cross between levels freely, and tile merging works across them.
  - Rejected for now: `World` as a container of `Room`s, which would take over some of `Room`'s responsibilities, such as the player, so that it doesn't move from room to room. The aim would be a spatial subdivision ready for caching, respawn rules and rough render culling, none of which would be built yet.
  - Why: what those features need from a subdivision differs (caching may want chunks sized by memory and load time, respawn may want sections or challenges, culling whatever is cheap to test), and without them its boundaries are a guess. The hard part of a subdivision is ownership of what can't be subdivided: a block across two levels, a robot on a platform that crosses an edge, the player pushing a block through an opening. Those rules depend on how caching and pausing work, so writing them now is guessing too. And splitting later is cheap: on the grid of §1.2, the level a tile or entity is in is a division.
- [x] 2.2 Next step, in a PR of its own (Neil): `World` becomes a container of `Room`s, moving toward the eventual design in small steps. The name `World` stays, because the code already uses "world" for this concept. Its constant, `TilePixels`, moves to another class, so that `LdtkReader` doesn't depend on `World`, which will depend on `LdtkReader`.

## 3. The stitched world

- [x] 3.1 One player start in the whole world, rather than one per level. Moving it in LDtk is how to start somewhere else.
- [x] 3.2 The world is one room, `main`, first in the list that `,` and `.` cycle through, before the text rooms. A level can't be named on the command line any more, which reverses that part of `009 §4.3`.
- [x] 3.3 Grid cells with no level are solid, as everything outside a `TileGrid` already is. So a world that isn't a rectangle needs nothing new.
- [x] 3.4 The reader checks §1.2: every level is 320x240, at a multiple of 320 and 240, with `worldDepth` 0, and the project has one world. The grid rules out overlaps.
- [x] 3.5 `__neighbours` is ignored: the grid already says which levels neighbour. Openings aren't checked to line up; an opening against a wall is a wall.
- [x] 3.6 The room doesn't keep which level a tile or entity came from (§2.1).
- [x] 3.7 No culling, and one back wall over the world's bounding box. Every collider is built at load.
- [x] 3.8 Tests: one reader test that stitches two levels, offset from each other, and the test that every level reads becomes a test that the world reads.

## 4. The camera

- [x] 4.1 What the camera keeps inside. Decision (Neil): the world's bounding box, as it clamps to the room's tile grid now. It's only to keep the stitched world playable; the camera will be designed and tuned later. `010 §2.3`, keeping to the level the player is in, stays open.
