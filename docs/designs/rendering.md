# Rendering

Relevant decision records:
- [000: Initial design](../decision-records/000-initial-design.md)
- [003: Rendering pipeline for the Star Fox SNES look](../decision-records/003-snes-3d-look.md)
- [004: Rings around lights in the colour pass](../decision-records/004-quantisation-artifacts.md)
- [008: Telemetry, and a debug overlay drawn at the window's resolution](../decision-records/008-telemetry-and-overlay.md)
- [009: The first level loader](../decision-records/009-level-loader.md)
- [010: Camera framing](../decision-records/010-camera-framing.md)
- [011: Stitching LDtk levels into one world](../decision-records/011-stitched-levels.md)
- [012: Subsystems in `BodyCheck.Core`](../decision-records/012-core-subsystems.md)
- [014: Less disruptive looking](../decision-records/014-less-disruptive-looking.md)
- [015: The phase ability](../decision-records/015-phase.md)
- [024: Playtesting feedback](../decision-records/024-playtesting-feedback.md)
- [038: Vertices on a grid, for the jitter of mid-1990s 3D](../decision-records/038-vertex-grid.md)
- [039: Pausing for the menu and the map](../decision-records/039-modes-kept-across-pauses.md)
- [044: Dialogue](../decision-records/044-dialogue.md)
- [050: The cutscene mode](../decision-records/050-cutscene-mode.md)
- [040: No vertex snap](../decision-records/040-no-vertex-snap.md)
- [042: The camera clamped by tiles](../decision-records/042-camera-clamped-by-tiles.md)

The image is Mode X with modern lighting: 320x240 and 256 colours, as on a VGA card in the mid-1990s, drawn with shadows, ambient occlusion, reflections and bloom. Star Fox, Quake and Descent are the references for the geometry, and Mode X for the image (`003`).

## Pipeline

1. **Low resolution.** `WorldViewportContainer` draws the game into a viewport of its own, 320x240 (`003 §1.1`, `§2.1`). The window's own viewport is the window's size, so what is drawn there, outside the game's viewport, is at full resolution and doesn't go through the colour pass, such as the debug overlay (`008 §2.3`).
2. **Lighting.** Shadows, ambient occlusion, screen-space reflections and glow, which is the engine's name for bloom. All were chosen by eye at the target size (`003 §4.2`). There is no anti-aliasing (`038 §1.3`).
3. **Colour pass.** `ColorPass` covers the screen with a rectangle whose shader, `game/look/mode_x.gdshader`, reads what is behind it and writes each pixel back as a colour of the palette, with dither (`003 §3.3`, `004 §2.10`).
4. **Scaling.** `WorldViewportContainer` scales the image to the window by the largest whole number that fits, with nearest-neighbour sampling, and fills the rest with black bars. The window starts at 1280x960, which is 4x.

Besides the world, the game's viewport draws these, which are scaled with it, all but the letterbox placeholders (`039 §2.1`, `§2.3`):

- `PhaseAimMarker`, the aim of a phase, is drawn over the world and under the colour pass, so it is dithered as part of the game's image. Its lines are sized in pixels at 320x240.
- `Letterbox`, black bars across the top and bottom, which say that the player's control is taken away, is a canvas layer under the colour pass, so the bars are part of the game's image, under every overlay, in the palette's darkest colour. Each is 24 pixels high at 320x240, and they slide in or out in whole pixels over a quarter of a second of real time. They show in a cutscene whose holds ask for them, such as a script's that drives the player, and not in a dialogue's (`051 §3.6`).
- `PauseMenuOverlay`, the menu, is a canvas layer above the colour pass, so the dither leaves it alone, and above the aim marker. It is laid out at the viewport's size and centred, so with low resolution off it is small but still centred.
- `DialogueBox`, the text box, is a canvas layer above the colour pass, along the bottom edge of the viewport, in a pixel font at 10 pixels, aliased, laid out from the font's metrics (see [dialogue.md](dialogue.md#the-text-box)).

`WorldViewportContainer` pauses with the game, and so does everything in the game's viewport that inherits its process mode; what must run while paused, such as the camera, says so itself ([`game/main/`](../../game/main/architecture.md#the-games-modes)).

The colour pass reads tone-mapped sRGB values, and its output reaches the screen unconverted. This holds while the project's HDR 2D setting is off, which is the default (`003 §3.3`).

## Scale

A tile's face is 16x16 pixels at 320x240, so a screen is 20x15 tiles. LDtk levels may be any size in whole tiles (`024 §4.1`). `Constants.TilePixels` holds the 16 (`009 §5.1`).

The camera is at the distance where a tile's front face is 16 pixels, whatever the size of the room (`009 §5.2`). At the window's own resolution, which a switch gives, the view is the same 15 tiles high, so it shows the same framing.

The camera follows the player with a lag, and its view is centred 3 tiles above the player, which puts the player 70% of the way down the screen (`010 §1`). Up and down, on the same inputs as moving, look up or down, but only while they are the only input: no moving, no jumping and no phase held (`010 §1.3`, `014 §1.1`, `015 §5.15`). After a hold of 0.5 seconds the view eases over 1 second to where the player's centre is 5.625 tiles from the view's, on the 1/8 or 7/8 line, partway for the left stick partway pushed; when any other input starts, it goes straight back, as fast as damping allows (`014 §1.2`, `§1.3`). Since this measures from the player and not from the 3-tile lift, looking up does little for now. It shows only the room: the view's centre is clamped so that the view at the tiles' front faces stays inside the room, and is centred on the room where the room is smaller than the view (`010 §2.1`, `§2.2`). A 20x15 room fills the view, so the camera doesn't move in it.

## Textures

Vertices aren't snapped to whole pixels. A snap was tried and removed: it was barely noticeable at 320x240, and it needed every material replaced at runtime by a shader that reimplemented it (`038 §1.1`, `§1.2`, `040 §1.1`).

Imported models get nearest filtering from `nearest_textures_import.gd`, a post-import script, since smoothed textures took hardware that the games of the look didn't have. It's the default for new models, in `project.godot`. It reaches only materials embedded in a model: one saved in a file of its own, such as the platform's `body.tres`, has to set nearest filtering itself, and the import warns if it doesn't.

## Animation steps

`AnimationStepper` advances every animation 10 times a second of real time, so that slow motion doesn't make animations choppier, by the game time that has passed (`038 §4.1`). See [models.md](models.md#animation).

## Palette

The palette has 256 colours. `Palette.cs` generates it when the game starts, as an image in sRGB (`004 §2.6`). It is defined in HSV (`003 §3.1`, `004 §4.6`):

| Saturation | Hues | Values | Colours |
|---|---|---|---|
| 0 (gray) | 1 | 16, black to white, evenly spaced | 16 |
| 0.19 | 6 | 8 | 48 |
| 0.54 | 12 | 8 | 96 |
| 1 | 12 | 8 | 96 |

- Hue differences are hard to see in pale colours, so the palest ring has half the hues. Six hues are the primaries and secondaries; twelve add the colours between.
- The saturations are 1/3, 2/3 and 1, each raised to the power 1.5, which makes colours of different saturations easier to tell apart.
- A coloured ramp's values are steps 2 to 9 of 9 evenly spaced lightnesses, by CIE L*, each turned into the luminance that has it: 0.036, 0.077, 0.14, 0.23, 0.36, 0.53, 0.74 and 1. Most of a ramp is dark. With evenly spaced values, dithered dark areas came out too bright (`004 §4.3`, `§4.4`).
- The image is 8 wide and 32 high. The top two rows are the grays. Each row below is one ramp, darkest first, so a row has one hue and one saturation and is easy to judge by eye.

## Colour pass

Each pixel becomes the palette colour nearest to it (`004 §2.6`, `§2.10`).

- **Distance** is Euclidean in Oklab, with Ottosson's toe on lightness (the L_r of Okhsl). Plain Oklab puts the darkest colours too far from black.
- **Conversion.** The shader converts each pixel from sRGB to linear RGB to Oklab (`004 §2.7`). `Palette.cs` makes a second image of the palette in Oklab, so the shader converts only the pixel. The Oklab function exists in both places, and the two have to agree.
- **Search.** The shader compares the pixel with all 256 colours. A lookup texture in place of the search was tried and gave visible bands (`004 §2.8`).
- **Dither** is an offset added to the pixel's colour in Oklab before the search. Lightness, a and b each get a 2x2 ordered (Bayer) pattern on the low-resolution pixel grid (`003 §3.2`), shifted by a pixel for a and by a row for b, so the four pixels of a block search around four different points.
- **Spread.** The offsets span about the distance between neighbouring colours: less leaves bands, and more mixes colours that are far apart. Along lightness, `Palette.LightnessSpread` works it out from the palette: three quarters of the mean step of lightness between a colour and its neighbour up or down the lightness axis, which is 0.07 for this palette (`004 §4.5`). Along each of a and b it is 0.015, chosen by eye.

Other ways of choosing colours were tried and set aside: quantising in HSV, a lattice in Oklab, and two ways of picking a second colour to dither with (`004 §2`).

## Tools

- **Switches.** `LookSwitches` puts each part of the pipeline on a number key, and the window's title shows their state. The keys are listed in the README.
- **Dither spread.** 7 and 8 change the lightness spread, and 9 and 0 the chroma spread (`004 §3.4`, `024 §5.2`). The window's title shows both.
- **Palette card.** Key 6 draws the palette's image behind the colour pass, in place of the game, stretched to fill the screen (`004 §4.1`).
- **The "Look" room.** A room the shape of the screen, to which `LookProps` adds turning shapes that gray boxes don't show: a smooth mirror sphere, a flat-shaded sphere, a wedge, a glowing torus, a pane of glass, a gray ramp and a pure yellow lamp, whose falloff is where bands and false colours show first (`004 §3.1`).

## Open questions

- Flat or smooth shading for models and tiles. Flat shading comes from a mesh's normals, so it is a rule for modelling and not a pipeline setting. The "Look" room shows both (`003 §4.1`).
- Transparency and refraction were not judged on their own (`003 §4.2`).
- The resolution of game UI. It stays out of the colour pass, whose dither changes even colours taken from the palette. The placeholder menu is drawn at 320x240 on a layer above the colour pass, in the game's viewport, with the default font antialiased; aliased, it looked terrible (`039 §2.1`, `§2.2`). Whether all game UI is drawn this way, or at the window's resolution, is undecided. Text in system fonts at 8 pixels is unreadable at 320x240; a 3x5 pixel font is readable (`008 §2.1`, `§2.6`). The dialogue box is drawn at 320x240 in a pixel font, Fusion Pixel at 10 pixels, aliased (`044 §4.1`).
- What the aim of a phase looks like. Leaning (Claude): part of the game's image, as Ori's bash arrow is part of its world, whether as a 3D model, a billboard or 2D sprites (`015 §5.7`, `039 §2.3`).
- Framing by area: centring underwater and in platforming challenges, the player to one side on ledges in front of challenges, and camera distances that suit the geometry. Phantom Camera's way: a phantom camera per area, made active by trigger areas. Put off until there are areas (`010 §3.1`).
- Looking when up and down mean something else too, such as doors, climbing or aiming. Any input that controls the player has to stop a look (`014 §1.1`).
- Looking with the left stick. Each axis has its own dead zone of 0.2, so a stick pushed fully up still looks up to about 11.5° either side of vertical; past that, the player moves and the look stops. Many games give up a wedge of ±22.5°. Not judged on a gamepad (`014 §1.1`).
- What the camera keeps inside in the seamless world. For now, the world's bounding box (`011 §4.1`). Tried and parked: clamps by the static tiles, the last by how hidden each tile is from the player. Next to try: outside tiles that designers paint in LDtk, which the view avoids showing (`042 §5.3`).
- When should the camera leave the 16-pixel distance? Leaning: zooming out for cinematics, or to show something important that is hard to see at the usual distance. Contextual zoom may turn out better after trying both (`009 §5.2`).
- On a 16:9 monitor the image has black bars at the sides. Whether to fill them, and with what, is undecided.
- Shader time. A shader's `TIME` follows the engine's time scale but not the tree's pause, so the slowdown before a phase would slow shader animations, and the release would snap them back. The first animated shader gets a time uniform the game advances itself; which clock it runs on is open (`050 §3.1`, `§3.2`).
- The palette as an asset. It is generated at startup until it is final; then it becomes a file, and the code that generates it becomes the first part of an asset pipeline (`004 §2.6`).
- A smaller palette (Neil, 2026-10-13, thinking aloud). It would suit pixel art drawn by hand: 256 colours is a lot to draw with. A palette of 128, with 8 grays and 4 values to a ramp, was tried: it saves about 0.4 ms a frame and costs nearly three times the lightness error (`004 §4.3`, `§4.4`). Alternatives: a reduced palette for pixel art only, or high-fidelity drawings scaled down, with interpolation, before the colour pass.
- The cost of the search. A frame of the "Look" room takes about 2.0 ms of GPU time at 320x240 with it, about 0.4 ms of which is the search, and the work grows with the number of pixels, so it is about 16 times as much with low resolution off. Untried: a coarse texture that lists the few colours that can be nearest in each part of the colour cube, with the exact search over those.
- Spreads that fit the palette everywhere. The palette's colours are unevenly spaced, so one lightness spread is a compromise, and the rule that works it out was fitted to two choices made by eye (`004 §4.5`). The chroma spread is still set by hand. Untried: scaling the offset by the spacing around the nearest colour (`004 §2.10`).
- HDR 2D. With it on, the colour pass would read linear RGB and skip a conversion, but every 2D shader and the UI would see linear values too (`004 §2.7`).
