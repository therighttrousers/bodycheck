# 037: Resizable platforms, and a first try at the game's style

## Context

Inciting event (2026-10-30): Neil wants platforms of any width, sized from their boxes in LDtk, with a placeholder model. The model is also the first attempt to make something in the game's style and get feedback on it.

Goals:
- A platform's width comes from its entity's width in LDtk.
- The player can tell that a platform is alive and how it is coping, from a face.
- A look to judge and argue about: a Tron-like one, of simple dark shapes with glowing circuit traces.

Anti-goals:
- Platforms of other heights. They would probably need more models.
- Solving aliasing before seeing whether it is a problem. Neil's daughter says the game could stand to look more "crunchy": a 1990s look, in which something snapping by a pixel was expected.

## 1. Size

- [x] 1.1 How does a platform get its size? Decision (Neil): from its box in LDtk, less a margin on every side.
- [x] 1.2 Which sizes does the reader accept? Decision (Neil): a whole number of tiles wide and exactly one tile high. Loosen the height if we want thicker platforms.
- [x] 1.3 Where is the body in its box? As built by `025 §1.4`, the body's bottom is at the box's bottom, and the margin is all at the top, so a platform in the cell above a floor has no gap below it, whatever the margin. That is why low platforms scraped the floor. Neil wants them not to touch it. Decision (Neil): center the body in its box, so the margin is on every side. This also removes the offset that `WorldNode.CreatePlatform` adds to the line, which is now simply where the body's center goes.
- [x] 1.4 The margin. It was 0.1 tiles, which is 1.6 pixels. First decision (Neil): 2 pixels, for clearance, which left 12 rows for the face. Once the body was centered, which gives it clearance by itself, changed (Neil): 1 pixel, or 0.0625 tiles. The body is then 14 pixels high, and 30 or 46 wide for a platform 2 or 3 tiles wide. An empty platform clears a floor below its cell by 1 pixel. One with a rider sags further than that (§2.2), so it touches.
- [x] 1.5 Platforms of even width. Decision (Neil): no change for now (`025 §1.3`). LDtk's configuration is still changing, and a platform's start must stay inside its box in the editor, which it does.
- [x] 1.6 How do a body, its box in the map and its model line up, now that bodies of a kind differ in size? Neil: with an anchor, a spot on a box. The model's origin goes there, and the anchor on the body is pinned to the anchor on the map's box. This replaces each entity's start at the middle of its box's base, and the offset to the line. Where does it live? First built on `EntityConfig`, which measured the model's offset on the map's box, not the body, so that walkers were drawn 0.1 tiles low; and the player's block, which has a body and no configuration, had no anchor at all. Decision (Neil): on `BodyTuning`, since it describes a body. By default the middle of the bottom edge; a platform's is its center. `EntityConfig` holds only the map's box, in whole tiles.

## 2. Mass and thrust

- [x] 2.1 A platform's mass comes from its size (`025 §4.3`), and platforms carry the player on purpose. Until now they did so by accident, being heavy enough. The caps were fractions of the platform's weight, so at the same density a platform one tile wide, of mass 1.67, could hold only 0.83, and the player would sink it. Decision (Neil): specify mass and capacity per tile of width, with neither a fraction of the other. The features `PlatformMassPerTile`, 2.5, and `PlatformCapacityPerTile`, 1.25, a mass, replace `PlatformMass`, `PlatformMaxDriveFrac` and `PlatformMaxHoldFrac`. Both caps are the capacity times unscaled gravity, so the same load still overloads a platform whichever way it moves (`025 §2.2`). The two features' ratio keeps the maximum acceleration at today's half of gravity, at every width, so all platforms still accelerate alike. Capacity per tile is above the player's mass of 1, so every platform carries the player.
- [x] 2.2 What follows from it, worked out from the code and not yet measured:

  | Width | Mass | Capacity | Sag under the player |
  |---|---|---|---|
  | 1 | 2.5 | 1.25 | 8 pixels |
  | 2 | 5 | 2.5 | 4 pixels |
  | 3 | 7.5 | 3.75 | 2.7 pixels |

  The player with a block, a mass of 3, no longer sinks a platform 3 tiles wide, but does sink one 2 tiles wide. A platform 3 tiles wide goes from a mass of 5 to 7.5, so phasing and pushing it change a little.

## 3. The face

