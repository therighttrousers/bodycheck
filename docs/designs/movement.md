Relevant decision records:
- [000: Initial design](../decision-records/000-initial-design.md)
- [001: Godot's 3D physics engine](../decision-records/001-godot-3d-physics.md)

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

# Physics engine

Rationale: decision record `001`.

- Bodies are simulated by Godot's 3D physics engine, Jolt. Each is a `RigidBody3D` with a box shape, with movement along Z and all rotation locked.
- The player is not a `CharacterBody3D`: that kind of body has no mass and can't be pushed (`001 §1.1`).
- Walking entities have engine friction 0 and continuous collision detection, and get their grip from traction, below. With engine friction they grip walls and lose their speed on landing (`001 §2.1`).
- Other bodies, such as rocks to push or stacks to topple, use engine friction and ordinary collision detection. A hovering platform is one of these, so that what rests on it is carried.
- No body is ever `Rough`, so that a walker's zero friction wins on every contact it has (`001 §1.4`).
- Bounce is 0 on everything. Drag is our own force, not the engine's damping.
- All solid tiles are shapes of one static body, merged along rows into larger boxes, so that a floor seam can't catch a sliding body (`001 §1.2`).
- Four project settings make contacts exact and corners sharp (`001 §1.3`): collision margin fraction 0.005, penetration slop 0, continuous collision detection's maximum penetration 0, and 60 physics ticks per second.
- Controllers give a body its forces and impulses through `AddForce` and `AddImpulse`, never the engine's own methods, so that traction can take them into account.
- An impulse doesn't show in a body's velocity until the engine's next step.

# Ground contact and traction

Rationale: decision record `000 §2.2`, `000 §2.5` and `001 §2.1`.

- Traction is applied once per tick, after every controller has run and before the engine's step. It works out the velocity that the tick's forces, impulses and drag give each body, takes grounded bodies towards their targets, and hands the engine one impulse per body.

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

- Landing on a ledge after a short fall is unreliable. With an overhang of 0.16 tiles or less and a fall of under about 3 tiles, the player sinks past the corner in most cases and is eased out sideways. Longer falls always land. Not yet tried: more physics ticks per second; a cap on falling speed; our own downward sweep before the step (`001 §1.3`).
- The player can land on a seam partway down a vertical wall. Boxes can't avoid this. Candidate fix: one concave mesh made of the exposed tile faces (`001 §1.2`).
- Bodies with engine friction are untried: a rock on a platform, a rock pushed by a walker, a walker standing on a rock.
- Body stability and simulation time under the current settings are to be watched over longer testing.
- By hand, the player no longer falls into a one-tile gap at top speed. To be confirmed by a test.
- Pushing from the side, in either direction, is not measured yet. By hand, a robot can push the player.
- Sprinting on a patrolling robot stalls it within a couple of ticks, because the robot's drive is far weaker than the player's. Should a robot hold its patrol speed more firmly when something pushes on it?
- Traction limits come from each character's tuning, not from the load on the contact, so a robot carrying the player grips the floor no harder than one carrying nothing. Is that worth changing?
