# `game/bodycheck/look/`

Relevant decision records:
- [003: Rendering pipeline for the Star Fox SNES look](../../../docs/decision-records/003-snes-3d-look.md)
- [004: Rings around lights in the colour pass](../../../docs/decision-records/004-quantisation-artifacts.md)
- [038: Vertices on a grid, for the jitter of mid-1990s 3D](../../../docs/decision-records/038-vertex-grid.md)

## What it is

How the image is made from what the camera sees, and the tools for judging it: `WorldViewportContainer`, the low-resolution view; `ColorPass` and its shader, `mode_x.gdshader`, with the `Palette` it reads; `AnimationStepper`, which steps animations; `nearest_textures_import.gd`, the post-import script that gives models nearest filtering; and `LookSwitches` and `LookProps`, for judging the look by eye. See [rendering.md](../../../docs/designs/rendering.md) for the pipeline, the palette and the tools.

## What it is not for

Where the camera is, or what is drawn over the image for the player or for debugging.
