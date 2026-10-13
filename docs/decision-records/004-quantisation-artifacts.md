# 004: Rings around lights in the colour pass

## Context

Inciting event (Neil, 2026-10-13): the colour pass of `003 §3.1` leaves rings around light sources and glowing things. Dither covers or spreads most of them, and the palette itself works well.

Goal: a colour pass without the rings, that still chooses hue directly. Neil never saw rings when quantising in linear spaces such as YUV, but those didn't hold hue (`003 §3.1`).

Anti-goals:
- Throwing away the HSV pass while options are compared. It stays in the code until then, to go back to and to compare against.
- `palette_card.gdshader` stays as it is for now. Neil wants to see it under each shader.

Closed on 2026-10-13. The decision is 2.10: a stored palette, the nearest colour in Oklab by search, and dither by an offset. Outcomes and unresolved items are in [rendering.md](../designs/rendering.md).

Items 1.1 to 1.3 are about the HSV shader, which is gone. They were not settled and need no answer.

The shaders named in section 2 were tried and removed, and are not in source control: `mode_x_luma`, `mode_x_palette`, `mode_x_lookup`, `mode_x_partner` and `mode_x_lattice`. Their items say what each did and why it lost. `mode_x_offset` became `mode_x.gdshader`, replacing the HSV shader of `003`, which is in the history. The shader cycling of 3.2, the shared file of 3.3 and the third pair of keys of 3.4 went with them.

How we work on this: Claude writes the shaders, so that options can be tried quickly. The best options stay in the code, with a key to cycle through them. Every shader keeps `uniform bool dither`, because rings are easiest to see with dither off.

A second reason for a stored palette (2.6): one of Neil's children is a pixel artist and is interested in the 2D assets and UI. She needs the palette in hand, not hidden in shader arithmetic.

## 1. Where the rings come from

Claims about `game/look/mode_x.gdshader`, from reading it unless marked measured.

- [~] 1.1 Value is not brightness, so a step of saturation is a step of brightness. At full value, blue's luma falls from 0.69 to 0.38 between saturations of 1/3 and 2/3; yellow's falls from 0.98 to 0.95. Neil sees worse rings around blue, which fits. Not the whole cause: see 1.4.
- [~] 1.2 Saturation is chroma over value, so it is unstable in the dark. A faint tint in a glow's tail swings across saturation levels.
- [ ] 1.3 Hue, saturation and value share one dither threshold, so they step on the same pixels. Untested.
- [x] 1.4 The gray ramp and the coloured ramps disagree about brightness. Gray has 16 levels and colours have 9, and `floor(v * levels) / (levels - 1)` pushes dark values down by a different amount in each: a value of 0.22 is 0.2 as a gray and 0.125 as a colour. A falloff that crosses a saturation of 1/4 therefore jumps. Measured 2026-10-13 on a capture of the yellow lamp (3.1), on one dither phase, going outwards: luma 0.245, 0.122, 0.200, 0.133. The dip to 0.122 is a coloured band inside a gray one. So yellow rings too, and 1.1 can't be the main cause in this scene.

## 2. Options

- [x] 2.1 A: keep HSV, and choose value to match the source's luma after hue and saturation are rounded. Tests 1.1. Built as `mode_x_luma.gdshader`. On the capture of 1.4 it gives the same bands around the yellow lamp, and brightens the bluish tail of the falloff into rings of its own. By eye (Neil, 2026-10-13): it removes an overly bright band around the lamp and around the player, but a bright band around the lamp grows as the player approaches it. Rejected: it keeps the fault of 1.4.
- [x] 2.2 B (not tried; 2.6 was chosen first): hue, absolute chroma (max − min) and luma. Addresses 1.1 and 1.2. The gamut is no longer a box, so codes are wasted or clamped.
- [x] 2.3 C (not tried; 2.6 was chosen first): OkLCh, the polar form of Oklab. As B, with even hue steps and a less regular gamut.
- [x] 2.4 D (not tried; 2.6 was chosen first): Okhsl or Okhsv. A box-shaped gamut, but normalising brings 1.2 back near black.
- [x] 2.5 E (tried in Oklab as 2.11, and rejected): a lattice in a Cartesian space. No singularities and well-behaved dither, but hue drifts along a shaded ramp, as with YUV in `003 §3.1`.
  - YCoCg (Neil): the same family as YUV, exactly invertible and cheap. Its Y weighs blue at 1/4, against 0.07 in luma.
  - IPT (Neil): built so that lines of constant hue are straight. Oklab has the same structure and was fitted to keep that property with better lightness, and it goes straight from linear sRGB.
