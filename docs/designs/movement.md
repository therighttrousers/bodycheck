Relevant decision records:
- [000: Initial design](../decision-records/000-initial-design.md)

# Player movement

TODO: Clean up, elaborate

Note: As we develop, we will create test rooms and refer to them here; e.g. "player should be able to traverse {room} with {outcome}".

- Minimum jump height is just over 2 tiles.
- Maximum jump height is just over 4 tiles.
- Full-horizontal-speed jumps, on the first few frames, have velocity with ~45 degrees. (May be more or less depending on friction, drag, top speed, jump speed.)
- Air drive is weaker than ground drive.
- Drive in reverse is faster than drive forward (mostly for precision landings).
- Moving off an edge (not jumping) maintains ground control for a short time; i.e. coyote frames.
- Moving over a 1-tile gap at top speed consistently skips over it or falls in it.
- Jumping up through a 1-tile gap doesn't require perfect alignment.

# Ground contact and traction

Rationale: decision record `000 §2.2` and `000 §2.5`.

- A character's speed is measured relative to what it stands on: tiles, or another body.
- On the ground, running and friction are one mechanism, traction. Each tick a character names a target speed relative to its ground and the most force it will use to get there. Braking is a target of zero.
- Traction pushes back on the body underneath with equal force. Tiles take it without moving.
- A character that is idle, or moving against its input, brakes with its ground friction. One that is driving the way it is already going gets no friction, so its drive is never fought.
- An idle character on a moving body stays exactly where it stands, and leaves with that body's velocity when it jumps or walks off.
- Jumps from a body are as high as jumps from tiles.
- In the air there is no traction. Air drive is a plain force with nothing pushing back.

# NPC movement

TODO

# Open questions

- The one-tile gap rule above doesn't hold yet. At top speed the player falls into a one-tile gap on about four runs in five and skips over it on the rest, depending on where the ticks land.
- Sprinting on a patrolling robot stalls it within a couple of ticks, because the robot's drive is far weaker than the player's. Should a robot hold its patrol speed more firmly when something pushes on it?
- Traction limits come from each character's tuning, not from the load on the contact, so a robot carrying the player grips the floor no harder than one carrying nothing. Is that worth changing?
