# 014: Less disruptive looking

## Context

Inciting event (2026-10-18): issue #56, "Make panning less disruptive". Work on the phase ability showed that looking up and down (`010 §1.3`) moves the camera while up and down are used to control the player, such as setting up and using the phase ability. `010 §1.3` left this open: "A common answer is a short hold before the view moves."

What was there: up and down moved the view's centre 5 tiles from where it is without looking, 3 tiles above the player (`010 §1.2`), at once, whatever else the user was pressing. The camera's damping smoothed the move.

Goals, from the issue:
- Most important: no panning while the user gives any other input that controls the player.
- A short hold before panning starts, so that an accidental press of up or down doesn't pan.
- Slower panning, so that it disrupts less when the other guards aren't enough.
- When the conditions stop holding, panning stops at once, and the camera returns as fast as damping allows.
- Fully panned, the player is on the 1/8 line (looking down) or the 7/8 line (looking up).

## 1. When and how the view moves

- [x] 1.1 When the player can look. Decision (Neil): only while up or down is the only input: no horizontal input and no jump. `PlayerInput.CanLook` says so, so that the record that gains a new input is the one that decides whether it stops a look. Still never while the tree is paused.
  - Not judged: on the left stick. Each of the input actions has a dead zone of 0.2 on its own axis, so a stick pushed fully up gives no horizontal input up to about 11.5° either side of vertical; past that, the player moves and the look stops.
- [x] 1.2 Timing. Decision (Neil): a hold of 0.5 seconds, then the view moves over 1 second, eased in (quadratic), with the camera's damping on top. Neil's judgement after playing: easing in helps keep a press that wasn't meant to look from disrupting. When `CanLook` stops holding, the view's centre goes back to 3 tiles above the player at once, and the hold starts again on the next look.
- [x] 1.3 How far. Decision (Neil): fully panned, the view's centre is three quarters of the view's half-height from the player's centre, 5.625 tiles, which puts the player on the 1/8 or 7/8 line. This measures from the player, not from the 3-tile lift: looking up raises the view only 2.625 tiles, and looking down lowers it 8.625. Looking up does more when the lift changes by area (`010 §3.1`). The left stick pushed partway still looks partway.

## 2. Timing the look

- [x] 2.1 How the rig times the hold and the ease. Options:
  - A. Count frames. Tried first; rejected, because `CameraRig` moves the target in `_Process`, so the hold and the pan took less time at higher frame rates.
  - B. Add up each frame's delta. Correct, but every path through the code has to add to it or reset it.
  - C. Store the time the look started and compare it with the clock. Tried (Neil): it doesn't depend on every path adding up deltas, a habit from Quake 3 modding. But the wall clock ignores `Engine.TimeScale`, which the camera's damping follows, and it keeps running at a debugger's breakpoint.
  - D. A Godot `Tween` that eases a look amount from 0 to 1 after a delay, killed when the look stops. Decision (Neil): it says what it does, with no time-tracking code or easing maths to read, the delay, duration, transition and ease are its parameters, and it runs on the process delta, as damping does. In play it can't be told apart from C.
  - E. A second phantom camera, offset up or down, given priority after the hold, so that the host tweens to it with the tween and ease set for that pair by a `PhantomCameraTweenDirector`. Rejected for now: a look would be all or nothing, unless the second camera's offset is also scaled by the stick; the host tweens from where the camera was when the switch happened, so the camera trails a player who moves during the tween; and it isn't less code, since it still needs the hold's timer and a second clamped target. Worth another look when framing by area brings a phantom camera per area anyway (`010 §3.1`).