- [x] 2.6 F: a stored palette, and the nearest palette colour in Oklab, through a 3D lookup texture. Claude's pick and Neil's leaning. It separates which colours exist (polar suits this) from which colour a pixel gets (a Euclidean perceptual space suits this). The palette of `003 §3.1` can be kept exactly.
  - Decision (Neil, 2026-10-13): generate the palette texture at startup for now. When the palette is final it becomes an asset, and the code that generates it becomes the first part of an asset pipeline.
  - Built as `mode_x_palette.gdshader`, with `Palette.cs` generating the palette of `003 §3.1` as a 16x16 image and a second image of the same colours in Oklab.
  - No lookup texture yet: the shader compares each pixel with all 256 colours. That is exact, a change of metric or dither is a change to the shader alone, and it took 2.5 ms of GPU time a frame at 320x240 on Neil's machine. A lookup texture can replace it when the method is settled.
  - Lightness has Ottosson's toe (the L_r of Okhsl). Plain Oklab's cube root puts the darkest colours so far from black that the background, at 5/255, would become the darkest gray.
  - Dither: between the nearest colour and the second nearest. The share of the second is how far along the line between them the source lies in linear RGB, compared with the 2x2 Bayer threshold. It needs no measure of the palette's spacing.
  - Measured 2026-10-13 on a capture of the yellow lamp: the falloff has none of the bright and dark bands of 1.4. Faint bands of hue remain, green and orange around the yellow, where the second nearest colour is of another hue.
  - By eye (Neil, 2026-10-13): clearly the best yet. No apparent value banding, and hues are faithful. Hue banding happens but doesn't bother him. The walls are faintly blue, which is acceptable, and the dark background is closer to its true colour.
  - Decision: the stored palette, Oklab and the search stand. The dither between two colours was replaced by 2.10.
  - Tried by Neil: 9 gray levels in place of 16. With this shader a thick dark gray band far from the yellow lamp became a thick dark orange one. Rejected: lights should fall off to gray, not to false colours. With `mode_x.gdshader` the same change removed one band around the player.
  - The palette against `mode_x.gdshader`: 6 of the 147 colours in a frame from that shader differed from the palette, each by 1 in one channel, at 127 against 128. A channel of exactly 0.5 is 127.5 of 255, and the shader's arithmetic lands on either side of it. The palette rounds every such channel to 128, so it is the one to trust; nothing was changed.
  - Checked 2026-10-13, dither off, one frame: the shader's choice equals the nearest colour computed separately in double precision at all 76,800 pixels. So the Oklab function in the shader and the one in `Palette.cs` agree.
