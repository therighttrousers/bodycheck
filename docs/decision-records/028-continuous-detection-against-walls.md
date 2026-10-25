# 028: Continuous detection against walls

Closed on 2026-10-25. Outcomes and unresolved items are in the design docs: [movement.md](../designs/movement.md).

## Context

Found while measuring `027 §6` (2026-10-25): a player falling while it holds towards a wall it touches stops in the air once it falls at about 20 tiles per second, and stays there, its velocity growing every tick while it doesn't move. Falling idle beside the same wall, it lands. Neil had also seen the player freeze against a wall in a jump, and after phasing down through a block while driving.

Walkers use Jolt's continuous collision detection (`001 §1.3`, `§1.4`), with the project's settings for exact contacts: penetration slop 0, and continuous detection's maximum penetration 0. Jolt uses continuous detection only when a body moves more than a fraction of its size in a tick, 0.75 by default, about 18 tiles per second for the player (`001 §1.3`). `001 §1.3` saw a similar failure when lowering that fraction: "with one collider per tile a sliding body doesn't move at all".

Goal: walkers don't freeze against walls, without losing what continuous detection gives them: landing on a ledge after a long fall (`001 §1.3`, the drop probe's tallest drops).

## 1. Cause

- [x] 1.1 Measured (Claude, 2026-10-25) in a shaft 40 tiles high, the player starting 34 tiles up, touching a wall inside the room, holding towards it:
  - Continuous detection off for walkers: the player lands. So it is continuous detection.
  - Its maximum penetration at 0.01, 0.05 or 0.25 (Jolt's default): no change, to the freeze or to any probe. Godot 4.7.2 reads the setting by that name, so it is applied; it doesn't govern this case.
  - Probably, not checked in Jolt's source: the player's motion has a small part towards the wall, from the drive pressing it there, so the sweep along it hits the wall it touches at its start, and Jolt stops the tick's motion at the hit.
  - The room's outer wall, in an empty frame, doesn't freeze a player falling the same way. Not explained.
- [x] 1.2 Phasing down through a block, then driving either way, in the open, beside a wall on either side, and in a pit 1 tile wide: no freeze. Neil's case after phasing isn't reproduced.

## 2. Fix

- [x] 2.1 Options (Claude), each measured on the shaft and the probes `drop`, `corner`, `seams` and `player`:
  - (a) In the air, a walker doesn't drive into a side it touches, with the test `RunAction` uses since `027 §6`. Lands; the probes print what they did. Covers only the walker's own drive pressing it into a wall.
  - (b) A walker uses continuous detection only while nothing touches its sides. Lands; the probes print what they did. Covers whatever presses it into a wall: its drive, a robot, a carried block.
  - (c) No continuous detection for walkers. Lands, but the drop probe's tallest drops with overhangs of 0.01 and 0.02 no longer land on the wall, which is what `001 §1.3` turned it on for.
  - Decision (Neil): (b). It is set with the other end-of-tick actions, from the contacts of the last step, like the friction (`027 §1.6`), and lives in `Entity`, for any kind of body that uses continuous detection, so that it is one rule, not a walker's exception.
- [x] 2.2 Results, 2026-10-25: 134 of 134 tests pass, among them a new one, `Falling_fast_while_driving_into_a_wall_reaches_the_floor`, which fails without the fix. The probes `drop`, `corner`, `seams`, `player`, `push`, `run-off`, `ride-robot`, `ride-platform`, `landing`, `buffered-landing`, `carry`, `carry-checks`, `wobble`, `tilted`, `offset-landing` and `phase` print exactly what they did without it.
- [x] 2.3 Neil's repro, 2026-10-25: a platform moving down to a stop 1 tile above the floor (its range's lower end at the floor tile's y + 2). Standing under it and phasing up through it while it is still coming down, the player lands on it and can't move left or right for about a second. With continuous detection off for walkers, it moves at once. Landing on the platform after it has started back up, it doesn't stick. Neil: phasing only makes the timing easy; what matters is landing on the platform while it is nearly still. The player is touching only the platform below it, so the rule of 2.1 doesn't cover it: evidence that a body shouldn't use continuous detection unless it has no contacts at all, and that walkers may not need it. Neil: don't use it unless there's a clear need.
  - Not reproduced by a test (Claude, 2026-10-25), in a room with a platform coming down from y 8.5 to a stop at 2.5, its bottom 1 tile above the floor, with continuous detection on (as of 2.1) and off: the player placed above it at 31 moments of its run, dropped from 0.3, 1 or 3 tiles, and then driving right, never fails to move for more than 3 ticks of 30; and standing under it and phasing up at 26 moments, it doesn't stick either. There, the platform comes to rest on the player's head, so the phase is made late, and the player lands on the platform after it has started back up. On and off measured the same wherever both were measured. Not yet tried: the game's pause while a phase is aimed, which tests don't have, and Neil's level itself.
  - Neil's simpler repro: the player at tile (X, Y), a platform at (X, Y - 6) that moves down first, between (X, Y + 5) and (X, Y - 9). Drive left or right on the first tick to get the player off its tile's middle; it lands at about 19 tiles per second; then try to move. Measured (Claude, 2026-10-25) with the player at (10.5, 20) and 0.01 tiles off its middle, its start raised from -3 to 18 tiles, landing at 14 to 32 tiles per second, driving each way, with continuous detection on and off: no run sticks for more than 3 ticks of 30, on (1 run of 3 ticks) or off (3 runs). Here the platform is still coming down at 1 to 7 tiles per second when the player lands, never nearly still, and the landing pushes it past its range's end; so the positions likely differ from what the level makes of Neil's. A tick of the drive at the start in the air doesn't move the player off its tile's middle here.
- [x] 2.4 Superseded by §3, which removes continuous detection: whether a freeze can still happen without a side contact, and why the empty frame's outer wall doesn't freeze the player (1.1).

## 3. No continuous detection

- [x] 3.1 Decision (Neil, 2026-10-25): no body uses continuous detection unless there is a clear need. It has stopped a walker three ways now, each needing its own diagnosis, and what it still gives is small: the drop probe's two tallest drops with the smallest overhangs. 2.1 (b) is removed with it. The mechanism of Neil's platform repro (2.3) is left open: if continuous detection is ever turned back on, it will need investigating anyway, to show that it doesn't cause problems.
- [x] 3.2 Results, 2026-10-25: 134 of 134 tests pass, the wall fall of 2.2 among them. Of the probes of 2.2, all but `drop` print what they did; in `drop`, the tallest drops with overhangs of 0.01 and 0.02 no longer land on the wall.
- [x] 3.3 Neil asked why these freezes showed up only after `027`, which gave walkers the engine's friction. They don't come from it: on `25a944f`, before `027`, the wall fall of 1.1 freezes identically, on the same tick and at the same height. Not checked, but likely: they were seen now because the levels made since have tall shafts and long drops, which reach the 18 tiles per second at which continuous detection starts.
- [ ] 3.4 Open: what in Neil's platform repro (2.3) stops the player.
