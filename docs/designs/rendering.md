# Rendering

Relevant decision records:
- [000: Initial design](../decision-records/000-initial-design.md)
- [003: Rendering pipeline for the Star Fox SNES look](../decision-records/003-snes-3d-look.md)

The image is Mode X with modern lighting: 320x240 and 256 colours, as on a VGA card in the mid-1990s, drawn with anti-aliasing, shadows, ambient occlusion, reflections and bloom. Star Fox, Quake and Descent are the references for the geometry, and Mode X for the image (`003`).

## Pipeline

1. **Low resolution.** The project's stretch mode is `viewport` with a base size of 320x240, so the engine draws the whole game, UI included, at that size (`003 §1.1`, `§2.1`).
2. **Lighting.** MSAA 2x, shadows, ambient occlusion, screen-space reflections and glow, which is the engine's name for bloom. All were chosen by eye at the target size (`003 §2.2`, `§4.2`).
3. **Colour pass.** `ColorPass` covers the screen with a rectangle whose shader, `game/look/mode_x.gdshader`, reads what is behind it and writes it back in the palette, with dither (`003 §3.3`).
4. **Scaling.** The engine scales the image to the window by the largest whole number that fits, with nearest-neighbour sampling, and fills the rest with black bars. The window starts at 1280x960, which is 4x.

The colour pass reads tone-mapped sRGB values, and its output reaches the screen unconverted. This holds while the project's HDR 2D setting is off, which is the default (`003 §3.3`).

## Palette

The palette is defined in HSV, by the shader's rules, and is not stored anywhere. It has 256 colours:

| Saturation | Hues | Values | Colours |
|---|---|---|---|
| 0 (gray) | 1 | 16, black to white | 16 |
| 1/3 | 6 | 8 above black | 48 |
| 2/3 | 12 | 8 above black | 96 |
| 1 | 12 | 8 above black | 96 |

- Hue differences are hard to see in pale colours, so the palest ring has half the hues. Six hues are the primaries and secondaries; twelve add the colours between.
- A coloured pixel too dark for its lowest value becomes the gray ramp's black.
- Dither is a 2x2 ordered (Bayer) pattern on the low-resolution pixel grid, applied to hue, saturation and value before each is quantised (`003 §3.2`).
- Saturation is quantised first, because the number of hue and value levels depends on it.

## Tools

- **Switches.** `LookSwitches` puts each part of the pipeline on a number key, and the window's title shows their state. The keys are listed in the README.
- **Palette card.** Key 6 draws `game/look/palette_card.gdshader` behind the colour pass, in place of the game: smooth sweeps through HSV, which the colour pass turns into a picture of its palette. The card knows nothing of the palette's rules, so it stays right when they change.
- **The "Look" room.** A room the shape of the screen, to which `LookProps` adds turning shapes that gray boxes don't show: a smooth mirror sphere, a flat-shaded sphere, a wedge, a glowing torus, a pane of glass and a gray ramp.

## Open questions

- Flat or smooth shading for models and tiles. Flat shading comes from a mesh's normals, so it is a rule for modelling and not a pipeline setting. The "Look" room shows both (`003 §4.1`).
- Transparency and refraction were not judged on their own (`003 §4.2`).
- UI is drawn at 320x240 and goes through the colour pass. Whether text is readable that way is untried.
- On a 16:9 monitor the image has black bars at the sides. Whether to fill them, and with what, is undecided.
