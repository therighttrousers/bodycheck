# 022: Actions at the end of a tick

## Context

Inciting event (2026-10-22): playtesting PR #78 (issue #77, phase cooldowns) turned up two phase bugs on branch `phase-cooldowns`.

- **Grabbing while paused.** The grab ran on every paused tick, and each one added the same impulses again, since the velocities it reads stay as of the engine's last step. Fixed by grabbing only on ticks of play.
- **A paused release moved a tick late.** `GameModeManager` ran its physics tick after the world's. On the tick a phase was released while paused, the world was still paused when its turn came, so neither the world's step nor the entities' moves ran. The manager then released the phase and unpaused, and the engine stepped once with the phase's velocities from where the bodies were before the move: phasing up into a platform from below, the player was thrown into it and stopped dead, then moved above it on the next tick.

The first fix for the second bug ran the manager before the world (`ProcessPhysicsPriority = -1`), so that the world steps on the tick the manager unpauses. Neil is uncomfortable with it: on that tick, `StepPaused` and `Step` both run, so the player's controller runs twice, and only `_phaseMade` keeps the second run from doing anything.

Goals:
- A tick is either paused or played, never both.
- A move asked for while paused happens before the engine's step on the same tick, as one asked for during play does.
- Phasing's code in one place, where it can be.

Anti-goals: reordering the engine's own work; running controllers in any particular order.

Background:
- Each physics tick, Godot calls every node's `_PhysicsProcess`, by `ProcessPhysicsPriority`, then tree order, parents first, and skips nodes the tree has paused when their turn comes. The engine steps after that.
- Work that changes what controllers see waits until every controller has run, so that what they decide doesn't depend on the order they run in (`005 §3.4`, `015 §4.2`). Setting a position is seen at once, unlike forces and impulses, so the move through entities waits (`015 §5.21`). Its only place to wait now is the entity's own `_PhysicsProcess`, which runs after the world's, since the entities are below it in the tree.
- So the move through entities lives in `Entity`, though it is phasing's, and only phasing uses it.
- Why only the move needs to wait (Neil): controllers only apply forces and impulses, and the engine applies those after every controller has run. A move can't be put in those terms. Neil tried an impulse that moves the body, with collisions off for a tick and the impulse undone after it, and backed out: the state to manage was too hairy. Anything else that writes the world's state directly, such as spawning or removing entities later, would be in the same position.

Measured (2026-10-22), by a probe that paused the tree and ran an entity's pending move by hand:
- `MoveAndCollide` works while the tree is paused. It went through a robot, stopped at a wall, and printed no errors. The physics server reported the old position until it synced, but the engine's first step after unpausing started from the new one.
- An impulse applied while paused takes effect in that step.

What waits now, from a survey of Core:
- Until every controller has run, before the engine's step: the move through entities (`Entity._destination`), only.
- A whole tick: `PlayerController._phaseMade` skips the first tick of play after a paused release, when contacts are from before the move. It exists only because the move waits a tick on the paused path. So does the check in `Entity.ApplyFriction` for a pending move.
- A tick behind by choice, not by delay: controllers read `LinearVelocity`, which doesn't show this tick's impulses (`005 §3.4`).
- Related: on the tick of a release during play, the grab's impulses and then the throw's are both computed from that same velocity, so the `Fixed` throw doesn't quite replace the velocity. Measured: 11.5 to 12.6 tiles per second instead of 12.7.

## 1. Order within a tick

- [x] 1.1 Can the paused and played steps run in the same tick? Decision (Neil, 2026-10-22): no. Running both is easy to forget, and every controller that does work in both paths would have to guard against it. `GameModeManager` runs after the world, and both set `ProcessPhysicsPriority` explicitly, so that the order doesn't depend on where `Main` adds them. This reverts the manager's priority of -1.
  - As built (2026-10-22): `WorldNode.PhysicsPriority` is 0, which the world sets on itself, and the manager sets its own to one more. The world stays at 0 so that `Main`, its ancestor, still runs before it, which probes rely on for their input.

## 2. Doing work at the end of a tick

- [~] 2.1 How does a move asked for while paused happen before the engine's step on the same tick? The entities' `_PhysicsProcess` doesn't run while the tree is paused. Options:
  - A. `WorldNode.StepPaused`, after stepping every controller, finishes each entity's pending move, with the code now in `Entity._PhysicsProcess` moved into a method both call. Nothing new, but the move stays in `Entity`.
  - B. A queue of actions that run once every controller has run. Controllers add to it, and whatever steps them runs it at the end of the tick, on both paths. Not a search of the tree for methods to call, as the engine does.
  - Leaning (Neil): B, for 2.2 and 2.3.
  - Built (2026-10-22) to try, for Neil to review: B, as `EndOfTick` in Entities, beside what a controller is, with `Defer(Action)`, and `Run()` for whatever steps the controllers. `WorldNode` holds one and runs it after stepping every entity, in its `_PhysicsProcess` and in `StepPaused`. `ReleasedWhilePausedByTheGamePhasesUpThroughAPlatform`, which runs the real `GameModeManager`, passes with the manager after the world.
  - Changed (Neil, 2026-10-22): controllers get `IEndOfTick`, in Entities, which has only `Defer`, so that none is tempted to order things by running the actions itself. `WorldNode` implements it with a private nested class, which has `Run`. It makes a new one at the start of each tick on either path, rather than holding one, so that if a tick throws partway, its actions are dropped rather than run against a later tick's world.
