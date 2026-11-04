# 040: No vertex snap

## Context

Inciting event (2026-11-03): Neil considered removing the vertex snap of `038 §1.1`. Having played without paying attention to whether it was on or off, he doesn't notice when it is on.

Against keeping it:
- It adds complexity to the art pipeline. Materials are authored as `StandardMaterial3D` and replaced at runtime by a `ShaderMaterial` from a template, which has to keep up with every feature the game's materials use (`038 §1.2`).
- A change to a material after it's replaced doesn't show. The node draws a copy whose uniforms were set once, so code that changes the original later, such as a hit flash or a pulsing glow, does nothing, and neither does tuning a material in the remote inspector while the game runs. Code that reads the node's material as a `StandardMaterial3D` gets nothing. It is also a second copy of what the engine already holds.
- It is harder for artists to get right. A platform's face bobs when the platform moves a little vertically (`038 §2.4`), and there's no obvious way to stop it: whether a vertex rounds up or down depends on where it falls between pixels, which changes with the camera.
- Materials with features the template lacks, and directional shadows, stay unsnapped, and the editor shows nothing snapped, so what is previewed isn't what is played.

For keeping it:
- It might add a little to the crunchiness of the look.

## 1. The snap

- [x] 1.1 Keep or remove. Claude: at 320x240 without anti-aliasing, the rasterizer already puts every edge on a whole pixel, so the snap adds only that each vertex rounds on its own, which deforms shapes by at most a pixel. Low resolution, the palette and its dither, nearest textures and stepped animation carry the look. A blind comparison, with someone else toggling it, was offered. Decision (Neil): no comparison; remove it. `VertexSnap` goes, and with it the runtime replacement of materials and the key that switched it. Supersedes `038 §1.1` and `§1.2`. The nearest filtering of `038 §3.1` and the stepped animation of `038 §4` don't depend on it and stay.
