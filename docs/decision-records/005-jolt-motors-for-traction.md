# 005: Jolt's joint motors instead of our own traction stage

## Context

Inciting event (2026-10-13): Neil asked how traction could be generalised into more "game physics escape hatches" than walking and running. Traction turned out to be a velocity motor with a force limit, which Jolt already has. Godot exposes it as the linear motor of `Generic6DOFJoint3D`.

History: traction came from our own simulation (`000 §2.5`) and was kept on top of Jolt (`001 §2.1`) after engine friction failed in three ways: a boost after take-off, sticking to walls, and a loss of speed on landing. Motors weren't considered then.

Neil's position: traction feels overly specific, and it forces a proxy for the engine's bodies (`Body`, with state copied in and out each tick). Testing traction without Godot is an advantage, but not much of one. Moving `BodyCheck.Core.Tests` to gdUnit4Net would be fine.

Goal: decide whether a walker's grip becomes a motor inside the engine's solver, by trying it.

Tensions:
- Record `002` put the rules in Core behind `Body` so that they run outside the engine. Without traction, the main reason for `Body` goes.
- The generalisation Neil asked for (grip on other axes, against other anchors, with other gates) is still wanted, whichever way traction is applied.

## 1. Does a joint motor reproduce traction?

- [x] 1.1 How the spike applies it. Branch `spike/jolt-motors`. Switched on by `--traction=motor` after `--`, or the environment variable `BODYCHECK_TRACTION=motor`, so that the same probes and tests run both ways.
  - Each walker's `BodyNode` has a child `Generic6DOFJoint3D` with every linear and angular limit off, so the joint constrains nothing and only its X motor acts. `exclude_nodes_from_collision` is off, or the rider would fall through its support.
  - The walker is `node_b`. `node_a` is the support's node, or empty for tiles, which anchors the joint to the world.
  - Controllers are unchanged: they still set `Body.Traction`. Each tick, after the controllers, the node copies the target speed and maximum force to the motor, enables it only on the ground, and moves the joint when the support changes. `World.Step` skips its traction stage and still passes on forces, impulses and drag.
  - Sign: a positive target velocity moves `node_b` towards +X relative to `node_a`, for both a world anchor and a body. Checked by reversing it: the player then runs the wrong way, and 12 of 30 tests fail.
  - The force limit is a force, in the same units as ours.
- [x] 1.2 Results, 2026-10-13. Outcome: the motor reproduces traction. Every number that the current design depends on is the same, to the precision printed.
  - Player probe (`--probe=player`): identical. Tap jump 2.210 tiles with and without pushing into a wall; top speed 15.000; a jump at top speed keeps 15.000; the reversal matches tick for tick.
  - Riding a robot (`--probe=ride-robot`): identical. The idle rider moves 0.0000 tiles over 30 seconds; the tap jump reaches 2.209 tiles and keeps the robot's speed.
  - Landing (`--probe=landing`, new): identical. Holding the run, 13.710 in the air and 13.691 on the first tick down. Idle since take-off, braking starts on landing at the same rate.
  - Changing anchor (`--probe=run-off`, new): the player sprints along a robot, runs off its front and lands on tiles. The 51-tick trace of both bodies' velocities is identical, across two changes of anchor.
  - In-engine tests: 30 of 30 pass with the motor.
  - Neil's three problems with engine friction don't come back: they came from friction acting on every contact, with a limit set by the contact's impulse. A motor acts on one axis against one anchor, with a fixed limit.
- [x] 1.3 Where the motor differs.
  - It sees the contact impulses of the same tick, which traction can't (`001 §2.1`). In the push probe (`--probe=push`, new) a robot at 3.636 tiles per second walks into an idle player. With traction the two share the momentum for a tick (both at 1.818) and the player brakes on the next. With the motor the player's brake acts in that tick (both at 0.152). The player ends 0.026 tiles from where it stood, against 0.068.
  - Riding a platform (`--probe=ride-platform`): sag, dip and jump height are identical. The rider ends 30 seconds 0.039 tiles from where it stood, against 0.002, with the same largest slide (0.19) when a wall stops the platform dead. Not investigated.
- [ ] 1.4 Not yet measured.
  - The cost of moving the joint. Setting `node_a` rebuilds the joint in the engine. Two moves showed no glitch in the trace; time wasn't measured, and neither was a body that changes support every few ticks.
  - A stack of three or more, where our stage settles approximately over eight passes and Jolt solves the motors together with the contacts.
  - Feel, by hand. Run with `.\Godot\Godot.exe --path game -- --traction=motor`.
  - What happens to the joint when its anchor is freed while the walker stands on it.

## 2. Can drag be the engine's damping?

- [~] 2.1 Leaning: no; keep drag as an explicit force. Tried with `--damping=engine` (`BODYCHECK_DAMPING=engine`), which sets each body's linear damping to drag ÷ mass and drops our drag force.
  - Riding is still exact, so the motor does cancel the engine's damping.
  - Jumps are lower: a tap jump reaches 2.175 tiles against 2.210, and `Jumping_gives_an_upward_impulse` fails. Likely cause: the engine damps the velocity after adding the tick's forces to it, and we damp the velocity from before them. Not confirmed.
  - It would need a retune for no gain. A controller can apply `-drag × velocity` to the engine itself.

## 3. What follows if we adopt it

- [ ] 3.1 Does `Body` go? With the motor, nothing needs every force to pass through `World.Step`, so controllers could hold a `RigidBody3D` and call the engine directly. `World`, `Body.AddForce` and the copying in `BodyNode` would go. The spike didn't do this: it kept `Body` so that both ways could be measured with the same controllers.
  - This supersedes part of `002`: Core's rules would use nodes, and couldn't run outside the engine.
- [ ] 3.2 Where do the tests go? `TractionTests` would be deleted with the stage it tests; `RidingTest` and `PlayerControllerTest` already state the same goals in the engine. Neil is fine with gdUnit4Net for Core's tests.
- [ ] 3.3 The generalisation. The joint gives axis (X, Y), anchor (`node_a`), gate (the enable flag) and limit (`force_limit`) directly. Limits to know:
  - Godot exposes one symmetric force limit per axis. Jolt has separate minimum and maximum, which Godot doesn't expose, so a grip that pushes harder than it pulls isn't available.
  - The reaction always goes to the anchor in full. A push that nothing pays for stays a plain force, as air drive is now.
  - A second motor on the same axis against a different anchor needs a second joint.
  - Ramping the target speed, to separate acceleration from strength, is the controller's business either way.

## 4. Decision

- [ ] 4.1 Replace the traction stage with joint motors? Claude recommends yes, after Neil has tried the feel by hand (1.4), with drag kept as our own force (2.1). The remaining items of 1.4 can be measured during the migration.

## Notes

- A warning seen while probing, "Jolt Physics job system exceeded the maximum number of jobs", is unrelated: it appeared once in 16 runs of `--probe=ride-robot` with traction, and once in 16 with the motor. Cause unknown. To be watched (`001 §1.3` already asks for stability to be watched).
