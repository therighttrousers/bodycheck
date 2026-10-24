# 025: Platforms that move along any line

## Context

Inciting event (2026-10-23): issue #84, vertical platforms. Platforms patrolled horizontally until something stopped them, and held their height with a PD controller whose thrust, capped at 1.5 times their weight, cancelled their weight too. Neil wants one kind of platform that moves between two points in any direction, reusing what there is.

Neil's objectives for the thrust:
- The player can overload a platform by putting enough weight on it.
- All platforms accelerate at the same rate, whichever way they move.
- All platforms return to their lines the same way.

## 1. The line

- [x] 1.1 How is a platform's line set? Decision (Neil): in LDtk, the entity `PlatformHorizontal` becomes `Platform`, with two Point fields, `RangeA` and `RangeB`, set by clicking on the map. The middle of the platform's box moves between the middles of the two cells. LDtk keeps them inside the level.
- [x] 1.2 Platforms without a range? Options: (a) fields optional, and a platform without them patrols horizontally as before; (b) fields required. Decision (Neil): (b), so there is one kind of platform. LDtk warns when they are missing but saves anyway, so the reader rejects a platform without both, and one whose two points are the same cell, since that has no line. Text rooms' `'H'` platforms move between the walls on either side.
- [x] 1.3 A platform's width in tiles may be even, so its middle is on a cell's edge and can't be clicked. Decision (Neil): accept it. Such a platform stops half a tile short of a wall.
- [x] 1.4 As built: the platform's body is shorter than its box, and starts at the box's bottom. The line is moved down by the difference, so the body keeps its place in the box.

## 2. Thrust

- [x] 2.1 Options: (a) generalize the two forces as they were: a drive along the line, and a PD controller across it that also cancels gravity's share across the line, capped at 1.5 times the platform's weight; (b) Neil: three thrusts, each for one job: one that cancels the platform's own weight, uncapped; a drive along the line; and a PD controller across it, back onto the line. Decision (Neil): (b). Gravity never uses up a capped thruster, so a platform accelerates the same way in every direction, and a rider's weight, which nothing cancels, is what the caps resist.
- [x] 2.2 The caps. Decision (Neil): both 0.5 times the platform's weight under unscaled gravity, so that the same load overloads a platform whichever way it moves, and the caps don't change while a grab holds it. Across a horizontal line, the cap's 0.5 over the weight matches the old 1.5. Unscaled gravity is the project setting, read by Core as `Constants.Gravity`, so that nothing assumes gravity is normal when a platform is built.
- [x] 2.3 The drive. First decision (Neil): a PD controller toward the end it is heading for, with no top speed at first, to see what it looks like. If it overshoots, lower its stiffness; if platforms move too fast, add a top speed.
  - Tried (Neil, 2026-10-24): platforms crawled into their ends, much slower than in the middle, and a vertical platform carrying the player never reached its top. Near its target, a critically damped PD's speed is about its frequency over 2 times the distance left, so raising the frequency sped up the ends only by speeding up the middle as much. Against a steady load, such as a rider's weight along the line, a PD settles short of its target by the load over m ω², which at 2 radians per second was more than the turn distance. At 8, the platform reached the top with the player, but hit the floor at the other end. Changing the cap or the damping found nothing Neil liked.
  - Options then: (a) the old headroom drive, which looked good, but stopped only against a wall; (b) a PID controller, whose integral would cancel a steady load, but builds up while the drive is at its cap and then overshoots, must be reset at each turn, and adds a gain; (c) Claude: a speed controller, like headroom, which was one, toward a target speed: top speed, or less near the end, on the curve along which braking at a constant rate stops the platform there. Decision (Neil): (c).
  - As built: the target speed is the least of `PlatformTopSpeed` and √(2 × braking × (distance left + turn distance)). The curve aims past the end by the turn distance, so the target speed is above zero where the platform turns. The braking is `PlatformBrakeFrac` of the drive's maximum acceleration, 0.5, leaving the rest for braking a load. The drive is the mass times `PlatformDriveGain`, 10 per second, times how far the speed is short of the target, capped either way. Unlike headroom, it brakes, so it doesn't need a wall to stop it. With the gain high, a rider only slows it a little: a rider of weight 28 slows a platform of mass 5 by about 0.6 tiles per second.
- [x] 2.4 Where on the line does the hold aim? Decision: the nearest point, which is what the old controller did with its height. It measures only the distance across the line, and leaves the ends to the drive.
- [x] 2.5 As built: the hold is critically damped, and set by a natural frequency instead of a stiffness, so that platforms of any mass hold alike, as the objectives ask. Its 4.75 radians per second is the old stiffness at a mass of 5. The drive's values are accelerations and speeds, for the same reason.
- [x] 2.6 While a grab holds it, a platform has no weight, so there is nothing to cancel. Decision: it doesn't hold to its line, as before (`024 §1.1`), but keeps driving, as it kept patrolling before; Neil says that looks fine and plays well.

## 3. Turning around

- [x] 3.1 When does a platform turn around? Contacts that worked horizontally don't for other directions: a rising platform has its rider above it, so a `Ceiling` contact would turn it at once. Options: (a) at the ends only; (b) also on contact against the direction of travel, from anything but riders, which needs contacts that say what they touch; (c) also when stalled, with too little speed toward its target for a while, which handles walls, the player, a ceiling with the player squashed under it, and overload, alike. Decision (Neil): (a) to start with; (c) later if needed. Platforms no longer bounce off the player: they push it.
- [x] 3.2 How does it know it reached an end? Decision (Neil): from where it is along the line as a fraction, 0 at A and 1 at B. A critically damped drive never quite reaches its target, so it turns 0.1 tiles before each end (the feature `PlatformTurnDistance`). The drive that replaced it (§2.3) does reach it, but turning short of the end keeps it off a wall there.

## 4. Open

- [ ] 4.1 The drive's tuning, to be seen by playing.
- [ ] 4.2 Turning around when stalled (§3.1). Neil wants something similar for robots.
- [ ] 4.3 A platform's mass from its `Width` field.
- [ ] 4.4 Seen once with a platform that moved up and down: the player bounced on it each time it landed on the platform (`005 §3.6`).
