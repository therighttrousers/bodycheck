# 041: Foreground visuals

## Context

Inciting event (2026-11-03): issue #120, random refractive details in the foreground. The removal of the vertex snap (`040`) took away what blocked it: refraction works on any `StandardMaterial3D` again.

Goals:
- Try foreground visuals in play, with a key to turn them off, or to cycle through variants, for comparison.
- Find out whether foreground visuals are worth having at all. Neil often finds them annoying as a player, especially when they hide the ground.

Tensions:
- A foreground layer works well in a game of separate rooms joined by doors, where the view is always limited: columns that reach far enough down look as if they connect to something offscreen. In a 2.5D open world, the player may see the same visuals from the room below, where they also have to look plausible.
- Procedural placement is cheap, but it may not give plausible results. Placement by artists may, with good positioning and enough margin around rooms, but it costs more art work.

## 1. Geometry

- [~] 1.1 What the player sees of a room's foreground. Superseded in part: Neil prefers a camera clamped by the static tiles to one clamped to rooms, and `042` deliberates it, with what it means for the foreground (`042 §2.1`). The camera is 16.1 tiles from the tiles' front faces (a 50° field of view, 15 tiles high), and the view narrows toward the camera. At `f` tiles in front of the front faces, the view is smaller by `7.5 f / 16.1` tiles above and below and `10 f / 16.1` tiles to each side: about 1.9 and 2.5 tiles at `f = 4`, and 3.7 and 5 at `f = 8`. If the camera is clamped to the player's room, foreground kept inside that room's rectangle never shows its edges, and never shows from another room. Today the camera is clamped to the world's bounding box, not to the room (rendering's open questions), so this needs that question settled, and it leaves the moment the camera passes from one room to the next. Parallax: at `f = 4`, the foreground moves 1.33 times as fast as the tiles, and at `f = 8` twice as fast.

## 2. What and where

- [ ] 2.1 Kinds of visual. Options: refractive details, such as glass, crystals, drops of water and bubbles, which hide less of what is behind them; solid silhouettes, such as columns, pipes and vines; or both.
- [ ] 2.2 Procedural or authored. Procedural, as `RoomNode` already makes the back tiles and the back wall; authored, from the LDtk room data; or procedural with an authored seed or density per room.
- [ ] 2.3 Keeping the ground visible. Options: keep foreground visuals away from the band the player plays in; fade what comes between the camera and the player; rely on refraction to leave what is behind them visible.

## 3. Comparing

- [~] 3.1 The switch. Key 2, unused since `040`, turns the foreground off and on, or cycles through variants. Leaning (Neil, with Claude): through the camera's cull mask, with the foreground on render layers of its own (§4), so that `LookSwitches` needs nothing from `RoomNode`, and the switch survives a change of world. Each variant is built up front on a layer of its own, and the key steps through the bits. To check: whether a layer hidden from the camera still casts shadows; if so, the switch changes the light's shadow caster mask too.

## 4. Render layers

- [x] 4.1 Layers by role. Decision (Neil): render layers for at least `Background` (the back tiles and the back wall), `Tiles` (the solid, playable tiles), `Entities`, `Foreground`, perhaps several, and `Markers`, for the map markers that F2 shows. Besides the foreground switch, they let the map's zoom show only some layers, and lights treat layers differently.
- [~] 4.2 Assigned in the editor or at runtime. Claude: at runtime, by the code that builds each visual, which knows its role: `RoomNode` sets its multimeshes', and the asset library sets every `VisualInstance3D` in an entity's visual. A node's layers aren't inherited by its children, so in the editor every mesh node in every imported model would need setting, and the tiles are multimeshes built in code anyway. A `RenderLayer` enum in Core's root, like `PhysicsLayer`, with the same names in `project.godot` so that the editor shows them, and a test that checks they agree.
