# 027: Engine friction for walkers and carrying

## Context

A walker's friction was its own: the engine's was 0, and a grip motor between the walker and what it stood on held it with its coefficient times the load, which `Entity.ComputeLoad` added up through the stack (019 §1.5). What a walker carried was held the same way, with the carrier's coefficient. `RunAction.Run` then had to take that friction back off while driving, keep only part of it while reversing (019 §4.8), and all of it off while jumping (019 §6.5), and a landing needed a catch on top (019 §2.1). Neil, on branch `refactoring`: the load is approximate, and its interplay with the run isn't obvious, so let the engine's friction do it, as it already does for blocks on tiles.

The obstacle was why a walker's engine friction was 0: with it, a walker grips walls, and loses its speed when it lands.

## 1. Where friction comes from

- [x] 1.1 A second, frictionless body, slightly shorter and wider than the walker, joined to it, so that it touches walls first. Tried by Neil. Rejected: the joint stretches. With the mass split evenly, every force and impulse computed from the walker's `Mass` had to be split across both, and jumps fell short; with a light second body, a wall pushes it aside and the walker's own box meets the wall.
- [x] 1.2 Neil: the walker's engine friction is its ground coefficient while it stands on something, and 0 in the air, so it slides along walls. Worked better than anything else tried.
- [x] 1.3 Neil: a walker's friction is also 0 on any tick its controller drives the way it is already going, or jumps, so that only traction moves it against its support. Driving against a wall and jumping no longer catches on the ground's last contacts.
- [x] 1.4 Carrying (Claude). A body has one coefficient for all its contacts, so a walker that slips while it drives would let go of what is on its head. Godot passes a rough material's coefficient to Jolt as negative, and Jolt combines two bodies' coefficients as the absolute value of the smaller (`jolt_space_3d.cpp`, Godot 4.7.2), so a rough coefficient wins over one that isn't rough, and of two rough ones the larger wins. So what stands on a carrier takes the carrier's `CarryFriction` as its own, rough. The engine multiplies it by the normal force, so `ComputeLoad` goes, and a landing's impact comes with friction, so the catch goes too. The steadying motor (019 §5) stays.
- [x] 1.5 A walker's ground coefficient is rough too, since tiles' is 1 and the smaller would win: the player's 4 would be 1.
- [x] 1.6 When it is set: at the end of the tick, for this tick's step. In `_IntegrateForces` it was a tick late: Godot runs that when it flushes the engine's queries, before the scene tree's physics process, so it saw the controller's decision from the tick before. Claude first set it at the end of `Entity.Step`, which was safe because each entity sets only its own coefficient, from its own support and its own controller; Neil moved it to the end-of-tick actions, so that every change that waits, waits for the same moment, and named it `SetFrictionAtEndOfTick` (architecture, "Timing in names").
- [x] 1.7 Neil: the player can't be carried (`CanBeCarried = false`). Carried, it took a robot's carry coefficient, 6, and couldn't run off the top of a robot going the other way. It stands on a robot with its own ground coefficient instead, and isn't steadied.

## 2. The run

- [x] 2.1 Traction is a motor with the drive as its limit, and a flag for whether the walker slips. Reversing without a jump, it doesn't: the engine's friction helps to stop it, and stops at no slip, which is what 019 §4.8 computed by hand.

## 3. Results, 2026-10-25

- [x] 3.1 133 of 133 tests pass. Probes, against the commit before the experiment (`25a944f`):
  - `player`, `push`, `run-off`, `offset-landing`, `drop`: as before.
  - `landing`: an idle landing slides to a stop over 5 ticks, from 8.1 tiles per second, instead of stopping dead.
  - `landing`, `buffered-landing`: the tick after a jump no longer brakes (019 §6.5): presumably because the jump's impulse leaves no normal force for the engine's friction to scale, where the motor's limit was fixed. It leaves at 8.17 tiles per second, against 6.79.
  - `ride-robot`, `ride-platform`: two ticks after a jump, rider and carrier differ by 0.06 tiles per second, against 0.
  - `wobble`: the start of the run is unchanged, 5.44 tiles per second after 20 ticks, with the block flat. Stopping from 7.5 tiles per second takes the player 2 ticks, against 4; the block slides 0.21 tiles forward and tips to 0.27 radians, against 0.11 and 0.21, then settles flat 0.07 tiles ahead of the player's center, against 0.005. The engine's friction follows the normal force from tick to tick, which rises at the front corner as the block tips; the motors' limits were fixed for the tick.
