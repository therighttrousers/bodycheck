# 015: The phase ability

## Context

Inciting event (2026-10-18): work on issue #5, "Design/implement the phase ability", began.

What the design says ([progression.md](../designs/progression.md#ability-phase)): the player teleports to the other side of an object. Its momentum is replaced: a fixed magnitude, in one of eight directions chosen by input. Its position is replaced: a fixed distance from the object's center, in the direction of the new momentum. A movable object is thrown the opposite way, and lighter objects farther. [characters.md](../designs/characters.md) says every NPC can be phased through.

Open in progression.md: what momentum the target gets (to be decided by trying both options), what happens when the destination is blocked, what phasing straight down through a grounded target is for, whether heavy or immovable targets launch the player faster, whether dead NPCs can be phased through, and whether platforms can.

Goals:
- A phase the player can use for traversal and to throw robots, blocks and platforms, in every test room and the LDtk world.
- Both options for the target's momentum, to try.
- A test room for trying it.

Anti-goals: visual or sound effects, animation, damage, stun, turrets, dead NPCs. NPCs that phase.

## 1. Using it

- [x] 1.1 Which input phases? Decision (Neil): as leaning. A new action, `phase`, acting on a press only, like jump: holding it doesn't phase again.
  - Leaning (Claude): Left Shift and J on the keyboard, X (the west face button) on a gamepad.
  - Changed (Neil): Left Shift brought up Windows' offer to turn on Sticky Keys, after it was pressed many times in a row. Jump, phase and boost are now on three keys under each hand: J, K and L for the right, with the arrow keys, and C, X and Z for the left, with W, A, S and D. On a gamepad: A, X and the right trigger.
- [x] 1.2 Which entity is the target? Decision (Neil): (b).
  - (a) Only one the player touches.
  - (b) The nearest one whose box is within a gap of the player's box, e.g. 1 tile.
  - (c) The nearest one in the chosen direction, within a cone.
  - Leaning (Claude): (b). The destination doesn't depend on where the player is, only on the target and the direction, so the direction needn't pick the target too. A gap between boxes, rather than a distance between centers, treats a wide platform like a robot.
- [~] 1.3 Which kinds of entity can be phased through? Leaning (Claude): every kind, as characters.md says, platforms included. Phasing through a platform throws it and leaves its riders behind, which is puzzle material.

## 2. Where the player goes

- [x] 2.1 The direction. Decision (Neil): (a). One of eight, from the horizontal and vertical inputs, the stick's direction snapped to the nearest 45 degrees. With no direction held:
  - (a) The line from the player through the target's center, snapped to eight. This is "the other side" literally.
  - (b) The way the player faces.
  - (c) No phase.
  - Leaning (Claude): (a).
- [x] 2.2 The distance. Fixed, from the target's center, as progression.md says: 2.5 tiles to start with. That clears the player's box from every entity there is, in every direction: a robot needs 1.8 vertically, a platform 1.8 horizontally.
- [x] 2.3 When is the destination blocked, and what then? Decision (Neil): (a). Blocked: the player's box at the destination overlaps tiles or any other entity, or the line from the target's center to the destination crosses a tile, so that a phase never passes through a wall.
  - (a) Nothing happens: no teleport, no change in momentum, no throw.
  - (b) The player goes to the nearest place that is free.
  - (c) The player goes as far along the line as is free.
  - Leaning (Claude): (a). It is predictable, and (b) and (c) can put the player somewhere it didn't aim, or back inside the target. Telling the player that a phase failed waits for effects.

## 3. Momentum

- [~] 3.1 The player's new speed: fixed, 16 tiles per second to start with. Without drag, rising from that speed would climb about 4.5 tiles, as high as the highest jump, from 2.5 tiles above the target's center. Measured (2026-10-18, `--probe=phase`): with drag, and drifting left, the feet rise about 3.6 tiles after the teleport. That reaches a ledge 6 tiles above the floor from a robot standing on it, as the "Phase" room has. The change is one impulse, the player's mass times the difference between the new velocity and the old, so momentum is still tracked.
- [ ] 3.2 What momentum the target gets. To be decided by trying both, so both are built, chosen by the player's tuning:
  - (A) The opposite of the player's new momentum. Every phase throws the same.
  - (B) The opposite of the player's change in momentum. Conserved; the throw depends on how the player was moving.
- [x] 3.3 A phase ends a jump's thrust and coyote time. Expected: contacts would be a step behind the teleport, so that for the tick after a phase the player would have to count as in the air, or brake against the ground it left. Measured (2026-10-18): they aren't. On the tick after a phase up from the floor, the player is already off the ground, so nothing more is needed.

## 4. Building it

- [~] 4.1 Where the code goes. The controller decides whether and where to phase; the entity offers what a body can do: find the entities near it, say whether its box fits at a place, and teleport. The physics queries read the world as of the engine's last step, like everything else a controller sees.
- [~] 4.2 When the teleport happens. Setting a body's position moves it at once, so a controller stepped later in the same tick would see the player where it went, unlike forces and impulses (`005 §3.4`). Leaning (Claude): the entity holds the destination until every entity has been stepped, and sets it then, before the engine's step, so that what controllers decide still doesn't depend on the order they run in.
  - As built: the entity sets its position in its own `_PhysicsProcess`, which Godot runs after the room's, since the room is its parent. It also resets physics interpolation, so the player isn't drawn sliding from where it was to where it went.
  - Not tried: setting the transform from `_IntegrateForces`. Interpolation would have to be reset after the engine's step, which nothing calls back for.
