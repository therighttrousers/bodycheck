# 005: Jolt's joint motors instead of our own traction stage

## Context

Inciting event (2026-10-13): Neil asked how traction could be generalised into more "game physics escape hatches" than walking and running. Traction turned out to be a velocity motor with a force limit, which Jolt already has. Godot exposes it as the linear motor of `Generic6DOFJoint3D`.

History: traction came from our own simulation (`000 §2.5`) and was kept on top of Jolt (`001 §2.1`) after engine friction failed in three ways: a boost after take-off, sticking to walls, and a loss of speed on landing. Motors weren't considered then.

Neil's position: traction feels overly specific, and it forces a proxy for the engine's bodies (`Body`, with state copied in and out each tick). Testing traction without Godot is an advantage, but not much of one. Moving `BodyCheck.Core.Tests` to gdUnit4Net would be fine.

Goal: decide whether a walker's grip becomes a motor inside the engine's solver, by trying it.

Closed on 2026-10-13 with the decisions to use the motor (item 4.1) and to remove the proxy (item 3.1). Outcomes and unresolved items are in the design docs: [high-level-design.md](../designs/high-level-design.md) and [movement.md](../designs/movement.md).

Tensions:
- Record `002` put the rules in Core behind `Body` so that they run outside the engine. Without traction, the main reason for `Body` goes.
- The generalisation Neil asked for (grip on other axes, against other anchors, with other gates) is still wanted, whichever way traction is applied.

## 1. Does a joint motor reproduce traction?

- [x] 1.1 How the spike applies it. Branch `spike/jolt-motors`, first commit. Switched on by `--traction=motor` after `--`, or the environment variable `BODYCHECK_TRACTION=motor`, so that the same probes and tests run both ways. The switches were removed when item 4.1 was decided.
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
  - Feel, by hand. Done (Neil, 2026-10-13): he can't tell the difference.
  - What happens to the joint when its anchor is freed while the walker stands on it.

## 2. Can drag be the engine's damping?

- [x] 2.1 Decision (Neil, 2026-10-13): no; drag stays an explicit force. Tried with `--damping=engine` (`BODYCHECK_DAMPING=engine`), which sets each body's linear damping to drag ÷ mass and drops our drag force.
  - Riding is still exact, so the motor does cancel the engine's damping.
  - Jumps are lower: a tap jump reaches 2.175 tiles against 2.210, and `Jumping_gives_an_upward_impulse` fails. Likely cause: the engine damps the velocity after adding the tick's forces to it, and we damp the velocity from before them. Not confirmed.
  - It would need a retune for no gain. A controller can apply `-drag × velocity` to the engine itself.

## 3. Removing the proxy

Part two, decided in conversation on 2026-10-13 and built the same night by Claude.

- [x] 3.1 Does `Body` go? Decision (Neil): yes. With the motor, nothing needs every force to pass through `World.Step`. `Body`, `World`, `BodyNode` and `Block` are deleted, and controllers push the engine's bodies directly.
  - This supersedes part of `002`: Core's rules use nodes, and can't run outside the engine.
- [x] 3.2 Can a node class live in Core? Outcome: yes, with Godot's source generators. Tried on 2026-10-13 with a throwaway `RigidBody3D` subclass.
  - As a plain library: the node is created and simulated, but `_Ready`, `_PhysicsProcess` and `_IntegrateForces` are never called, and nothing warns.
  - With a package reference to `Godot.SourceGenerators` and four properties in `BodyCheck.Core.csproj`: all three are called. Core stays a plain class library and keeps referencing the installed engine's `GodotSharp.dll`.
  - The generator that records each script's `res://` path is off, because Core is outside the Godot project. So a Core node class is created in code and can't be attached to a scene in the editor.
  - Consequence for art: a character made as a scene would be a visuals-only scene, loaded by the game and handed to the entity as a child.
- [x] 3.3 How entities and controllers are wired. Neil proposed a base class for game entities that are drawn, have physics and run code each tick, held in one list and stepped from `_PhysicsProcess`. Decisions, each Neil's after discussion:
  - `Entity` is the rigid body, not a holder of one, so that contacts and joints lead straight to entities. If that goes wrong, we back out and contain instead.
  - The room holds the list and steps it, not `Main`: tests build a `RoomNode` with no `Main`.
  - The player's input stays injected, as tests and probes need. `PlayerController` is given its input source when constructed, since `Apply` no longer receives it.
  - Controllers stay separate from entities. Swapping an entity's controller isn't planned, and isn't ruled out. `Apply` is passed the entity, which costs nothing and keeps that possible.
  - `ICharacterController` is the interface for every controller, the player's included, so that an NPC can be made as capable as the player.
  - One `Entity` class to start, with no `Player` or `Robot` subclass: they differ only in tuning, looks and controller. Subclass when it gets painful in a way subclassing fixes.
  - Tuning is split. `BodyTuning` has the body's values (half-size, mass, drag, engine friction), with one preset per kind of body. `PlayerTuning`, `RobotTuning` and `PlatformTuning` keep the controller's values.
  - Looks are handed to the entity, not loaded by it. The test projects can't see the game's files (`001 §3.1`), so an entity that loaded its own model would fail there.
