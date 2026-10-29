# 035: One shape for actions

## Context

Inciting event (2026-10-29): issue #107, which moves state and logic out of controllers into action classes that other controllers can use: first the robot's laser, which turrets will need, and the player's jump, so that other things can jump by the same rules. Two shapes of action exist already: `RunAction`, a static function with no state, and `PhaseAction`, an object that exists only while a phase is aimed. The laser and the jump would add a third: state that lasts as long as the entity, such as timers and cooldowns. Neil: reduce the number of shapes first, starting with phasing.

Goals:
- Every action has the same shape, so that any controller can use one by building it and wiring it up.
- What an action does to the others, such as which one takes precedence on a tick and which timers one clears, stays visible in each controller.

## 1. The shape

- [x] 1.1 An action is an object built with the entity it acts on, as the kind of body it needs, and the fixed things it works with, such as the player's block. It lives as long as the entity's controller, and keeps its own state. A controller builds the actions it uses, and calls each one on every tick it applies. Decision (Neil): yes, for every action, even one with no state, such as running, so that all are alike.
- [x] 1.2 An action takes intent, not input: what the controller wants, such as whether phase is held and the direction, never a `PlayerInput`, which an NPC has none of. Reading input, such as the stick's dead zone, stays in the player's controller. Decision (Neil): yes.
- [x] 1.3 No interface common to every action (Claude). Each takes a different intent, so an interface would be empty or the lowest common signature. The shape is a convention. Decision (Neil): agreed.
- [x] 1.4 Rejected (Claude, Neil agreeing):
  - A framework of actions, which a controller runs as a list with priorities or arbitration, as in behaviour trees or subsumption. The interactions are few and particular to each controller, and a framework would hide them.
  - Actions owned by the entity. Actions are behaviour, and the entities subsystem doesn't depend on behaviour (`034 §1.5`).

## 2. How actions interact

- [x] 2.1 Interactions are each controller's (Neil): the controller decides which action takes precedence on a tick, and what an action's outcome does to the others' timers. An action returns enough for that, and exposes as little of itself as it can. Decision (Neil): as below (Claude).
  - An action returns an outcome, an enum where a bool would hide a case. Phasing has one: a cancel lets running and jumping apply that tick, as a phase doesn't, but both end the jump.
  - An action that another's outcome changes offers a verb for it, such as ending a jump, instead of its timers. The rule stays the controller's, and how the action carries it out stays the action's.
  - Exposing state to read, such as the phase being aimed, is fine.

## 3. Tuning

- [x] 3.1 Features are read when used, so an action can't keep them from when it was built (`016 §3.1`), and a shared action can't read one entity's features, such as the player's, itself. Options:
  - (a) The action is built with a function that picks its tuning, a record of the values it needs, out of the features, and calls it with `Features.Current` on each tick. First built this way.
  - (b) The action is built with its tuning, a record whose values are functions, each of which reads its feature from `Features.Current` when called. Like `BodyTuning`, so one convention for both. It reads only the values it uses, when it uses them, and a test can change one value with `with`, as `BodyTuning.Player with { Drag = 0 }` does, without the features.
  - Decision (Neil): (b), with functions of no arguments: every call passes `Features.Current`, so a parameter would only suggest another source. Each kind of thing that uses an action has one tuning instance, such as `PhaseTuning.Player`. `BodyTuning`'s functions, which take the features now, move to no arguments in a later change, once the controllers are refactored. An action in progress, such as a phase being aimed, still goes on with the values it started with.

## 4. Phasing

- [x] 4.1 `PhaseAction` becomes `PhaseAim`: what exists while a phase is aimed, with the grab, the direction and the phase itself. A new `PhaseAction` holds what lasts: the cooldown, how long the last direction held still counts, choosing the target, starting the aim, and its release, which phases or cancels. It raises the phase's cues. Decision (Neil): yes. Its outcome is idle, aiming, phased or cancelled. `PhaseAim` is nested in `PhaseAction`, so that the logic of phasing is in one file (Neil).

## 5. Running

