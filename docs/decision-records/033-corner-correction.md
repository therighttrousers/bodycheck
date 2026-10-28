# 033: Corner correction

## Context

Inciting event (2026-10-27): issue #106, try adding corner correction. Neil pointed at Celeste's player code, whose upward correction is the model: when Madeline rises into a `Solid`, it tries shifts of 1 to 4 pixels sideways, each 1 pixel up, takes the first that is clear, and returns before her vertical speed is zeroed. It shifts only the way she moves horizontally, or either way, left first, when she doesn't, and it applies to any rise, not only jumps.

Neil: don't overindex on Celeste. We have no one-way platforms, and with phase might never have them, and we may want things Celeste doesn't.

Goals (Neil):
- Upward corner correction: a player that rises into a corner it clips by a smidgen slides round it instead of bumping.
- Self-contained, like `RunAction`.
- Generalizable to a sideways case, such as running into the top corner of a step, with its own threshold.
- Simple. Where it conflicts with generalizable, simple wins.

Scale: Celeste's tiles are 8 pixels and Madeline is 8 wide, so she needs correction to enter a shaft 1 tile wide at all; with 4 pixels, she can be up to half a tile off center. Our player is 0.8 tiles wide, so it has 0.2 tiles of slack in such a shaft already, and a correction of `c` makes the window 0.2 + 2c wide. Celeste's window, 1 tile, is `c` = 0.4 for us. Jump speeds per tick are about the same: 0.22 tiles for Madeline, 0.21 for our player.

## 1. How it works

- [x] 1.1 Predict the bump, before the engine's step, not after it (Claude). In Jolt, a bump is solved inside the step: by the time a ceiling contact is reported, the vertical speed is gone, the impulse has gone into what was hit, and the solver may have moved the player. Godot offers no hook between finding a contact and solving it. So the controller casts the player's box along the motion it expects in this tick's step, as of the engine's last step, and nudges the player before the step if it would bump. Nothing is lost, so nothing needs restoring: velocity, thrust and the rest go on as they were. Decision (Neil): accepted, by asking for the implementation.
- [x] 1.2 The nudge is found by trying sizes a pixel apart, 1/16 of a tile, smallest first, every allowed side at each size, as Celeste does (Claude). An exact nudge from the obstacles' edges is possible, by casting a thin box back toward the player, but it is more to read, and the pixel search works the same for any motion and side. Simple wins (Neil, §Context). The step became a quarter of a pixel (§4.2).
- [x] 1.3 All or nothing (Claude). A nudge is taken only if both the nudge and the motion after it are clear, so the move needs no sliding and can't end early. If nothing clears, the player bumps as before. Something that moves into the way during the tick is pushed apart by the engine, as when any two bodies meet.
- [x] 1.4 Only tiles trigger it, and only when a tile is the first thing the cast would meet (Claude). Bumping a body can be meant, as in knocking a block or catching it on the head, and a block carried on the head is met first, so a loaded player gets no correction. Platforms may be added later.
- [x] 1.5 Everything the player collides with can stop it: the nudge and the motion after it must be clear of tiles and bodies, or the engine would push the player out of a body it was shoved into (Claude).
- [x] 1.6 The nudge is drawn: the player keeps physics interpolation, so it is drawn sliding over the tick instead of jumping (Neil). `Entity.MoveTo` takes whether to interpolate.
- [x] 1.7 Corrections may chain, one per tick, under a staggered ceiling (Neil: okay).
- [x] 1.8 It is a static action in the controllers, `CornerCorrection.Correct`, like `RunAction.Run`. It takes the entity, the motion, the sides it may nudge to, and the furthest nudge. Upward correction passes the vertical motion and sideways sides; a sideways correction would pass the horizontal motion and up.

## 2. Upward correction for the player

- [x] 2.1 It applies to any rise in the air, not only a jump, as in Celeste: a throw from a phase rising into a corner gets it too. Not on the ground, so that a rising platform pressing the player into a ceiling doesn't slide it. Superseded by §4.3: on the ground too.
- [x] 2.2 The motion is the vertical part of the velocity, as of the engine's last step, times the tick's game time. Gravity makes it a slight overestimate, which only nudges a tick early. Superseded by §4.3, for the velocity: it includes this tick's jump impulse. The horizontal part is left out: moving away from a corner, as the sides of §2.3 require, the cast from the start of the tick sees the clip as at least as large as it will be.
- [~] 2.3 Which side (Claude): the way the player drives, or either way while it is idle. Celeste uses the sign of horizontal speed, which drag here never brings exactly to 0, so speed would need a dead band, a new value to tune. Input says what the player means and needs no new value. Against it: a player drifting right with nothing held can be nudged left, against its momentum, by up to the correction. To be confirmed by Neil, and checked by playing. Superseded by §4.4: by the velocity's angle.
- [~] 2.4 The furthest nudge is a feature, `PlayerUpwardCornerCorrection`, 0.25 tiles to start: 4 pixels, as in Celeste, but at our 16 pixels a tile. Celeste's window would be 0.4. To be tuned by playing.
- [x] 2.5 The design's open question, jumping up through a 1-tile gap without perfect alignment, is answered: `PlayerControllerTest` jumps into a shaft 0.25 tiles off center, which clips its mouth by 0.15 and enters, and 0.5 off center, which clips it by 0.4 and bumps.

