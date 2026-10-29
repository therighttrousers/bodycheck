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
