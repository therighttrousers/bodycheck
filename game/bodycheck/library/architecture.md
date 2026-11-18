# `game/bodycheck/library/`

Relevant decision records:
- [012: Subsystems in `BodyCheck.Core`](../../../docs/decision-records/012-core-subsystems.md)
- [020: Drawing rooms](../../../docs/decision-records/020-drawing-rooms.md)
- [057: Tile kinds](../../../docs/decision-records/057-tile-kinds.md)
- [061: Environments per room](../../../docs/decision-records/061-room-environments.md)
- [062: Background layers](../../../docs/decision-records/062-background-layers.md)

## What it is

`AssetLibrary`, the game's asset library, which implements Core's `IAssetLibrary`: it makes each kind of entity's visual and gives each kind of tile's mesh, so that Core doesn't know the game's assets (`012 §3.1`, `020 §2.2`), and it makes each room environment's scene and each room's decoration.

## What it is not for

The assets themselves, which are content, in `game/assets/`, or what runs in an entity's scene once it is made.

## What it makes

- **Entities.** Each kind's visual is its wrapper scene in `assets/entities/`, or a box the size of its body. The door's look is built in code, with its sounds as a `CueSounds` with placeholder sounds.
- **Tiles.** It maps each kind of tile to its wrapper scene in `assets/tiles/`, and loads each kind's mesh, and each darkened copy, once (`057 §3.1`).
- **Environments.** It makes each room environment's scene, from `assets/environments/`, with SSR, SSAO, SSIL and glow switched on in code, so that no environment has to remember them (`061 §1.2`).
- **Decorations.** It reads the decorators in each environment's `Decorations` the first time a room of it is built, from a copy of the scene it then frees, and puts what each makes for the room under one node, which `RoomNode` adds. A scene without `Decorations` has none; one whose `Decorations` isn't a `RoomDecorations`, or lists an empty slot, is an error (`062 §3.4`).