- [x] 3.4 As built.
  - Traction is applied each tick, like a force, with `ApplyTraction(targetSpeed, maxForce)`. `Entity.Step` switches the motor off, runs the controller, and applies drag.
    - First built with `ApplyTraction` storing the two values and `Step` passing them to the motor afterwards. Neil asked whether the delay had a reason. Its only one was switching the motor off on a tick when no traction is applied, which switching it off first does with three fewer fields.
  - Controllers read the node's `LinearVelocity`, which shows neither the forces nor the impulses applied this tick.
    - First built with an `Entity.Velocity` that asked the physics server, which does include this tick's impulses, as `Body.Velocity` had. One test needed it: `Driving_does_not_slow_a_body_that_is_over_top_speed` pushes the player with an impulse and drives it in the same tick.
    - Neil called it a smell, and pointed out that forces don't show either. Removed: no probe changes without it, and the test now gives the push a tick of its own. A gain: what a controller decides no longer depends on the order entities are stepped in.
    - Cost: a controller reacts a tick late to an impulse from elsewhere. Driving forward on the tick of a boost can take up to one tick of drive off it.
  - Gravity is the project setting `physics/3d/default_gravity`, and weight uses `RigidBody3D.GetGravity`.
    - First built with the room setting the space's gravity when it loads, as before. `GetGravity` then returns the engine's default 9.8 on a body's first tick, so the platform started with a fifth of its thrust and dipped 0.385 tiles on the rider's landing, against 0.382. Worked around with a constant in Core.
    - Neil asked for gravity in one place. With the project setting, the engine has it before any body exists, the first tick is right, and neither the constant nor the room's call is needed. The test project copies the game's settings, so tests get it too.
  - Results: all ten probes print what they did before the rewrite, apart from the sign of a zero. 30 of 30 in-engine tests pass, with changes only to how they build rooms and name things.
  - Not built: looks supplied by `Main`. `RoomNode` still makes the box for each entity itself.
- [ ] 3.5 Where do the tests go? Not changed: `BodyCheck.Core.Tests` is still xUnit, with only the tile-grid tests, and everything that needs the engine is in `BodyCheck.Game.Tests`.
  - Tried: converting `BodyCheck.Core.Tests` to the Game tests' setup (Godot SDK, gdUnit4Net, a copied `project.godot`). A Core node gets its callbacks there, and the xUnit tests keep running in the same project.
  - Open: whether two test projects still earn their keep, now that Core's rules need the engine.
- [ ] 3.6 A joint motor or spring in place of the platform's PD controller. Deferred until the rest is done. Neil wants to do it himself.
  - To be checked first: whether the joint's linear spring has a force limit, which `MaxVerticalThrustFrac` needs.
- [ ] 3.7 The generalisation that started this record: grip on other axes, against other anchors, with other gates. The joint gives axis (X, Y), anchor (`node_a`), gate (the enable flag) and limit (`force_limit`) directly. Limits to know:
  - Godot exposes one symmetric force limit per axis. Jolt has separate minimum and maximum, which Godot doesn't expose, so a grip that pushes harder than it pulls isn't available.
  - The reaction always goes to the anchor in full. A push that nothing pays for stays a plain force, as air drive is now.
  - A second motor on the same axis against a different anchor needs a second joint.
  - Ramping the target speed, to separate acceleration from strength, is the controller's business either way.

## 4. Decision

- [x] 4.1 Replace the traction stage with joint motors? Decision (Neil, 2026-10-13): yes, with drag kept as our own force (2.1). Claude recommended it. The remaining items of 1.4 are open questions in [movement.md](../designs/movement.md).
  - Done in two parts, in one pull request. Part one removes the traction stage: `World.ApplyTraction` and `TractionTests` are deleted, the motor is the only way, and `World.Step` only adds drag and passes on each body's impulse. The in-engine tests already state the goals that `TractionTests` did.
  - Part two is section 3, discussed after Neil had read part one's diff.

## Notes

- A warning seen while probing, "Jolt Physics job system exceeded the maximum number of jobs", is unrelated: it appeared once in 16 runs of `--probe=ride-robot` with traction, and once in 16 with the motor. Cause unknown. To be watched (`001 §1.3` already asks for stability to be watched).