- [ ] 2.2 Where does the move through entities live? With B, `PhaseAction.Phase` can queue the move itself, and phasing's code is in one place. What stays in `Entity` is whatever is a body's: sweeping against tiles only, sliding, and resetting physics interpolation.
  - Built to try: `PhaseAction.Phase` takes the `IEndOfTick`, works out both destinations from where the bodies are, and defers moving them. `Entity.MoveThroughEntities` now moves at once; it no longer holds a destination, and `Entity` has no `_PhysicsProcess`. A probe in the game, `CarryProbes`, calls it directly before the world's step, so its move now shows at once.
- [ ] 2.3 Is the queue for `Step` too? Then both paths do deferred work the same way, and the entities' `_PhysicsProcess` goes. Since the move is then done on the tick of a paused release, `_phaseMade`'s skipped tick and the check in `ApplyFriction` are no longer needed (section 3).
  - Built to try: yes, on both paths. The check in `ApplyFriction` is gone. `_phaseMade` is left as it was, for 3.1.
- [x] 2.4 How do controllers get the queue? Options: a parameter of `Apply` and `ApplyPaused`, passed down from `WorldNode` through `Step` and `StepPaused`; or something the entity or the world holds. Decision (Neil, 2026-10-22): a parameter, which makes the dependency visible. It is an object with one method that takes an action to run later; as a function, it would take a function of no arguments. `ApplyPhase` passes it to `PhaseAction.Phase`.
- [~] 2.5 What else goes in that object? Options:
  - A. Only the queue. The deltas stay separate parameters, as `018 §1.2` has them: `ApplyPaused` takes no game delta, since no game time passes.
  - B. The deltas too. Fewer parameters, but `ApplyPaused` would get a game delta that is always 0, or the two paths would need two types, and `ApplyPhase`, which serves both, would take the play path's with a game delta of 0 when paused. It also lets any method that holds it read either delta, where now the signature says which it uses.
  - C. The entity too. The queue is the world's and lasts the tick, while the entity is each controller's own, so the object would become one per entity per tick.
  - Leaning (Claude): A.
  - Built to try: A.
- [ ] 2.6 Order among queued actions: in the order they were added, which is the order entities are stepped. Moves through entities sweep against tiles only, so their order doesn't matter now. Is that enough?

## 3. Afterwards

- [ ] 3.1 What becomes of `_phaseMade`? To discuss once 2.1 is settled. As built, a paused release moves the bodies before the engine's step on the same tick, so on the first tick of play the contacts are already from after the move, and the tick the player's controller skips is no longer needed. Its comments, and the line in [movement.md](../designs/movement.md) about it, still give the old reason.
  - Removed, to try (Neil, 2026-10-22). `ApplyPhase` still returns whether it phased on the tick of the release, a local now, so that the player neither runs nor jumps on that tick, whose contacts are from before the move. The phase still takes coyote time and the buffered jump (`015 §3.3`). A paused release now behaves as one during play: on the first tick of play, a player that the phase left on a floor can jump from it.
  - `JumpRightAfterAPausedPhaseDoesNothing` is removed: it checked that a jump on the first tick of play did nothing while the contacts were still those of where the player had been, which no longer happens in the game. The tests' own pause, `RoomRun.PausedTick`, keeps the tree paused for the whole tick and unpauses before the next, so the engine doesn't step on the tick of the release as it does in the game; `ReleasedWhilePausedByTheGamePhasesUpThroughAPlatform` uses the real `GameModeManager` for that.
- [x] 3.2 Tests: `AimingWhilePausedPhasesOnTheTickAfterRelease` asserted that nothing moves on the tick of a paused release, which no longer holds with 2.1. As built: renamed `AimingWhilePausedPhasesOnRelease`, it asserts the player is past the robot on that tick. `ReleasedWhilePausedByTheGamePhasesUpThroughAPlatform` passes unchanged.
- [ ] 3.3 The grab's and the throw's impulses on the same tick (Context). Grab only while phase is held, or let it be?
- [ ] 3.4 When this closes: `015 §5.5`'s "As built" ("made on the first tick of play after the release") and `§3.3` (the skipped tick) are superseded, and [architecture.md](../designs/architecture.md) and the Entities and Controllers `intent.md` files may change.
