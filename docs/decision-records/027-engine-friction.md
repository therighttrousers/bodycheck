# 027: Engine friction for walkers and carrying

## Context

A walker's friction was its own: the engine's was 0, and a grip motor between the walker and what it stood on held it with its coefficient times the load, which `Entity.ComputeLoad` added up through the stack (019 §1.5). What a walker carried was held the same way, with the carrier's coefficient. `RunAction.Run` then had to take that friction back off while driving, keep only part of it while reversing (019 §4.8), and all of it off while jumping (019 §6.5), and a landing needed a catch on top (019 §2.1). Neil, on branch `refactoring`: the load is approximate, and its interplay with the run isn't obvious, so let the engine's friction do it, as it already does for blocks on tiles.

The obstacle was why a walker's engine friction was 0: with it, a walker grips walls, and loses its speed when it lands.

## 1. Where friction comes from

- [x] 1.1 A second, frictionless body, slightly shorter and wider than the walker, joined to it, so that it touches walls first. Tried by Neil. Rejected: the joint stretches. With the mass split evenly, every force and impulse computed from the walker's `Mass` had to be split across both, and jumps fell short; with a light second body, a wall pushes it aside and the walker's own box meets the wall.
- [x] 1.2 Neil: the walker's engine friction is its ground coefficient while it stands on something, and 0 in the air, so it slides along walls. Worked better than anything else tried.
- [x] 1.3 Neil: a walker's friction is also 0 on any tick its controller drives the way it is already going, or jumps, so that only traction moves it against its support. Driving against a wall and jumping no longer catches on the ground's last contacts.
- [~] 1.4 Carrying (Claude). A body has one coefficient for all its contacts, so a walker that slips while it drives would let go of what is on its head. Godot passes a rough material's coefficient to Jolt as negative, and Jolt combines two bodies' coefficients as the absolute value of the smaller (`jolt_space_3d.cpp`, Godot 4.7.2), so a rough coefficient wins over one that isn't rough, and of two rough ones the larger wins. So what stands on a carrier takes the carrier's `CarryFriction` as its own, rough. The engine multiplies it by the normal force, so `ComputeLoad` goes, and a landing's impact comes with friction, so the catch goes too. The steadying motor (019 §5) stays.
- [~] 1.5 A walker's ground coefficient is rough too, since tiles' is 1 and the smaller would win: the player's 4 would be 1.
- [x] 1.6 When it is set: at the end of the tick, for this tick's step. In `_IntegrateForces` it was a tick late: Godot runs that when it flushes the engine's queries, before the scene tree's physics process, so it saw the controller's decision from the tick before. Claude first set it at the end of `Entity.Step`, which was safe because each entity sets only its own coefficient, from its own support and its own controller; Neil moved it to the end-of-tick actions, so that every change that waits, waits for the same moment, and named it `SetFrictionAtEndOfTick` (architecture, "Timing in names").
- [x] 1.7 Neil: the player can't be carried (`CanBeCarried = false`). Carried, it took a robot's carry coefficient, 6, and couldn't run off the top of a robot going the other way. It stands on a robot with its own ground coefficient instead, and isn't steadied.

## 2. The run

- [~] 2.1 Traction is a motor with the drive as its limit, and a flag for whether the walker slips. Reversing without a jump, it doesn't: the engine's friction helps to stop it, and stops at no slip, which is what 019 §4.8 computed by hand.

## 3. Results, 2026-10-25

- [~] 3.1 133 of 133 tests pass. Probes, against the commit before the experiment (`25a944f`):
  - `player`, `push`, `run-off`, `offset-landing`, `drop`: as before.
  - `landing`: an idle landing slides to a stop over 5 ticks, from 8.1 tiles per second, instead of stopping dead.
  - `landing`, `buffered-landing`: the tick after a jump no longer brakes (019 §6.5): presumably because the jump's impulse leaves no normal force for the engine's friction to scale, where the motor's limit was fixed. It leaves at 8.17 tiles per second, against 6.79.
  - `ride-robot`, `ride-platform`: two ticks after a jump, rider and carrier differ by 0.06 tiles per second, against 0.
  - `wobble`: the start of the run is unchanged, 5.44 tiles per second after 20 ticks, with the block flat. Stopping from 7.5 tiles per second takes the player 2 ticks, against 4; the block slides 0.21 tiles forward and tips to 0.27 radians, against 0.11 and 0.21, then settles flat 0.07 tiles ahead of the player's center, against 0.005. The engine's friction follows the normal force from tick to tick, which rises at the front corner as the block tips; the motors' limits were fixed for the tick.
- [ ] 3.2 Open:
  - Whether the stop in `wobble` is acceptable, or wants tuning.
  - A rough body is rough against everything it touches: a carried block against a wall grips it with the carrier's coefficient, and so does a block stacked on a carried block, which used to slide at 1.
  - A block landing on a walker's head has its own coefficient for the impact, since its support is from the step before; the carrier's grips from the next tick.
  - The design docs still describe the grip motor's friction.

## 4. What is carried, against walls

- [x] 4.1 Neil, after playing: the stop in `wobble` is fine, and so is the rider's speed after a jump. A carried block gripping walls with its carrier's coefficient is worth fixing; a block stacked on a carried block can wait.
- [~] 4.2 Options (Claude): (a) while what is carried touches something at its side, as of the last step, it has its own coefficient, not rough; (b) carried bodies keep their own friction, and a grip motor holds them on, limited by the carrier's coefficient times the normal force the engine reported at the support, with no load to add up, but a tick late and solved apart from the contacts; (c) per-contact friction, which Jolt has but Godot doesn't expose. Neil: try (a).
- [~] 4.3 Measured, 2026-10-25, with a block of mass 2 on the player's head, with (a) and without:
  - Holding toward a wall, touching it, and jumping: the player rises 0.31 tiles, against 0.004 without; 0.38 with no wall. Unloaded, against the wall, 4.57.
  - Running at a tunnel 2 tiles high, which the player fits and the block doesn't: the block's side meets the tunnel's lip, the block is knocked off behind, and the player runs on in. Without, the player stops at the lip and stays there.
  - `wobble`, `carry-checks` and the tests are as before (133 of 133).
