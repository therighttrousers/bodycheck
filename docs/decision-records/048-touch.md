# 048: Triggers set off by touch

## Context

Inciting event (2026-11-05): issue #162, triggers set off by touch. `043 §4` left what a trigger is in the engine, how touches reach the tick, and what sets one off as leanings or open; the issue chose among them: `Area3D`s on the `Trigger` layer, polled at a fixed point in the tick against what they held at the last tick, set off by the player only. What was left is below.

Goals:
- Touches reach the routing inside the world's tick, in a known order (`043`'s goals).
- Nothing that tests or debugging need beyond what the design needs.

Closed on 2026-11-05. Outcomes are in the architecture docs: the root's conventions, and those of Core and `Simulation`.

## 1. Touch

- [x] 1.1 What a trigger is in the engine (`043 §4.1`): an `Area3D` on the `Trigger` layer, of its box in LDtk, built by `WorldNode`, which keeps it with the trigger's runner. Its mask is the `Character` layer, the player's, and it isn't monitorable.
- [x] 1.2 How touches reach the tick (`043 §4.2`): polled. Each trigger asks its area whether it holds the player, as of the last step, and is set off if it does and didn't at the last poll. Jolt's areas do update their overlaps in the step: a test sees the player set a trigger off as it runs through it, and again as it comes back. Polling is a lookup per trigger per tick, which a few dozen triggers don't feel.
- [x] 1.3 What sets a trigger off (`043 §4.3`): the player only, for now, so what a trigger held at the last poll is one flag.
- [x] 1.4 Where in the tick (`043 §2.5`): in each played tick while controllers are enabled, after the entity steps' deferred actions, just before the world's scheduler runs, in the order of the world's controls (Claude, agreed by Neil). What a touch starts then runs on in that tick's run, so that a chain that finishes at once runs to its end in the tick that the touch came in, as the scheduler already does for chains (`043 §2.6`).
- [x] 1.5 A player that starts in a trigger sets it off at the first poll, since a trigger has held nothing before it (Claude, agreed by Neil): every touch is a change from not holding to holding. Until the game state saves a `Once`, a player respawned in a trigger sets it off again after the world is rebuilt.
- [x] 1.6 How tests see a touch, with no target that does anything visible yet (Claude, agreed by Neil): `WorldNode.TriggerTouched`, an event raised with the trigger's configuration, as `CurrentRespawnChanged` is for respawns. Rejected: a metric in the telemetry.
- [x] 1.7 Tunnelling (`043 §4.2`): an area finds what is in it at the end of each step, so a body is missed if it moves more in a tick than its own width and the trigger's together. A test throws the player at 1.5 tiles a tick through a trigger 1 tile wide, which sets it off, since the player is 0.8 tiles wide.
- [x] 1.8 Showing triggers on F2 (Neil): the map markers already draw every control entity's box, which is the trigger's area. Nothing more for now. The collision debug view draws the areas too.
