# 062: Background layers

Status: open. Still to settle: the placeholder backdrops (§2.6), the back wall's parameters (§3), and the leanings in §2.3, §2.5 and §4.

## Context

Inciting event (2026-11-12): issue #202, background layers, which follows per-room environments (`061`). An environment's background is a procedural sky or a colour, and anything fancier was left for background layers (`061 §1.3`). Separately, the back wall with holes that `RoomNode` drew behind the tiles has been gone since `057`, and what replaces it is open (`057 §2.2`).

Goals:
- Background art under artists' control, made in the Godot editor. Nobody sets LDtk fields or paints tiles in LDtk for it: fields are abstract, and tiles fit non-tile visuals poorly (Neil).
- Art effort that a team of one, and kids working very part-time, can keep up with (Neil). A procedural background, as the back wall was, may be the better trade.

Tensions:
- Levels are authored in LDtk and read at runtime (`000 §1.2`), so the Godot editor shows nothing of a room. Art placed by hand needs a guide to where the tiles are, which may mean a build step, or reading LDtk in the editor.
- The camera is perspective. At `b` tiles behind the tiles' front faces, a screen shows about `15 (16.1 + b) / 16.1` tiles of height, and the layer moves at `16.1 / (16.1 + b)` of the tiles' speed: at `b = 16`, 30 by 40 tiles, at half speed. The camera boundary keeps outside space out of view only at the front faces (`042 §5.3`), so a deep layer shows past its room's edges, and from neighbouring rooms.
- Dithering in the colour pass may be on or off in the end; Neil hasn't decided (2026-11-12).

## 1. Two kinds of background

- [x] 1.1 What "background layers" covers. Claude: two different things.
  - **Backdrop:** far away, about the place's mood rather than particular tiles: sky, distant machinery, mountains, haze. It belongs to an environment.
  - **Set dressing:** close behind particular tiles: a pipe run behind this corridor, a window there. It belongs to a room.

  Depth decides between them. Deep layers placed per room would show from neighbouring rooms, shifted by parallax, and would have to cover about twice a room's area at `b = 16`. Shallow layers stay about inside their room, and what shows of a neighbour's through an opening is really there, so they need no fade. Decision (Neil): treat them separately.

## 2. Backdrops

- [x] 2.1 Where they live. Decision (Neil): in the environment's scene, which already brings along anything besides its `WorldEnvironment` and `Lighting` (`061 §1.1`), so a backdrop is made and tuned in the editor, and art scales with the number of environments, not of rooms.
- [x] 2.2 How they move. Decision (Neil, with Claude): a backdrop follows the camera with a parallax factor, rather than being placed in the world, so that it has no edges to hide and needs no guide. A node script on each layer does it, so the artist sets the factor in the editor.
- [~] 2.3 Render layer. Leaning: `Background`, as `041 §4.1` lists, so that the map's zoom and the debug views can leave backdrops out.
- [x] 2.4 Fading as environments blend. Each environment's scene stays in the tree while it has weight (`061 §4.1`), so its backdrop shows with it, and has to fade by the weight. Decision (Neil): not by a dissolve in the colour pass's Bayer pattern (`061 §5.1`): fading mustn't need dither, or change `mode_x.gdshader`, since whether dither stays on is undecided. Options:
  - A. Transparency: each layer's material takes the weight as its alpha. Overlapping backdrops of two environments sort by depth, or by render priority set per layer. Cheap; the colour pass then quantises the mix.
  - B. Fade into the mixed sky: each layer's colour moves toward the sky's by its weight, staying opaque. No sorting, and it reads as haze, but a heavier environment's opaque layer still hides a lighter one's, so the two backdrops pop where they overlap.
  - C. A wipe by position: each backdrop is cut where its rooms end, with a soft edge, like the weights themselves (`061 §3.1` E). Parallax makes "where its rooms end" hard to define for a layer at depth.
  - D. A swap where the weights cross a half. Free, but a pop.

  Claude leaned to A, which fades most like a crossfade and has no sorting problems among layers that are flat and parallel. Decision (Neil): A, transparency.
- [~] 2.5 What a layer is. Leaning: a textured quad, at a depth behind the tiles, for 2D art; or a model, for 3D. The quad suits pixel art and the placeholders (§2.6).
- [ ] 2.6 Placeholder backdrops. They must be licensed so that we can use them, and be credited in `ACKNOWLEDGEMENTS.md`. Candidate sources (Claude): Kenney's background packs (CC0); OpenGameArt's parallax backgrounds, many CC0 or CC-BY; free packs on itch.io, whose licences vary pack by pack; Poly Haven's skies (CC0), which are panoramas and would need a sky layer rather than a quad. Or simple shapes made in Blender, which suit the look and need no credit.

## 3. The back wall

- [~] 3.1 What replaces it (`057 §2.2`). Decision (Neil): procedural, as the old back wall was, spreading from the solid tiles with holes in it, so that no room needs art of its own. To decide: its parameters, such as its mesh or material, density and depth.
- [~] 3.2 Where its parameters live. Leaning: in each environment's scene, so that an artist tunes them in the editor, and a cave's wall can differ from a facility's.

## 4. Set dressing placed by hand

- [x] 4.1 When. Decision (Neil): later, when a particular room needs more than the back wall gives, such as for a story beat. Until then, backdrops (§2) and the back wall (§3) are the background.
- [~] 4.2 Where it lives. Leaning: an optional scene per room, such as `assets/rooms/<world>/<level>.tscn`, which the game adds at the room's anchor, so that moving a level in LDtk takes its dressing along. With the anchor at the room's bottom-left, growing a level right or up leaves the dressing in place, but growing it left or down shifts it, which nothing detects. A room's scene could also turn off the back wall for its room.
- [~] 4.3 The guide in the editor. Options:
  - A. LDtk's own image export: LDtk writes a PNG of each level when it saves, and a small GDScript tool node in the room's scene shows it on a quad at the tiles' plane, the level's size, at the room's origin. No build step, and no Core in the editor. Flat, but the solid tiles' silhouette is most of what placement needs. To check: LDtk's image export setting and what it writes.
  - B. A C# tool node that reads `main.ldtk` with `LdtkReader` and draws the tiles as `RoomNode` does, at their real depth, with a preview camera at the game's distance, and reads the file again when it changes. To check, with a spike: whether the editor unloads the game's assembly cleanly after a build, since libraries that cache types, possibly `System.Text.Json`, can stop it, and the editor then needs restarting.
  - C. A build step: an MSBuild target, after the build, writes the rooms as Godot scenes, so that the editor's build button rebuilds them. It means writing Godot's scene format by hand, running Core outside the engine, and generated files that either come out the same on every build, for CI's no-changes check, or are ignored by Git.

  Leaning (Claude): A first; B if placement needs the tiles' depth; C only if both fall short.
