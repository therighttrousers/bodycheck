# 020: Drawing rooms

## Context

Inciting event (2026-10-21): Neil's work on the tiles' looks, on the `tile-visuals` branch. A model for each tile, drawn as a mesh instance each, would eventually become too slow, and the world's single back wall needed replacing with something that has depth.

Closed on 2026-10-21. Outcomes are in [architecture.md](../designs/architecture.md), with what is still open in its open questions.

Goals:
- Draw every tile as a model, at a frame rate that holds up for a whole game's rooms.
- Keep the seams between rooms free of ghost collisions (`001 §1.2`, `011 §2.1`).

## 1. The world's tiles

- [x] 1.1 What is outside every room? Decision (Neil): empty, superseding `011 §3.3` and `013 §3.1`, which made it solid. The world is the union of its rooms. A map must be closed by its rooms' own tiles, so that nothing can leave it; that isn't checked yet.

## 2. Drawing

- [x] 2.1 How are tiles drawn? Decision (Neil): `WorldNode` holds a `RoomNode` for each room, which draws the room's tiles as a few multimeshes: the solid tiles, a darker layer behind them, and a back wall with holes in it that spreads from them. Rationale: one multimesh draws many instances in one call, and a node per room is where drawing only the explored rooms would go (architecture's open questions).
- [x] 2.2 How does Core get the tile's look? Decision (Neil, with Claude): the game gives `WorldNode` an `IAssetLibrary`, in place of `012 §3.1`'s function for entities' visuals. It gives what to draw for each kind of entity, and the tile's mesh, darkened by a given amount, with the transform that fits it to a unit cube. The game's library loads the mesh once and each darkened copy once, so that rooms and worlds share them and no scene's nodes are left behind.

## 3. Colliders

- [x] 3.1 Per room, or for the whole world? Decision (Neil): still one static body for the whole world, with the tiles merged into boxes. Colliders per room brought back ghost collisions at rooms' edges, since the engine removes them only between shapes of one body. One collider per tile is gone, and with it the seam probe's option for it.
- [x] 3.2 Entities stay the world's, not a room's (`013 §1.1`).
