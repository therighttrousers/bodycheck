# Movement

Relevant decision records:
- [000: Initial design](../decision-records/000-initial-design.md)
- [001: Godot's 3D physics engine](../decision-records/001-godot-3d-physics.md)
- [005: Jolt's joint motors for traction](../decision-records/005-jolt-motors-for-traction.md)
- [007: Running over one-tile gaps is not a design goal](../decision-records/007-one-tile-gaps.md)
- [015: The phase ability](../decision-records/015-phase.md)
- [018: Game time and real time](../decision-records/018-game-and-real-time.md)
- [019: Carrying blocks on walkers](../decision-records/019-carrying-blocks.md)

## Player movement

Quantities that are still being tuned, such as speeds and forces, are described by comparison. Distances in tiles and angles are design goals.

The player is 0.8 tiles wide and 1.8 tall.

### Running

- Input sets a direction and, on a stick, how hard to drive.
- Drive fades as the player nears top speed in the direction of input, and is zero at top speed and beyond, so it never slows a player that something else has made faster. Running settles just under top speed, where drive balances drag, and gets most of the way there quickly.
- Drive against the direction of travel is stronger than drive along it, mostly for precision landings and quick turns.
- On the ground, drive is traction (see [below](#ground-contact-friction-and-traction)). A player that is idle, or driving against its direction of travel, also brakes with ground friction, which is stronger than forward drive. Braking stops the player and never reverses it: reversing, friction stops the player within the tick at most, and the drive alone pushes it on past zero (`019 §4.8`).
- Drive on the ground grows with what the player carries, in proportion to its load, so carrying slows it less than its mass would (`019 §6.4`).
- Drive in the air is much weaker than on the ground, and there is no friction.

### Jumping

Rationale: decision record `000 §1.8`.

- A jump is an upward impulse when jump is pressed, then an upward thrust, a little weaker than gravity, for as long as jump is held, up to a limit. A tap gives the lowest jump, and holding gives the highest. Thrust ends early if the player stops rising, e.g. at a ceiling.
- The lowest jump clears a ledge 2 tiles up but not 3, and the highest clears 4 tiles but not 5. Each aims at the middle, with room to spare at the ledge and no temptation to try the next: the feet rise about 2.5 tiles and about 4.5 (measured 2.50 and 4.57).
- Jump speed is about top speed, so a jump at full running speed starts at about 45 degrees. Held, it stays within 3 degrees of 45 for as long as thrust lasts.
- Jump acts on a press only: holding it through a landing doesn't jump again. There is no jump in the air.
- A press in the air a few ticks before landing jumps on landing: jump buffering. If jump has been let go of by then, it is a tap.
- The impulse and the thrust push down on what the player stands on, with equal force.

### Edges and gaps

- For a few ticks after moving off an edge without jumping, the player can still jump, and drives as on the ground: coyote time. A coyote jump is as high as one from the ground.
- Coyote time and jump thrust run on game time, so the slowdown before a phase stretches them. Jump buffering measures the player's fingers, so it runs on real time. All are tuned in ticks at 60 per second, and at full speed always last exactly that many ticks (`018 §1.1`, `§2.1`).
- 1-tile gaps don't belong on running paths: at any speed, a running player may cross one or catch on its far edge and fall in, depending on where the ticks land (`007 §1.1`, `007 §1.2`).
- Jumping up through a 1-tile gap doesn't need perfect alignment.

## Physics engine

Rationale: decision record `001`.

- Bodies are simulated by Godot's 3D physics engine, Jolt. Each is an `Entity`: a `RigidBody3D` with a box shape, with movement along Z and rotation locked, except that a block can rotate about Z.
- The player is not a `CharacterBody3D`: that kind of body has no mass and can't be pushed (`001 §1.1`).
- A walker is a `WalkerEntity`, which sets its own engine friction to 0 and uses continuous collision detection (`005 §3.8`). It grips what it stands on with its entity's friction and its controller's traction, below. With engine friction, walkers grip walls and lose their speed on landing (`001 §2.1`).
- Other bodies use engine friction and ordinary collision detection: blocks, which have no controller and are only pushed, carried and stood on, and hovering platforms, so that what rests on them is carried. On a walker, where the engine's friction is 0, an entity's own friction makes it up (below).
- No body is ever `Rough`, so that a walker's zero friction wins on every contact it has (`001 §1.4`). Tiles have friction 1, which therefore affects only bodies that have friction of their own.
- Bounce is 0 on everything. Drag is our own force, not the engine's damping, which gives lower jumps under the same tuning (`005 §2.1`).
- All solid tiles are shapes of one static body, merged along rows into larger boxes, so that a floor seam can't catch a sliding body (`001 §1.2`).
- Bodies are on physics layers: tiles on the static layer, blocks on the prop layer, and the player, robots and platforms on the character layer. Every entity collides with all three, so the layers change nothing about what collides yet; they let queries choose what they see. See [architecture.md](architecture.md).
- Four project settings make contacts exact and corners sharp (`001 §1.3`): collision margin fraction 0.005, penetration slop 0, continuous collision detection's maximum penetration 0, and 60 physics ticks per second.
- Once per tick, before the engine's step, the room steps every entity: its motors are switched off, its friction is applied, its controller runs, then its drag is applied (`005 §3.3`, `019 §4.1`).
- A controller pushes its entity with the engine's own forces and impulses, and a walker's also with traction, below.
- A motor earns its place when its target speed binds, as in braking or holding against pushes, or when its push must land on another body. Otherwise a plain force does the same with less. So a platform moves itself with forces, and holds its height with a PD controller of our own, whose thrust is capped so that enough load sinks it, and which sags under a rider (`005 §3.6`).
- A controller sees the world as of the engine's last step. Neither the forces nor the impulses applied this tick show in any entity's velocity until the next step, so what a controller decides doesn't depend on the order in which entities are stepped (`005 §3.4`).
- A move through other entities, such as a phase's, waits until every entity has been stepped, and happens before the engine's step, so that it doesn't show to controllers stepped later in the same tick either. The entity's own `_PhysicsProcess` does it, since Godot processes a parent before its children. It sweeps the body toward where it goes against tiles only, sliding along any it meets, and leaves the engine's step to push apart whatever ends up overlapping. It resets physics interpolation, so the body isn't drawn sliding to where it went (`015 §4.2`, `§5.21`).
- On the tick of play on which such a move happens, every entity's contacts are still from before it. The player's controller neither runs nor jumps then, nor counts itself on the ground (`015 §5.21`). An entity asked to move through entities has no friction until the engine's next step (`019 §4.2`).
- Gravity is set in one place: the project setting `physics/3d/default_gravity`, which the test project copies. An entity's weight uses the engine's figure for the gravity on it.

## Ground contact, friction and traction

Rationale: decision record `000 §2.2`, `000 §2.5`, `001 §2.1`, `005` and `019`.

- Every entity grips what it stands on with the motors of two joints between the two, owned by the entity, so that they live as long as it does (`005 §1.1`, `019 §1.1`, `§1.2`). Each joint has every limit off, so that its only effects are its motors. Each connects the entity to the body it stands on, or to the world when it stands on tiles, and is anchored again whenever that changes.
- A joint's motor pushes along the axes of what the entity stands on, at the entity's point of the joint, which is fixed in the entity when Godot builds the engine's joint. Godot builds it only when a node path changes to a new value, so the entity moves the joint to where it should act, level, and clears node B before setting node A (`019 §2.2`).
- **The grip** is at the contact with what the entity stands on, and is anchored again whenever it is more than 0.05 tiles above or below it, so that a block tipping off an edge isn't held back (`019 §2.2`, `§4.6`). Its motor is along X.
- **Friction.** On the ground, where the engine's friction falls short of the entity's own coefficient, the grip's motor makes up the difference: a target of no slip, with a limit of the shortfall times the entity's load, its weight and the loads of what stands on it (`019 §1.5`, `§2.1`). That happens on a walker, whose engine friction is 0; elsewhere the engine's friction is enough. The coefficient is what the entity stands on carries with, if it has one, and otherwise the entity's own (`019 §1.4`). The engine's contact impulses aren't used for the load: they are estimates for each contact on its own (`019 §2.3`).
- **A landing's catch.** On the tick after a landing, friction adds as much force as stops the slip within the tick, so whatever lands on a moving walker stops slipping at once (`019 §2.1`). On a body that can turn, the catch acts at its center: a real impact's friction comes with its normal force, which keeps it from tipping the body, and the catch has nothing to balance it (`019 §4.3`).
- A motor's push lasts one tick, like a force: an entity switches its motors off at the start of its step, then applies its friction, then runs its controller. In the air the grip is off.
- A walker's friction applies whether or not its controller runs. Its coefficients, for the ground and for what it carries, are its body's tuning, which reads them from the features (`019 §4.5`, `§6.1`).
- **Traction** adds to friction. `ApplyTraction` sets the grip motor's target speed and a limit of the entity's friction plus an extra force, which may be negative (`019 §1.6`). An idle walker calls nothing, and brakes with its friction. One driving the way it is already going adds its drive minus its friction, so its drive is never fought. One reversing adds its drive and only as much of its friction as stops it, and what it carries, within the tick (`019 §4.8`). One jumping takes all of its friction off on the jump's tick, so a jump leaves with the walker's speed, a buffered one on landing included (`019 §6.5`).
- The engine solves the motors together with the tick's contacts, so friction and traction take account of every force on the body, and of what other bodies do to it in the same tick.
- A character's speed is measured relative to what it stands on: tiles, or another body.
- Traction and friction push back on the body underneath with equal force. Tiles take it without moving.
- An idle character on a moving body stays exactly where it stands, and leaves with that body's velocity when it jumps or walks off.
- Jumps from a body are as high as jumps from tiles.
- In the air there is no traction. Air drive is a plain force with nothing pushing back.
- When what an entity stands on is freed, its grip is off from the next tick (`019 §3.3`).

## Carrying

Rationale: decision record `019`.

- A block on a walker's head stays on it as it would on a platform. A walker's head grips what it carries with a coefficient of its own, which wins over the block's (`019 §1.4`), and the player's is higher than a block's.
- **The player steadies what it carries**, like a hand held up behind its head: the second joint's motor, at the carried body's center, holds the center's speed to the player's, with a limited force, so that friction at the body's base needn't push it, and doesn't tip it. The same joint's angular motor turns the body towards level, the nearest quarter turn, with a limited torque, so that a block caught tilted settles flat. The player lets go once the body's center is past the side of its head, so a block mostly off the head tips off it (`019 §5.1`, `§5.2`).
- The steadying and the torque are light: a block stays on through running, starting and stopping, and comes off when the player reverses hard or runs into a low wall or a tunnel 2 tiles high (`019 §5.1`, `§6.4`).
- Robots don't steady or hold what they carry; their carrying friction alone keeps it on. Being indifferent to what the player drops on them fits the story (`019 §4.10`).
- Moving through entities, as a phase does, leaves what the player carries where it was, to fall (`019 §3.2`).

## NPC movement

TODO

## Open questions

- Not yet checked: jumping up through a 1-tile gap without perfect alignment.
- For a level that wants a comb-shaped floor (1-tile gaps back to back) as a puzzle: how long must the comb be for the chance of crossing it to fall below 1%? What would make crossing reliable, such as bevelled bottom corners on the player's shape? (`007 §1.3`)
- Landing on a ledge after a short fall is unreliable. With an overhang of 0.16 tiles or less and a fall of under about 3 tiles, the player sinks past the corner in most cases and is eased out sideways. Longer falls always land. Not yet tried: more physics ticks per second; a cap on falling speed; our own downward sweep before the step (`001 §1.3`).
- The player can land on a seam partway down a vertical wall. Boxes can't avoid this. Candidate fix: one concave mesh made of the exposed tile faces (`001 §1.2`).
- When two moving bodies meet, they overlap for a tick or two: by up to 0.15 tiles when the player runs into a robot at top speed. The engine tests collisions between moving bodies once per tick, and its continuous detection doesn't start until a body moves about 18 tiles per second. Is that acceptable, given the principle that player and NPCs can't occupy the same space? Bodies at rest against each other don't overlap.
- A block on a platform slides each time a wall stops the platform dead. It stays on over 30 seconds of patrolling, but nothing guarantees that for other speeds and sizes.
- Body stability and simulation time under the current settings are to be watched over longer testing.
- A robot that walks into an idle player turns round without moving it, because the player's braking grip is far stronger than the robot's drive. By hand, a robot can push the player in other situations, which no test covers yet.
- Sprinting on a patrolling robot stalls it within a couple of ticks, because the robot's drive is far weaker than the player's. Should a robot hold its patrol speed more firmly when something pushes on it?
- Not yet measured for the grip's motors (`005 §1.4`): a stack of three or more.
- An idle rider on a platform ends 30 seconds 0.04 tiles from where it stood, having slid up to 0.19 each time a wall stops the platform dead. Our own traction stage ended within 0.003. Not investigated (`005 §1.3`).
- Traction is one use of a joint motor: along X, against what the entity stands on, while it is on the ground. Grip on other axes, against other anchors and with other gates is wanted, for more ways to cheat physics than walking and running. What the joint offers and where it stops is in `005 §3.7`. Every entity's joints are its own (`019 §1.2`).
- How the player drops what it carries (`019 §5.3`). Now: by reversing hard or running into something low, which the light steadying lets throw it off. Options on the table: tuning those limits; an input that lets go; jumping lets go; phase. To be talked over with the playtester once they have played the game as it is and made puzzles.
- A block can be left tipped on the player's head, held, after a reversal at speed: friction at its base holds its corner to the head, steadying holds its center, and turning about its center would move the corner, so the leveling torque can't turn it. Perhaps friction at the base should drop while the player steadies the block, since the hand is carrying it then (`019 §5.1`).
- `RobotCarryFriction`, how hard a robot's head grips what it carries, is to be tuned for what puzzles need, perhaps lower (`019 §4.10`).
- What a motor costs, for a body that has one on every tick, is unmeasured. Building a joint again costs about 17 microseconds (`019 §3.3`, `§3.4`).
- Friction ignores the coefficient of what an entity stands on, apart from a carrier's own: surfaces such as ice will need a rule for combining two, the smaller or the product, say, with a walker's top above 1 (`019 §1.4`).