- [x] 2.8 A lookup texture in place of the search (Neil asked whether the shader could avoid converting to Oklab). Built as `mode_x_lookup.gdshader`: a cube of sRGB colours, each texel holding the places in the palette of the nearest and second nearest colours. The shader then does no Oklab arithmetic, reads three texels a pixel, and only `Palette.cs` knows the metric.
  - At 64 to an axis it is built in 0.1 s and takes 1.7 ms of GPU time a frame against 2.5 ms.
  - It is not exact. A texel stands for a block of 4x4x4 colours, so a colour almost as near to one palette colour as to another can go the other way. Measured with dither off: 17,503 of 76,800 pixels differ from the search, nearly all of them the walls, whose colour (68, 74, 91) is 0.038 from a pale blue and 0.040 from a gray. The search gives the blue and the lookup the gray.
  - 128 to an axis takes 0.4 s to build and is still not exact. 256 is exact: 33 MB, and by extrapolation about 6 s to build, so it belongs in the asset pipeline and not at startup.
  - By eye (Neil, 2026-10-13): it shows. The lookup gives thin dark bands that the search doesn't, obvious with dither on. Decision: keep the search and find other ways to make it cheaper. 2.5 ms and 1.7 ms are whole frames, so the search costs about 0.8 ms more than the lookup.
