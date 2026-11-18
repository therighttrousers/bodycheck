# `game/bodycheck/environments/`

Relevant decision records:
- [061: Environments per room](../../../docs/decision-records/061-room-environments.md)
- [062: Background layers](../../../docs/decision-records/062-background-layers.md)

## What it is

`EnvironmentMixer`, which draws a world's environments, and `BackdropLayer`, a layer of an environment's backdrop. See [rendering.md](../../../docs/designs/rendering.md#environments) for what an environment is.

## What it is not for

The environments' scenes, which are content, in `game/assets/environments/`, or what each room of an environment is decorated with.

## The mixer

`EnvironmentMixer` has each environment a world's rooms use, and blends them by where the camera is, so that the look changes smoothly across rooms' edges, and going back undoes it exactly (`061 §3.1`). It gets each environment's scene from the asset library, and leaves its `Decorations` where it is, since it only holds data.

- **Weights.** Each frame, from the camera's position in the world's plane, the view's centre (`061 §3.2`), each room near it weighs `clamp(0.5 - d / w, 0, 1)`, for its signed distance `d` from the world's `RoomsNear` and its own blend's width `w`, its `EnvironmentBlend`, so that two rooms that meet weigh a half each on their edge, whatever their widths. A room whose width is 0 weighs 1 inside it, 0 outside, and a half on its edge. An environment weighs what its rooms do, scaled so that the weights add up to 1. With no room within half its width, the nearest room's environment is the look (`061 §3.3`, `§3.4`).
- **Lights.** Each environment's scene stays in the tree, without its `WorldEnvironment`, and each light in its `Lighting` has its energy scaled by the environment's weight. A scene with no weight is hidden (`061 §4.1`).
- **The environment.** The mixer has the only `WorldEnvironment`, with an `Environment` of its own, copied from the first, and a `ProceduralSkyMaterial` of its own as its sky. It blends both property by property, for the properties in which the environments differ: floats, colours and vectors by their weights, and everything else from the heaviest. It sets them only when the weights change (`061 §4.2`).
- **Backdrops.** An environment scene's `Backdrop`, if it has one, is moved, before it enters the tree, to the centre of the box around the environment's rooms (`062 §2.13`). Every `GeometryInstance3D` under an environment scene's `Backdrop`, if it has one, has its `Transparency` set to 1 less the environment's weight, so that it fades by any material (`062 §2.4`). Each layer is a `BackdropLayer`, which keeps itself in view; see [rendering.md](../../../docs/designs/rendering.md#environments).
- It processes while the tree is paused, as the camera does, so that the map's zoom blends too, and so do the backdrop's layers, which inherit it.

## Open questions

- The default width of an environment's blend, 6 tiles, which every level has for now, was chosen without play; to tune by eye (`061 §3.3`).