- [x] 3.1 Eyes. Decision (Neil): everything that can move on its own has distinguishable eyes. A platform's are the color of its traces. Now in [high-level-design.md](../designs/high-level-design.md).
- [x] 3.2 A mouth. Decision (Neil): a platform also has one. It smiles slightly, with two 45-degree turns, is straight under some load, and frowns under more.
- [x] 3.3 How does the mouth change? Decision (Neil): a state machine in an `AnimationTree` in the wrapper scene, which Neil builds, so that he learns it well enough to teach it. The controller publishes a number on the entity, and the tree's expressions compare it, so the thresholds are part of the art.
- [x] 3.4 Which number? Neil: thrust or load, not distance from the line, which never changes under a rider on a vertical platform. A vertical platform may frown as it lifts the player and smile on the way back down. The thrust that cancels the platform's own weight doesn't count. Options: (a) the larger of the two capped thrusts, which frowns after every turn, where the drive is at its cap with nothing on board, and frowns going down, where it holds back a rider as hard as it lifts one; (b) Claude: the same, but counting only the drive that fights something: less what the platform's own acceleration used, and only while it pushes the way the platform is heading. Decision (Neil): (b), published as `Strain` on a new `PlatformEntity`: the larger of the hold's size and that drive, over the cap. An empty platform and one going down with a rider have a strain near 0. On a platform 3 tiles wide the player gives 27%, a block 53%, and both 80%; a platform that is overloaded, stopped against something, or far off its line gives 100%. Starting thresholds for the tree: straight from 15% and a frown from 75%, with lower ones on the way back.
  - Measured on a platform 3 tiles wide: 2% empty, through its turns too; 27% with the player and 53% with a block, moving sideways; on a vertical line, 29% lifting the player and 4% lowering it. The 2% is drag, which the drive also fights.
- [x] 3.5 One-tick spikes in `Strain`, such as when something lands on the platform, could make the mouth flicker. Options: (a) smooth `Strain` in code, over about 0.1 seconds; (b) in the tree, with clips of about that length and transitions set to switch at the end of a clip, so that a mouth stays for at least that long. Decision (Neil), from building the tree: (b), which leaves `Strain` a plain measurement and keeps the timing with the art, but with clips of 1 second, and the same thresholds both ways: straight above 0.15 and a frown above 0.75. The spike turns out to be the best part: a platform frowns for a moment when the player lands on it or jumps off it. An overloaded platform, which can't move, frowns for as long as it lasts.

## 4. The model

- [x] 4.1 Structure. Decision: one `.blend` and one `.glb`, with the end caps, the center and the face as separate objects. The game places the caps and scales the center along X.
- [x] 4.2 The center is a box beveled on its four edges along X, stretched and not tiled, since its traces are horizontal. It is flush with the end caps.
- [~] 4.3 Traces. Decision (Neil): start with a larger texture with the default filtering. Pixelated traces with 45-degree turns, drawn in a low-resolution viewport, would have two sources of aliasing.
  - Tried: a texture at 8 times the game's resolution, with linear filtering. Traces came out 2 pixels wide whenever the camera sat between two rows, and the 1-row gaps of §4.4 can't survive that; with a stronger glow, bloom also fused the eyes and mouth with the traces.
  - Leaning (Claude; Neil's to judge): an atlas at the game's resolution with nearest filtering. The front face is exactly 16 pixels to the tile (§4.5), so a texel lands on one pixel, and there is only one source of aliasing after all. Traces stay 1 pixel wide and sharp, at rest and moving, and it suits "crunchy".
- [x] 4.4 The front face's 14 rows, top to bottom: 1 bevel, 1 gap, 1 trace, 1 gap, 3 eyes, 1 gap, 2 mouth, 1 gap, 1 trace, 1 gap, 1 bevel. The gaps next to the bevels keep the traces from touching them.
- [x] 4.5 Depth. The front face is at z = 0.5, level with the tiles' front faces, where a tile is exactly 16 pixels.
- [x] 4.6 Filtering. Godot ignores the glTF's nearest sampler, and imports linear filtering with mipmaps. So the model's material is replaced in its import settings by `body.tres`, which sets nearest filtering, and is also the one place to tune the body and the glow. Its `emission` stays black: a `StandardMaterial3D` adds the emission texture to that color, so white would light the whole body.
- [x] 4.7 The eyes and mouths are quads just in front of the face, of the same material, so a mouth changes by showing one and hiding the others. MSAA dims lit pixels on a mesh's edge, so each quad's edges run down the middle of an empty margin, and the art on the caps and center repeats in a gutter in the atlas, for pixels on the seams between them. Lit art away from those edges isn't affected, so the circle at the end of a trace is 3 pixels across, set in from the seam and the bevel, not the 5 that fill the cap. A cap is 6 pixels wide with its bevel.
