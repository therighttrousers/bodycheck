# 010: Camera framing

## Context

Inciting event (2026-10-17): issue #33 put Phantom Camera in the game, following the player, centred, at the distance where a tile's front face is 16 pixels at 320x240. Neil played it, and judged centring a placeholder: the camera won't keep the player at the middle of the screen. He then played Ori for how a camera works well in a large, seamless 2D world.

What Neil saw in Ori (2026-10-17):
- The camera lags behind Ori, most obviously when Ori moves very quickly.
- Ori is usually near the bottom of the screen, around the 3/4 line. In some places, notably underwater and in platforming challenges, Ori is centred vertically.
- Ori is usually centred horizontally. In some places, notably on ledges in front of platforming challenges, Ori is to one side of the screen, around the 5/6 line. Ours may need 3/4, because of the aspect ratio.
- Different areas have different camera distances, depending on the geometry around them.
- The camera shows only the playable area, with very little drawn out of bounds.

What was there: one `PhantomCamera3D` in Simple follow mode, following the player's body. Phantom Camera's limits, which keep a camera inside a rectangle, are for its 2D camera only: the 3D camera has no limit properties at all. Its 3D camera does have framed follow, with a dead zone.

Goal: a camera that feels good in a seamless world. Anti-goal: building per-area framing before there are areas.

## 1. Following

- [x] 1.1 Lag. Decision (Neil): Phantom Camera's follow damping, 0.2 in X and Y. At the player's top speed the camera trails by about 2 tiles.
- [x] 1.2 Vertical position. Decision (Neil): the view's centre is 3 tiles above the player's centre, which puts the player 70% of the way down the screen. 4 tiles, about 77%, near Ori's 3/4 line, was tried first; Neil settled on 3 after playing.
- [x] 1.3 Looking up and down (issue #36). Decisions (Neil, 2026-10-17):
  - Keys: up and down, on the same inputs as moving: arrows, WASD, the left stick and the d-pad. The player needn't stand still, since there's no reason for the restriction.
  - Looking moves the view's centre 5 tiles up or down, at once. The camera's damping (§1.1) smooths it, and the speed that gives is right.
  - The look offset is added before the clamp (§2.1), so looking never shows outside the room. Near a room's floor or ceiling, looking that way does little or nothing.
  - The left stick looks partway when it's pushed partway; the keys and the d-pad look the whole 5 tiles.
  - Not decided: up and down will likely mean other things later, such as doors, climbing or aiming. A common answer is a short hold before the view moves.

## 2. Showing only the playable area

- [x] 2.1 How to keep the view inside an area, when Phantom Camera's 3D camera has no limits. Options:
  - Clamp the `Camera3D` after the host moves it. The host writes the camera's transform every frame, so this fights it.
  - Clamp what the phantom camera follows. Decision: `CameraRig` moves a target each frame to where the view should be centred, 3 tiles above the player, clamped so that the view at the tiles' front faces stays inside the area. The phantom camera follows the target, and its damping eases the camera into the clamp. When the area is smaller than the view in a direction, the view is centred on it in that direction.
  - The target moves in `_Process`, to the player's interpolated position, and isn't interpolated itself. Phantom Camera moves in `_physics_process` only when it follows a physics body; for a plain node it moves each frame. Measured on the player probe in "Seams": the camera moves smoothly.
- [x] 2.2 The area, for now: the room's tile grid. A 20x15 room, such as `Level_0`, fills the view, so the camera doesn't move in it.
- [ ] 2.3 The area in the seamless world. Leaning (Claude): the LDtk level the player is in, with the camera easing across when the player moves into the next one. A 20x15 level then never scrolls, and a bigger one scrolls within itself. It gives the level designer control, and matches Ori's camera showing only the playable area. Undecided (Neil).

## 3. Framing by area

- [ ] 3.1 Centring underwater and in platforming challenges, a sideways offset on ledges in front of challenges, and camera distances that suit the geometry. Phantom Camera's way to do this: a phantom camera per area, with priorities, made active by trigger areas, with tweens between them. The clamp of §2 would apply to each. Put off until there are areas.
