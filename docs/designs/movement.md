Relevant decision records:
- [000: Initial design](../decision-records/000-initial-design.md)
- [001: Godot's 3D physics engine](../decision-records/001-godot-3d-physics.md)
- [005: Jolt's joint motors for traction](../decision-records/005-jolt-motors-for-traction.md)

# Player movement

TODO: Clean up, elaborate

Note: As we develop, we will create test rooms and refer to them here; e.g. "player should be able to traverse {room} with {outcome}".

- Minimum jump height is just over 2 tiles.
- Maximum jump height is just over 4 tiles.
- Full-horizontal-speed jumps, on the first few frames, have velocity with ~45 degrees. (May be more or less depending on friction, drag, top speed, jump speed.)
- Air drive is weaker than ground drive.
- Drive in reverse is faster than drive forward (mostly for precision landings).
- Moving off an edge (not jumping) maintains ground control for a short time; i.e. coyote frames.
- Moving over a 1-tile gap at top speed consistently skips over it.
- Jumping up through a 1-tile gap doesn't require perfect alignment.

# Physics engine

Rationale: decision record `001`.

- Bodies are simulated by Godot's 3D physics engine, Jolt. Each is an `Entity`: a `RigidBody3D` with a box shape, with movement along Z and all rotation locked.
- The player is not a `CharacterBody3D`: that kind of body has no mass and can't be pushed (`001 §1.1`).
- Walking entities have engine friction 0 and continuous collision detection, and get their grip from traction, below. With engine friction they grip walls and lose their speed on landing (`001 §2.1`).
- Other bodies use engine friction and ordinary collision detection: blocks, which have no controller and are only pushed, carried and stood on, and hovering platforms, so that what rests on them is carried. A walker is a `WalkerEntity`, which sets its own engine friction to 0 (`005 §3.8`).
- No body is ever `Rough`, so that a walker's zero friction wins on every contact it has (`001 §1.4`). Tiles have friction 1, which therefore affects only bodies that have friction of their own.
- Bounce is 0 on everything. Drag is our own force, not the engine's damping, which gives lower jumps under the same tuning (`005 §2.1`).
- All solid tiles are shapes of one static body, merged along rows into larger boxes, so that a floor seam can't catch a sliding body (`001 §1.2`).
- Four project settings make contacts exact and corners sharp (`001 §1.3`): collision margin fraction 0.005, penetration slop 0, continuous collision detection's maximum penetration 0, and 60 physics ticks per second.
- Once per tick, before the engine's step, the room steps every entity: its controller runs, then its drag is applied (`005 §3.3`).
- A controller pushes its entity with the engine's own forces and impulses, and with the motors that its kind of body has. A walker's is traction, below.
- A controller sees the world as of the engine's last step. Neither the forces nor the impulses applied this tick show in any entity's velocity until the next step, so what a controller decides doesn't depend on the order in which entities are stepped (`005 §3.4`).
- Gravity is set in one place: the project setting `physics/3d/default_gravity`, which the test project copies. An entity's weight uses the engine's figure for the gravity on it.

# Ground contact and traction

Rationale: decision record `000 §2.2`, `000 §2.5`, `001 §2.1` and `005`.

- Traction is a motor inside the engine's solver (`005 §1.1`), owned by the walker. Each walker has a joint with every limit off, so that its only effect is a motor along X. The joint connects the walker to the body it stands on, or to the world when it stands on tiles.
- A motor's push lasts one tick, like a force. An entity switches its motors off at the start of its step, so a controller applies traction on each tick that it wants grip: `ApplyTraction` sets the motor's target speed and maximum force and switches it on. In the air it stays off.
- The engine solves the motor together with the tick's contacts, so traction takes account of every force on the body, and of what other bodies do to it in the same tick.

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
- When two moving bodies meet, they overlap for a tick or two: by up to 0.15 tiles when the player runs into a robot at top speed. The engine tests collisions between moving bodies once per tick, and its continuous detection doesn't start until a body moves about 18 tiles per second. Is that acceptable, given the principle that player and NPCs can't occupy the same space? Bodies at rest against each other don't overlap.
- A block on a platform slides each time a wall stops the platform dead. It stays on over 30 seconds of patrolling, but nothing guarantees that for other speeds and sizes.
- Body stability and simulation time under the current settings are to be watched over longer testing.
- A robot that walks into an idle player turns round without moving it, because the player's braking grip is far stronger than the robot's drive. By hand, a robot can push the player in other situations, which no test covers yet.
- Sprinting on a patrolling robot stalls it within a couple of ticks, because the robot's drive is far weaker than the player's. Should a robot hold its patrol speed more firmly when something pushes on it?
- Not yet measured for the traction motor (`005 §1.4`): what it costs to move the joint when a walker changes what it stands on, for a body that does so every few ticks; a stack of three or more; and what happens when a body is freed while a walker stands on it.
- An idle rider on a platform ends 30 seconds 0.04 tiles from where it stood, having slid up to 0.19 each time a wall stops the platform dead. Our own traction stage ended within 0.003. Not investigated (`005 §1.3`).
- The platform's vertical thrust is a PD controller of our own. Could a joint motor or spring in `PlatformEntity` replace it? Neil's to try (`005 §3.6`).
- What happens to `Entity.Support`, and to the traction joint, when the entity stood on is freed. Nothing is freed during play yet.
- Traction limits come from each character's tuning, not from the load on the contact, so a robot carrying the player grips the floor no harder than one carrying nothing. Is that worth changing?
- Traction is one use of a joint motor: along X, against what the entity stands on, while it is on the ground. Grip on other axes, against other anchors and with other gates is wanted, for more ways to cheat physics than walking and running. What the joint offers and where it stops is in `005 §3.7`. Each such motor belongs to a kind of body (`005 §3.8`).