- [x] 2.9 Choosing the colour to dither with. The search of 2.6 dithers the nearest colour with the second nearest, whatever its hue, which is where the bands of hue come from. Built as `mode_x_partner.gdshader`: the search keeps the four nearest colours, and the partner is whichever of the other three makes the mix closest to the source in Oklab, at the share of 2.6. The partner is always among the nearest, so the dither can't become coarse.
  - Measured 2026-10-13 on one frame: the average of each 2x2 block is off the source by 2.93 of 255 a channel, against 3.43 for the second nearest. 8,891 of 76,800 pixels differ between the two. The walls become a dither of pale blue and gray, and the lamp's falloff has less green and orange in it.
  - Cost: no more than the search of 2.6, within the noise of the measurement (2.2 ms a frame against 2.6 ms in the same session).
  - By eye (Neil, 2026-10-13): good in motion, but against the search of 2.6 it adds a dark band around the yellow lamp, plainer once the player has moved. Neil doubts the ordering: the fourth nearest colour can be far from the nearest. A possible cause of the band, unchecked: the partner is chosen for its ideal share, and a share under 1/8 never shows with a 2x2 pattern, so the pixel stays at the nearest colour.
  - Rejected for 2.10, which has no second colour to choose.
  - Behind it, untried: weighting chroma more heavily in the distance; four colours to a 2x2 block (Knoll's pattern dither), at about four times the cost of the search.
- [x] 2.10 Dither by an offset before the search (Neil): add `mode_x.gdshader`'s offset to the source in Oklab, and take the nearest palette colour to the sum. One search, and no second colour to choose. Built as `mode_x_offset.gdshader`.
  - Scale. On a lattice the offsets span one step. The palette is not a lattice, so the span has to be about the distance between neighbouring colours. Measured 2026-10-13 in Oklab: grays are 0.06 to 0.08 apart in lightness, coloured ramps 0.04 to 0.13, and a colour's nearest neighbour is 0.047 away at the median (0.026 and 0.062 at the quartiles). So no one span fits everywhere: too small leaves bands, and too large mixes colours that are far apart.
  - Direction. Lightness, a and b each get the 2x2 pattern, shifted by a pixel for a and by a row for b, so a block's four pixels search around four different points and can land on up to four colours.
  - Two uniforms: `lightness_spread`, starting at 0.07, and `chroma_spread`, starting at 0.035. Keys change them (3.4).
  - Measured on one frame at those settings: 2x2 block averages are off the source by 3.54 of 255 a channel, against 3.43 for 2.6 and 2.93 for 2.9. Everything is dithered, the back wall included, and the walls are bluer. Not judged by eye yet.
  - By eye (Neil, 2026-10-13): lovely. He likes a lightness spread of 0.07 with a chroma spread of 0.015 or less; walls and background are nearer the right hue with less chroma spread.
  - Measured on the same frame, lightness spread 0.07, error of 2x2 block averages against the source as sRGB of 255 a channel, then Oklab lightness, then Oklab chroma. Chroma spread 0.035: 3.54, 0.0059, 0.0118. 0.015: 3.21, 0.0055, 0.0099. 0.010: 3.15, 0.0059, 0.0093. 0.005: 3.03, 0.0060, 0.0099. 0: 3.02, 0.0063, 0.0097. For comparison, 2.6: 3.43, 0.0111, 0.0120, and 2.9: 2.93, 0.0068, 0.0112. One frame of a dark, mostly gray room.
  - Decision (Neil, 2026-10-13): a chroma spread of 0.015, with the lightness spread at 0.07.
  - Decision (Neil and Claude, 2026-10-13): this is the colour pass. It fixes the value banding that began the record, it does one search with no second colour to pick, which is where 2.8 and 2.9 went wrong, and Neil judged it the best in motion: the only one that would have made him ask how it was done with 256 colours.
  - If one span can't be made to fit: scale the offset by the spacing around the nearest colour, which needs a second search, or a coarse texture of spacing.
- [x] 2.11 A lattice in Oklab (Neil): round lightness, a and b each to evenly spaced levels, with no stored palette. This is E of 2.5, which Neil feels he didn't give enough attention. If an allocation of levels looks good, a palette for art can be generated from it later. The palette shaders stay in the cycle. Built as `mode_x_lattice.gdshader`.
  - Levels. Lightness has levels from 0 to 1. The levels of a and b are spaced evenly about 0, to 0.28 and 0.32 either side, so an odd number keeps gray. Keys change the three numbers (3.4).
  - Dither. The offsets of 2.10, spanning exactly one step on each axis, which is what they were made for.
  - Colours that sRGB doesn't have. Most combinations of levels are outside sRGB. Those move towards the gray of the same lightness, which keeps hue, until they are inside.
  - How many colours. Counted 2026-10-13 over a 40x40x40 grid of sRGB colours, as lightness x a x b, the number of combinations reached without dither and with it: 16x5x5, 124 and 192. 12x7x7, 167 and 241. 10x7x9, 169 and 238. 8x9x9, 172 and 250. 16x7x7, 220 and 303. 12x9x9, 249 and 347. The shader starts at 12x7x7.
  - Measured on the same frame at 12x7x7: 97 colours in the frame, 12 exact grays, and 2x2 block averages off the source by 3.80 of 255 a channel, against 3.21 for 2.10. The gray ramp is clean. The yellow lamp and the cyan player come out as dithers of paler colours, because the lattice has no colour that vivid at that lightness, and the lamp's falloff is speckled with teal and orange.
  - By eye (Neil, 2026-10-13): rejected. No allocation with a reasonable number of levels gave an accurate impression of the true colours. Odd banding appears with 7 or more levels of b, and stays until the other two are raised a good deal; its cause was not looked into, since fixing it would not reduce the levels needed. Searching a palette minimises squared error, which is hard to beat.
- [x] 2.7 Linear RGB. Decision (Neil, 2026-10-13): convert in the shader. HDR 2D might be fine, since there are no other shaders or UI yet, but it waits. Oklab and IPT start from linear RGB, and the colour pass reads sRGB.
  - Convert in the shader. A few lines, and nothing else changes.
  - Turn on the project's HDR 2D setting. The screen texture is then linear, and the shader must write linear; the engine converts to sRGB when it copies to the screen. It changes what every 2D shader and the UI see.

## 3. Tools

- [x] 3.1 A pure yellow lamp in the "Look" room (Neil), to test 1.1 before building 2.1: an emissive ball with a light inside it, out of reach of the torus's light.
- [x] 3.2 Cycling shaders. Decision: `ColorPass` lists the shaders, and the minus key switches to the next. The window's title names the one in use.
- [x] 3.3 Shared shader code is in `game/look/color_pass.gdshaderinc`.
- [x] 3.4 Keys for a shader's own settings, down and up: `[` and `]` for the first, `;` and `'` for the second, K and L for the third. The offset shader has a lightness spread and a chroma spread, and the lattice shader has levels of lightness, a and b. The window's title shows them.