- [x] 3.2 Left open after the results, and where each went: the stop in `wobble` (4.1), carried blocks against walls (§4), a block stacked on a carried block (5.1), a block landing on a head (5.3), and the design docs, which still describe the grip motor's friction, until this record closes.

## 4. What is carried, against walls

- [x] 4.1 Neil, after playing: the stop in `wobble` is fine, and so is the rider's speed after a jump. A carried block gripping walls with its carrier's coefficient is worth fixing; a block stacked on a carried block can wait.
- [x] 4.2 Options (Claude): (a) while what is carried touches something at its side, as of the last step, it has its own coefficient, not rough; (b) carried bodies keep their own friction, and a grip motor holds them on, limited by the carrier's coefficient times the normal force the engine reported at the support, with no load to add up, but a tick late and solved apart from the contacts; (c) per-contact friction, which Jolt has but Godot doesn't expose. Neil: try (a).
- [x] 4.3 Measured, 2026-10-25, with a block of mass 2 on the player's head, with (a) and without:
  - Holding toward a wall, touching it, and jumping: the player rises 0.31 tiles, against 0.004 without; 0.38 with no wall. Unloaded, against the wall, 4.57.
  - Running at a tunnel 2 tiles high, which the player fits and the block doesn't: the block's side meets the tunnel's lip, the block is knocked off behind, and the player runs on in. Without, the player stops at the lip and stays there.
  - `wobble`, `carry-checks` and the tests are as before (133 of 133).

## 5. After playing with it, 2026-10-25

- [x] 5.1 A block stacked on a carried block grips it at the carrier's coefficient, since it makes the carried block rough. Neil, playing with tall stacks: they are fairly stable, and only the bottom is held by motors, which makes sense. Kept for now. Open: whether to fix it later.
- [ ] 5.2 Steadying. Neil, tuning in a long test room: the torque matters most. `PlayerCarrySteadying` is 0 and `PlayerCarryTorque` 100. Open: whether to drop the steadying force altogether.
- [x] 5.3 A block landing on a walker's head has its own coefficient for the impact (3.2). Neil: fine as it is. What players will care about is whether they can catch a falling block, and for now they can't, which is fine. Open: whether to fix it.
- [x] 5.4 Dropping what is carried. Neil: a simple rule that may be intuitive, and seems close to how it works already: nothing is steadied while its carrier is in the air. Tried (Claude), in `Entity.Steady`. Measured with a block on the player's head, of mass 1 and 2, with the rule and without (with steadying at 0, the rule only turns off the torque in the air):
  - Jumping in place, and running and jumping: the block stays on either way. Running and jumping, a block of mass 1 tilts by up to 0.18 radians with the rule, against 0.01.
  - Running, jumping, and reversing in the air: the block comes off either way. In free fall the block barely presses on the head, so the engine's friction, which scales with that, can't hold it against the player's braking; only a motor could.
  - Running off a ledge 2 tiles high, idle on landing: a block of mass 1 comes off with the rule, and stays on without. In the air it tips by 0.1 radians with nothing to level it, and the landing's stop, in 2 ticks, throws it forward off the head. A block of mass 2 stays on either way.
  - Running off the ledge and reversing in the air: the same either way; mass 2 comes off, mass 1 stays.
  - So jumping and reversing already drops a block, through friction, with or without the rule. What the rule adds is that a block tilts in the air, which a hard landing may then finish.
  - Decision (Neil, playing): keep it. Players already have to take some care running off a ledge with a block, and only a little more now; and a jump from idle to the side always knocks the block off, a simple motion. When steadying is animated, with the player's arms raised, lowering them in the air would show that it waits for the landing.
- [ ] 5.5 Neil, testing: riding a platform up while driving against a wall stalls the platform, and near an end of its run, where its drive is small, holds it still. Probably the walker's rough ground coefficient gripping the wall too, the walker's side of §4: driving into the wall, its speed against the platform is about 0, and often a little backwards, which `RunAction` takes for a reversal, so it doesn't slip. Not yet checked. To address after dropping by jumping or falling.
