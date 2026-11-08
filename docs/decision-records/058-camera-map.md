# 058: A map of where the camera may be

## Context

The camera framing of 042 weighs, each frame, how much of the painted camera boundary each candidate view would show against how far it is from the wanted framing (`042 §6.1`). Tuned in play, it works as Neil expects a camera to work, in open space and against the world's concave boundary (`042 §6.6`). It fails where the player's space is lower than the view, under a thin painted band with a room above: a view through the band costs no more than one that stays below it (`042 §6.3`).

Visibility from the player was decided as the fix (`042 §6.5`), and hard visibility built first (`042 §6.7`). In play it kept the camera in good positions, but made the quick pans worse and left hardly anywhere to look up or down: the problems of the first version, with valid positions defined by the tiles, if not as badly (`042 §6.8`). It's parked on the branch `camera-visibility`.

Neil proposed backing out and trying something very different, from morphological image processing: a map, computed when the world loads, of where the view's centre may be.

Goals:
- Computed once, at load, not each frame.
- Visible: the map can be drawn in the game, for debugging and tuning both the processing and the painting.
- Painting rules that are intuitive and that the drawn map makes easy to check.
- Narrow spaces get a line the view can follow; gaps of 4 or 5 tiles between rooms give a path the view can pan along smoothly.

Anti-goals:
- A native dependency in the build, CI and exports.
- Per-frame cost that grows with the world.

## 1. The map

- [x] 1.1 What it holds (Neil): a binary image, at a resolution finer than the tiles, of the points where the view's centre may be. It starts from `CameraBoundary` inverted: outside the rooms and on painted tiles, 0; everywhere else, 1.
- [x] 1.2 How it's made (Neil):
  1. Scale the image 3 times across and 4 times up and down, for a view of 4:3. The swapped numbers are on purpose: each thinning step then removes a third of a tile across and a quarter up and down, so the steps eat into the space at the view's own aspect.
  2. Thin it a fixed number of steps, which removes the centres whose view would show the other side of a boundary, but leaves a skeleton in narrow areas. Where the view must show some boundary, the skeleton is where it shows the least, evenly on both sides.
  3. Grow and smooth it a few steps, by dilation or closing, so that the view may show the boundary tiles themselves.
- [~] 1.3 Details of 1.2:
  - Upscaling: nearest neighbour first. Smoother upscaling followed by a threshold is an option.
  - Thinning steps: Neil guessed about 40. Claude: the view less its 1-tile margin reaches 9 tiles across and 6.5 up and down from its centre, which is 27 and 26 pixels at 3 by 4, so about 27 steps. Thinning by less than the view could stand in for step 3.
  - Growing and smoothing: Neil meant one that only grows and smooths; he wrote opening, which Claude pointed out would erase the skeleton, since a line 1 pixel thick doesn't survive the erosion. Closing smooths without removing thin lines.
  - Between steps 2 and 3, the image could be scaled across to restore its 1:1 aspect with the tiles, if step 3 should grow it evenly. With nearest neighbour, step 3 would also smooth the jaggies; with smoother scaling, a threshold makes it binary again.
  - Only 4:3 for now. Other aspects need their own scale, so the map would be made for the window's aspect, and remade when it changes.
- [x] 1.4 Written in C#, not with OpenCV (Neil's first proposal, OpenCvSharp). Claude: OpenCV's thinning runs until nothing changes, with no fixed number of steps, so the loop would be written by hand anyway, and the native library would be needed in every build, CI run and export. Erosion and Zhang–Suen thinning are each short, and fast enough at load. Neil agreed. Claude writes it.
- [x] 1.5 The simple version first (Neil): scaling and a fixed number of steps, as in 1.2. If it works well enough, it stays. Claude's alternative, in reserve: find for each pixel how large a view fits centred there, and thin in order of that size down to a threshold. That handles any aspect exactly, with no scaling, and would give the drawn map a gradient.
- [x] 1.6 Pathologies are for painting rules, not processing (Neil). Thinning keeps every connection, so any gap at all in a band gives a strand of skeleton through it: the game takes it that the player should see through it. Claude expects Neil's band of alternating painted and unpainted tiles to give a ladder of strands into the room above. The drawn map makes it clear what happened when a boundary is painted oddly. Processing such as dropping connections narrower than the player is in reserve.
- [ ] 1.7 Later, perhaps: making the map a step of an asset pipeline, at build time rather than at load (Neil). Not yet.

## 2. Seeing it

- [x] 2.1 F1 toggles drawing the map in the game, at Z = -0.5, between the tiles' front and back faces, opaque or partly transparent (Neil). F2 to F5 are taken.
- [x] 2.2 The first step is the map and its drawing alone. `CameraFraming.Frame` is unchanged (Neil).

## 3. Placing the view

- [ ] 3.1 How the view's centre is found from the map, given the player and the wanted framing. Options:
  - (a) Neil: walk pixel by pixel from the player toward the wanted framing until a valid pixel; keep walking until an invalid one, and take the one before it. If none is valid, the player; if the walk ends valid, the wanted framing.
  - (b) Claude: the valid pixel nearest the wanted framing, among those that keep the player in view (`042 §6.4`). Later, perhaps, a soft minimum over valid pixels, the cost of 042 with the map in place of the hidden area, to smooth the jumps where the nearest pixel switches from one part of the map to another. Claude's concern with (a): where the walk misses the map, as for a player above the skeleton of a low room who wants to look up, it returns the player, and the view goes through the band again.
  - Neil: start even simpler, and see what happens.
- [ ] 3.2 Only the player's own area (Claude): thinning never joins areas that weren't connected, so if the view's centre may only be in the connected part of the map that the player is in, the room above a solid band can't win. That gives the regions of `042 §6.5` (b) without painting them.