- [x] 5.1 `RunAction` becomes an object, built with its walker and a `RunTuning`, with nothing to keep between ticks (§1.1). Its tuning has forces for the ground and for the air, and `Apply` takes whether to use the ground's: the player's controller says so for a moment after it leaves the ground, as before, and the robot's says so while it is on the ground. A robot's forces in the air are 0, which takes the place of its controller not running it in the air (Claude). Decision (Neil): yes, for running too.
- [x] 5.2 Neil: should the robot's `_move` move into the action? Decision (Neil): no, for the reasons below (Claude). Making it pluggable, with something the action asks where to go, would invert control for what a controller does just as well. It is where the robot decides to go, from its stalls, its contacts and overheating, and it also sets the way the robot faces, what its stall detector measures and `IsMoving`. That is intent, which an action takes and doesn't keep (§1.2). The player's is the same, without state: its stick on each tick.

## 6. The laser

- [x] 6.1 `LaserAction` holds the laser's timeline and what the entity sees, out of the robot's controller, so that turrets can fire one too (Neil, issue #107). Decision (Neil): extract it, as laid out (Claude):
  - It is built with a plain `Entity` and a `LaserTuning`, since a turret won't be a `RobotEntity`. On each tick it is given where the beam starts, the way the entity faces and its target, and returns the `Laser`, which the robot's controller puts on the robot. It doesn't write on the entity.
  - It exposes the laser's stage and what it sees, which the robot's movement reads. Nothing else changes its timers.
- [ ] 6.2 The footing rule: an entity that can move doesn't start aiming in the air, and gives up when off the ground too long (characters.md). A turret can't move, so the rule shouldn't hold for it. Left until there is a turret; the likely answer is a value in `LaserTuning` that turns the rule off (Claude).
- [x] 6.3 The laser's place in its timeline was its phase, `LaserPhase`, which clashes with the player's phasing, now that the robot's controller reads the laser's beside code about phasing. Decision (Neil): it is its stage, `LaserStage`, and `Laser.Stage`. Claude weighed state (too general: it sounds like all of the laser's data), step (a tick's word), mode (something switched, not a sequence) and beat (unclear without explaining), and found nothing better.

## 7. Jumping

- [x] 7.1 `JumpAction` holds the jump: the impulse relative to how the ground last moved, the thrust while jump is held, ending the thrust at a ceiling, and the `Jumped` cue, with a `JumpTuning`. Decision (Neil): extract it, as laid out (Claude):
  - When to jump is the controller's, so the action takes whether the entity jumps on the tick. The player's coyote time and buffered press stay in its controller: they are leniency for the player's fingers, on real time, and coyote time also picks the run's forces and the animation's `IsGrounded`. An NPC would just say when.
  - A jump uses up the coyote time and the buffered press, which the controller clears on the tick it jumps. A phase ends the jump through its verb, `Interrupt` (§2.1).
  - It exposes how much thrust time is left after the tick, and whether any is (Neil: computed from it, for one source), which the controller reads to keep a jump from beginning during another's thrust, and which the animation's `IsJumping` and the take-off point for telemetry read. They read it after it counts down, where they read it before, so `IsJumping` ends a tick sooner. Jump heights are the same to five decimals, for taps and holds of 1, 2, 5 and 600 ticks, as on `main`.
  - Two small differences, neither seen in play: the overlay's thrust time shows what is left after the tick, one tick's worth less than before; and while a phase is aimed the action isn't run, so the thrust time doesn't count down, where it did, slowly, as the game slowed. The release ends the thrust either way.
- [x] 7.2 Decision (Neil): remove the player's leniency telemetry, `CoyoteTime`, `ThrustTime` and `JumpBufferTime`, and the overlay's line for them: nobody read them, since they go by too fast to see. The thrust time was exposed only for its metric, besides the check that a jump can't begin during another's thrust, which `Thrusting` answers, so it is private, and the action exposes only `Thrusting` and `Interrupt`. This supersedes the part of `008 §1.3` that has the controller report coyote and thrust time.

## 8. Later

- [ ] 8.1 Patrolling as an action (Neil): many robot-like things might patrol. To be taken up at the end of these refactorings. It would decide intent, such as the robot's `_move` (§5.2), rather than act on the body, so it may be a different kind of thing from the actions so far.
- [ ] 8.2 How to swap abilities (Neil): for example, robots that shoot a laser, shoot missiles, or don't shoot at all. To be discussed.
