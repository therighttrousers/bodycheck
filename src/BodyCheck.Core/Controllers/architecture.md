# Controllers

Relevant decision records:
- [015: The phase ability](../../../docs/decision-records/015-phase.md)
- [022: Actions at the end of a tick](../../../docs/decision-records/022-end-of-tick-actions.md)
- [035: One shape for actions](../../../docs/decision-records/035-one-shape-for-actions.md)
- [046: Actions as async methods](../../../docs/decision-records/046-async-actions.md)
- [049: Doors and their stops](../../../docs/decision-records/049-doors-and-stops.md)
- [050: The cutscene mode](../../../docs/decision-records/050-cutscene-mode.md)
- [051: The scripted run at the spawn](../../../docs/decision-records/051-scripted-run-at-the-spawn.md)

## What it is

What decides an entity's actions each tick: the player's input turned into movement, and the behaviour of each NPC. A controller acts on a body only through what the body offers, and sees the rest of the world through the `IWorldView` it is given in `Ready`: the player's controller reads its input and its block there, and a robot's its target, the player.

## What it is not for

New capabilities of a body, such as a motor: those belong to the entity. Reading the keyboard or a gamepad: a controller is given its input. Choosing which controller an entity gets, or where its entity is in a room.

## Controllers

The player's, the robots', the platforms' and the doors' controllers. Each is built with its entity, as the kind of body it needs.

## Actions

What a controller makes its entity do, which another controller could make its own do, is an action: an object built with its entity and whatever fixed things it works with, which keeps its own state, and which the controller calls on each tick it applies. Phasing, running, jumping, spawning, corner correction and the laser have this shape.

- An action takes intent, such as whether phase is held and the direction, never the player's input, and returns an outcome, an enum, such as whether a phase was made or cancelled.
- How actions interact is each controller's: which takes precedence on a tick, and what one's outcome does to another, through a verb the other offers rather than its timers. A phase ends a jump with `JumpAction.Interrupt`.
- An action is built with its tuning, a record of functions with no arguments, each of which reads a feature from `Features.Current` when called, so that each controller tunes it with its own features: the player's phasing is tuned by `PhaseTuning.Player` (`035 §3.1`). A robot's running is tuned to have no drive in the air, where the player's has its own forces, which it also uses for a moment after leaving the ground.
- A behaviour, such as a robot's `PatrolBehaviour`, is shaped the same way, but decides intent for the controller rather than acting: it only reads its entity (`035 §10.1`, `§10.3`).
- The player's coyote time, buffered press and buffered direction for phasing stay in its controller: they are leniency for its fingers, and coyote time also says when it runs with its forces for the ground. An action is given what the controller means, such as the direction to phase in, which may be one let go of just now (`050 §1.5`).
- In a cutscene, the player's controller acts on its `ScriptedInput`, which a script sets, and which is none unless one does, so that the player otherwise comes to rest (`050 §1.2`, `051 §3.3`). At the cutscene's edges, in `GameModeChanged`, it makes what it remembers of the input fit: entering one, it clears its leniency timers and buffered presses, so that nothing pressed before the cutscene acts during it, and counts no button as held, so that a script's first press counts; leaving one, a button already held isn't a new press, so the jump that ends a dialogue doesn't jump, and the scripted input goes back to none, so that a script can't leave the player running. Only at the edges, so that a script that runs and jumps the player keeps its own (`050 §1.5`, `§4.3`). Actions don't know the game's mode, so that scripts can drive them in cutscenes.

## Responding to a route's actions

A controller can respond to the actions a route asks of its entity, such as a door's `Close`, in one of two ways, each a way of running something that takes several ticks. Which to use depends on who changes the entity's state over time (`046 §1.8`).

- **A controller whose `Apply` drives its entity records the action and returns at once,** as the player's, the robots' and the platforms' would. `Apply` acts on it at its next step, in order with everything else it does, so all of the entity's state stays in the controller and its actions, where the interactions between them can be seen. It is also the only way for something that runs on paused ticks, since the world doesn't resume async actions then. Letting what ran the action wait for it to end is harder, and not obvious.
- **A controller with nothing to do between actions may respond with an `async` method** that holds the whole action, given the `ITick` to wait on, which it scopes to its entity being live, so that the action ends if the entity is removed (`056 §3.4`): a door's `Close` unfreezes the door, thrusts it down until it stops, against its stop or whatever it meets, and freezes it (`049 §2`). Its `Apply` does nothing, or only what it does while idle, and changes nothing the method changes.
- **The two don't mix on one entity.** An async method that drives a body that its `Apply` also drives splits the body's state between them, and hides how they interact. An action that takes time on such an entity is another of the controller's actions, stepped from `Apply`.

## Phasing

The player's controller phases with a `PhaseAction`, using its entity's moves and queries: the action chooses the target, and its `PhaseAim` holds a phase being aimed, with its rules for the grab, the placement and the impulses, and defers the move until after the entity steps (`015 §4.1`, `§5.20`, `§5.21`, `022 §2.2`). While a phase is aimed, the player's controller asks for the game to pause. See [movement.md](../../../docs/designs/movement.md) and [progression.md](../../../docs/designs/progression.md) for what phasing does.

## The robot's laser

A robot's controller targets the world's player, and sets the robot's laser each tick from its `LaserAction`, which steps the laser through its timeline. The action is given the beam's origin and the way the robot faces on each tick, and finds the beam's end, and whether it can see the player, with a ray cast on a plain entity, so that another kind of body, such as a turret, can fire one (`035 §6.1`).

## Open questions

- What a door should do when it meets something on its way, such as opening again or pushing on. For now it stops there, and counts as having opened or closed (`049 §2.2`).
