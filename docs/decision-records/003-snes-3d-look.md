# 003: Rendering pipeline for the Star Fox SNES look

## Context

Inciting event: issue #3, "Rendering: Star Fox SNES look". `000 §1.6` left the low-resolution approach tentative and untried.

Goal: find a rendering pipeline that gives the look in [high-level-design.md](../designs/high-level-design.md#art-direction), by trying it in a spike: 1993-plausible low-resolution 3D with modern lighting.

Anti-goals:
- Models and tiles. Those are issues #4 and #7. The spike needs only enough geometry to judge the pipeline.
- The camera (Phantom Camera is still unverified).
- A low frame rate. Star Fox ran far below 60 frames per second; the game stays at 60.

Target (Neil, 2026-10-12): Mode X, the 320x240 VGA mode with a 256-colour palette that Michael Abrash made popular, which Neil programmed in at the time. He believes it is where the 320x240 and the quantising in `000 §1.6` came from. Star Fox stays the reference for the geometry; Mode X is the reference for the image.

Closed on 2026-10-13. Outcomes and unresolved items are in [rendering.md](../designs/rendering.md).

Tension: every "modern" feature moves the image away from 1993. The pipeline should make each one easy to switch on and off, so the line can be found by eye.

What the reference looks like (from memory, to be checked against captures): 256x224 output with the 3D drawn in a smaller window; flat-shaded, untextured polygons; very few colours on screen, with ordered dither patterns standing in for the shades between them; no anti-aliasing.

Facts checked on 2026-10-12, against the GodotSharp assembly of the installed 4.7.2:
- `Window.ContentScaleMode` has `Viewport`, and `ContentScaleStretch` has `Integer`.
- `Viewport.Scaling3DMode` has `Nearest`, beside `Bilinear` and the FSR modes.
- `Viewport.Msaa` goes up to 8x.
- `Compositor` and `CompositorEffect` exist.

State of the game's rendering on 2026-10-12: `Main` adds one directional light with shadows, a flat ambient colour and a `Camera3D`. Everything is a `BoxMesh` with a `StandardMaterial3D`. The window is drawn at its native size, with no project display settings.

## 1. Target size

- [x] 1.1 The size of the low-resolution image. Decision: 320x240, as in Mode X. On a 16:9 monitor it has black bars at the sides, and at 1080p it scales by 4, to 1280x960.
  - 256x224: the SNES itself. 8:7, so wider bars.
  - 16:9 sizes that scale to 1080p by a whole number: 320x180 (6x), 384x216 (5x), 480x270 (4x).
  - The spike can switch between sizes at runtime, to compare.
- [x] 1.2 Scaling to the window. Decision: whole-number scale factors only, with black bars, because uneven pixel sizes are obvious on straight edges in motion.

## 2. Getting a low-resolution image

- [x] 2.1 Mechanism. Decision: A, because it is two project settings, and nothing chosen since has needed more (2.2).
  - Option A: the project's stretch mode set to `viewport`, with the base size as the target. The whole game, UI included, is drawn at the target size and scaled up with nearest-neighbour.
  - Option B: the 3D world in a `SubViewport` of our own, shown through a `TextureRect`. More to wire, but the viewport can be any size (e.g. twice the target, for 2.2), the downscale can be our shader, and UI can be drawn at either size.
  - Option C: `scaling_3d` with the `Nearest` mode. Only 3D is drawn small; 2D stays at native size. The scale is a fraction of the window, so the image can't be pinned to an exact size.
- [x] 2.2 Anti-aliasing. Decision (Neil and three of his children, by eye, 2026-10-12): MSAA 2x at the target size. Option A of 2.1 stands, since nothing needs a viewport of our own.
  - None, as in the reference.
  - MSAA at the target size. Smooths polygon edges only.
  - Draw at twice the target and average 2x2 blocks (`000 §1.6`). Also smooths shading, highlights and shadow edges. Needs option B.

## 3. Colour

- [x] 3.1 Quantisation. Decision (Neil, 2026-10-13): a palette of 256 colours defined in HSV: 16 grays, and 8 values above black for each of 6 hues at a saturation of 1/3 and 12 hues at 2/3 and at 1. The steps that led there follow, in order.
  - 3 bits each for red and green and 2 for blue, the 256 colours of Mode X's usual palette: Neil's leaning on 2026-10-12.
  - 3 bits for each channel, 512 colours: Neil's first leaning.
  - Without dithering, 3-3-2 can't hold a gray ramp, which irritated Neil at the time. With dithering he liked a reduced blue channel a lot, which surprised him.
  - Measured 2026-10-12: the shader he judged gave blue 7 values, not the 4 that 2 bits hold. It took one from the number of blue levels instead of halving them. So what was liked is 8x8x7 colours, and true 3-3-2 is still unseen.
  - YUV, with 4-2-2 and 3-2-3 bits. Rejected by Neil: both turned the gray ramp purple. An even number of levels for U or V has none at zero, so gray can't be held.
  - HSV, with 3 bits for hue, 2 for saturation and 3 for value. Neil preferred it to RGB and YUV: the gray ramp is gray even without dither. A lit surface of one colour varies mostly in value, so its hue stays put. But only 176 of the 256 codes are distinct colours, because every hue is the same gray at no saturation, and everything is black at no value.
  - The decision above spends the spare codes on grays and on hues where they show. Measured 2026-10-13 on a captured palette card: 256 distinct colours.
- [x] 3.2 Dithering before quantisation. Decision: yes, a 2x2 ordered (Bayer) pattern on the low-resolution pixel grid. A 4x4 pattern was tried first; Neil finds 2x2 more low-tech, and one of his children called it cute.
- [x] 3.3 Where the colour pass runs. It has to come after tone mapping. Decision: the first, as the least code.
  - Measured 2026-10-12, on the gray ramp of the "Look" room, which is unlit and even in sRGB: the pass reads sRGB values and its output reaches the screen unconverted. The red channel of a captured frame holds exactly the 8 values k/7, and its average over each 4x4 block rises evenly along the ramp, reaching 125 of 255 halfway. Had the pass read linear light, halfway would be near 55. This holds while the project's HDR 2D setting is off, which is the default.
  - A full-screen `ColorRect` on a `CanvasLayer`, with a canvas shader that reads the screen texture.
  - A `CompositorEffect`. Runs inside the 3D pipeline; needs a compute shader and much more code.

## 4. Shading and modern features

- [ ] 4.1 Flat or smooth shading. Flat shading comes from the meshes' normals, so it is a rule for modelling (#4, #7) and not a pipeline setting. The spike should show both.
- [x] 4.2 Which features survive the target size, and which read as 1993. Decision (same four, by eye): shadows, ambient occlusion, screen-space reflections and glow all stay on. "Glow" is the engine's name for bloom. Transparency and refraction were not judged separately.

## 5. The spike

- [x] 5.1 What to judge on. Gray boxes facing the camera show almost nothing: no slanted faces, no curves, no highlights. Decision: a room named "Look", to which the game adds turning shapes: a smooth mirror sphere, a flat-shaded sphere, a wedge, a glowing torus, a pane of glass and a gray ramp. They move because dither and aliasing are judged in motion.
- [x] 5.2 Who writes what. Neil writes the colour shader, `game/look/mode_x.gdshader`. Claude writes the rest: project settings, the room and its shapes, and a switch on a number key for each part of the pipeline (`LookSwitches`).
- As built on 2026-10-12: option A of 2.1 and the first option of 3.3, with a shader that changes nothing yet. A captured frame is 320x240.
- [x] 5.3 Does the spike's code stay? Decision: yes. Unlike the spike of `001`, it is already in the shape the game needs, and Neil wants the switches and the palette card for later changes to the palette.