## 3. Not done

- [ ] 3.1 A jump's own tick: the impulse is applied this tick, but the velocity read is from the last step, so a ceiling within a tick's rise of the head, as in a tunnel 2 tiles high, is bumped without correction. Answered by §4.3.
- [ ] 3.2 Clips within the cast's tolerance: `Entity.RoomToMove` casts a box smaller by `Entity.ContactTolerance`, 0.01, on every side, so a clip smaller than that isn't seen, and a nudge can leave the box overlapping the corner by up to that much. Presumably the engine pushes the player out sideways. Not checked. Superseded by §4.1: the box is widened instead.
- [ ] 3.3 Sideways correction: running with the feet clipping the top corner of a step, nudging up. It could make crossing 1-tile gaps reliable (`007`), and help landing on ledges. Its own threshold, and whether it applies on the ground, in the air, or both.

## 4. After playing the first draft

Neil (2026-10-28): it works very well, except in two cases. Standing at the mouth of a tunnel 2 tiles high, there is no correction; at one 3 tiles high there is. And a player standing flush with a corner, having slid down the wall above it into a tunnel, bumps the corner when it jumps and is knocked away from the wall, though a player a little further under the corner, so less precise, is corrected past it.

- [x] 4.1 The flush bump is the engine's speculative contacts (Claude, from probing). At the start of a step, Jolt gives a contact to shapes within `speculative_contact_distance` of each other, 0.02 tiles by default. A corner within that of the head, across and ahead, gets a contact whose normal points from the corner, so the player loses most of its rise and is knocked away. Whether it happens depends on where the ticks land: placed flush, the player rose past a lone ceiling row every time, and was knocked away below a corner with the wall going on above it at gaps under 0.02. The cast, smaller than the box by the contact tolerance, couldn't see such a corner.
  - Decision (Neil, Claude): the cast's box is widened across the motion by the speculative contact distance and some slack. `Constants.SpeculativeContactDistance` reads it from the project setting, as `Constants.Gravity` does gravity. Only as far as each side has room: a cast whose box starts overlapping anything misses everything, as probing confirmed, so a box widened into a wall beside the player would see nothing.
  - So a player against a wall built of boxes stacked with a seam in its face can still be bumped at the seam, as it can land on one (`001 §1.2`).
- [x] 4.2 Nudges are a quarter of a pixel apart, so that a player flush with a corner is nudged about 0.03 tiles, which looks flush, instead of a pixel.
- [x] 4.3 Actual movement instead of intended (Neil). `CornerCorrection.UpwardCorrect` takes the entity's velocity, so anything can use it, even an entity that flies, and doesn't check whether the entity is on the ground: the engine still reports the floor for a tick after a jump, which is where the tunnel 2 tiles high was bumped. The velocity is taken, not read, because the jump's impulse doesn't show until the engine's next step: the player passes its velocity plus the impulse's change, so a clip within the jump's first step is seen too. Against: a rising platform carrying the player into a ceiling corner nudges it on the platform; probably welcome.
- [~] 4.4 Which side: by the velocity's angle, not a dead band on its horizontal part (Neil). A side is allowed unless the velocity leans away from it by more than `PlayerUpwardCornerCorrectionAngle`, tested with the dot product of the normalized velocity and the side. At 0 this is Celeste's rule, nudging only the way the body leans, or either way straight up; at 90, either way always. An angle is easy to tune, and can be made lax, as for getting up through a 1-tile gap in a ceiling easily, or, sideways, running over 1-tile gaps in a floor at low speed, perhaps carrying a block. 10 degrees to start, to be tuned by playing.
- [x] 4.5 `PlayerControllerTest` checks the flush corner under tunnels 3 and 2 tiles high: the jump rises as high as from open ground, and the player stays within a pixel of the wall. Not tested: a clip within the jump's first step, and the angle. Both tested since (§5.2).

## 5. Against the lean

- [x] 5.1 Neil, from playing: nudging against the lean, within the angle, makes getting up through 1-tile gaps in a ceiling fairly easy, and sideways correction will need it to run over 1-tile gaps in a floor, where gravity moves the player against the upward nudge. `PlayerUpwardCornerCorrectionAngle` goes to 30 degrees.
- [x] 5.2 Nudged against the lean, the player drifts back towards the corner during the step: up to 0.12 tiles at 30 degrees from a full jump. The motion was checked from where the nudge left it, so a nudge could be taken that the drift undid, and the player bumped anyway. Now it is checked from where the drift leaves it, as far as nothing stops the drift (Claude). So a nudge against the lean is larger by the drift, and within the furthest nudge less often. `PlayerControllerTest` jumps at angles from a tunnel 2 tiles high, with the angle set in the test: leaning towards the nudge, leaning against it within the angle with and without much drift, and leaning against it beyond the angle.
