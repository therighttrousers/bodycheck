# 060: The camera's height through a jump

Status: closed. The slack (§2.4) and the landing's time constant (§2.6) are in [rendering.md](../designs/rendering.md)'s open questions, for tuning in play.

## Context

Issue #195's third camera feature: hold the wanted framing's height through a jump. The camera's view is centred 3 tiles above the player (`010 §1`), and the framing's spring, with its 0.15-second smoothing time, follows every jump up and down, so the view bobs with every hop. The issue asked how long to hold the height (until landing? until enough time passes?), and noted that the hold would have to be cancelled if the player moves too far vertically.

Before the hold, the look was refactored, so that each of the camera's offsets (the look, the lead of `059`, and now the hold) is stepped by a method of its own and the rig only adds them up.

Goals:
- An ordinary jump doesn't move the view.
- The view still shows where a falling player will land.
- No snapping when the hold ends.

## 1. Timing the look

- [x] 1.1 How the rig times the look, again. `014 §2.1` chose a `Tween` (D) over adding up each frame's delta (B), because with B every path through the code has to add to the time or reset it. Decision (Neil): B, in a method of its own, `StepLook`. With the lead and the hold stepped by methods of their own, a tween is the one offset whose state is spread across the class, created in one place, read through a callback and killed in another. In one method, there is one path that adds and one that resets, which answers B's drawback. The time is real time, as the tween's was, ignoring the time scale (`030 §3.1`): the frame's delta divided by `Engine.TimeScale`, which is never 0 while playing, the only mode the player looks in. The ease is the tween's, quadratic in, after the same delay.

## 2. Holding the height

- [x] 2.1 What holds. Decision: an anchor, the height the view is lifted from. On the ground it is where the player stands; in the air it stays where it was while the player is in range of it. The issue's question of how long has the answer "until landing", with 2.2 and 2.3 as the cancel.
- [x] 2.2 Rising. Options: a time limit; a distance, past which the hold is cancelled; a distance, past which the view follows, with the player kept that far above the anchor. Decision: the last, so that it is continuous at the edge and there's nothing to cancel. Claude built it first as a clamp of the height, leaving the anchor where it was: the height was the anchor kept between the player's height and the slack below it. Neil simplified it: the clamp pushes the anchor itself along, which is then the height. Neil prefers how it looks. After a very high jump, the view moves up as the player leaves the range and down only at the end of the fall, rather than as soon as the player is back in range of the old anchor, so the movement is spread out and the height's path is smoother; and on landing higher, the anchor has less far to go.
- [x] 2.3 Falling. Decision: below the anchor, the anchor is pushed down with the player, so a fall off a ledge follows at once and shows where the player will land. It's the same clamp's other edge. The framing's box gives only 3.5 tiles below the anchor before it pushes the view anyway.
- [~] 2.4 The slack. The full jump is almost exactly 4.5 tiles high, by the player's tuning; jumps from platforms moving up go higher, so Neil wanted a little more. Claude: twice the lift, 6 tiles. At rest the lift puts the player 3 tiles below the view's centre; held, the player rises to at most 3 tiles above it, so the framing stays the rest framing mirrored, with at least 4.5 tiles above the player's centre, as there are below it at rest. Leaning: twice the lift, derived, not a feature of its own. To check in play.
- [x] 2.5 The lift as a feature. Decision (Neil): since the slack follows from it, the lift moves from `CameraRig` into `Features` as `CameraLift`, so that a configuration tunes both with one knob. The lift isn't derived from the jump height: it comes from the framing (`010 §1`), not from movement, and that the two are close is a coincidence.
- [~] 2.6 Landing higher. The anchor steps to the new height on landing, and the framing's 0.15-second spring would whip the view up a ledge. Claude built a critically damped spring of the anchor toward the player's height on the ground. Neil: the anchor is never above the player, so only landing higher needs easing; stepping down, the anchor is put at the player's height. One-sided, he switched to exponential smoothing, with time constant `CameraLandTime`, 0.25 game seconds. Leaning: that, to tune in play.
- [x] 2.7 With the look. The hold and the look are independent in state, and meet in one formula: the wanted height eases from the lifted anchor toward the player's height plus the look's offset, by the look amount. A look still measures from the player (`014 §1.3`). They differ only in the air, where looking is rare.
- [x] 2.8 Which modes. The anchor follows the world, not input, so it steps in every mode: paused, the player doesn't move, and it only settles. Anything the player stands on counts as ground, a robot's head included, so bouncing off one moves the anchor up.
