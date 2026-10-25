# Movement

Relevant decision records:
- [000: Initial design](../decision-records/000-initial-design.md)
- [001: Godot's 3D physics engine](../decision-records/001-godot-3d-physics.md)
- [005: Jolt's joint motors for traction](../decision-records/005-jolt-motors-for-traction.md)
- [007: Running over one-tile gaps is not a design goal](../decision-records/007-one-tile-gaps.md)
- [015: The phase ability](../decision-records/015-phase.md)
- [018: Game time and real time](../decision-records/018-game-and-real-time.md)
- [019: Carrying blocks on walkers](../decision-records/019-carrying-blocks.md)
- [022: Actions at the end of a tick](../decision-records/022-end-of-tick-actions.md)
- [024: Playtesting feedback](../decision-records/024-playtesting-feedback.md)
- [025: Platforms that move along any line](../decision-records/025-platforms-on-any-line.md)
- [026: A carrier's drive doesn't grow with its load](../decision-records/026-carrying-drive.md)
- [027: Engine friction for walkers and carrying](../decision-records/027-engine-friction.md)
- [028: Continuous detection against walls](../decision-records/028-continuous-detection-against-walls.md)

## Player movement

Quantities that are still being tuned, such as speeds and forces, are described by comparison. Distances in tiles and angles are design goals.

The player is 0.8 tiles wide and 1.8 tall.

### Running

- Input sets a direction and, on a stick, how hard to drive.
- Drive fades as the player nears top speed in the direction of input, and is zero at top speed and beyond, so it never slows a player that something else has made faster. Running settles just under top speed, where drive balances drag, and gets most of the way there quickly.
- Drive against the direction of travel is braking, and is stronger than drive along it, mostly for precision landings and quick turns. Braking is what the player does; reversing, changing direction, is where it leads if the player keeps braking past a stop.
- On the ground, drive is traction (see [below](#ground-contact-friction-and-traction)). A player that is idle, or driving against its direction of travel, also brakes with ground friction, which is stronger than forward drive. Friction stops the player and never reverses it: while braking, friction stops the player within the tick at most, and only the drive pushes it on past zero (`019 §4.8`, `027 §2.1`).
- Drive is the player's legs, and doesn't grow with what it carries, so carrying slows it as the stack's mass says; only its grip grows with the load (`026 §1.2`).
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
- A walker is a `WalkerEntity`. It grips what it stands on with the engine's friction, at its own ground coefficient, which it has only on the ground, so that it slides along walls in the air, and with its controller's traction, below (`001 §2.1`, `027 §1.2`).
- No body uses continuous collision detection, which stopped fast walkers against what they touched (`028`).
- Other bodies use the engine's friction at their own coefficient: blocks, which have no controller and are only pushed, carried and stood on, and hovering platforms, so that what rests on them is carried. What stands on a walker takes the walker's carrying coefficient (below).
- Tiles have friction 1. A walker's ground coefficient and what a carrier holds are rough, so that they win over the tiles' and a block's, below (`027 §1.4`, `§1.5`).
- Bounce is 0 on everything. Drag is our own force, not the engine's damping, which gives lower jumps under the same tuning (`005 §2.1`).
- All solid tiles are shapes of one static body, merged along rows into larger boxes, so that a floor seam can't catch a sliding body (`001 §1.2`).
- Bodies are on physics layers: tiles on the static layer, blocks on the prop layer, and the player, robots and platforms on the character layer. Every entity collides with all three, so the layers change nothing about what collides yet; they let queries choose what they see. See [architecture.md](architecture.md).
- Four project settings make contacts exact and corners sharp (`001 §1.3`): collision margin fraction 0.005, penetration slop 0, continuous collision detection's maximum penetration 0, which has no effect while nothing uses continuous detection (`028`), and 60 physics ticks per second.
- Once per tick, before the engine's step, the room steps every entity: its motors are switched off, its carrier steadies it, its controller runs, then its drag is applied. After the entity steps, each sets its friction for this tick's step (`005 §3.3`, `019 §4.1`, `027 §1.6`).
- A controller pushes its entity with the engine's own forces and impulses, and a walker's also with traction, below.
- A motor earns its place when its target speed binds, as in braking or holding against pushes, or when its push must land on another body. Otherwise a plain force does the same with less. So a platform moves itself with forces: one that cancels its weight; a drive along its line toward a target speed, which falls near the end so that it brakes to a stop there; and a critically damped PD controller of our own that holds it to the line and sags under a rider. The drive and the hold are capped, so that enough load overpowers them (`005 §3.6`, `025 §2.1`, `§2.3`).
- A controller sees the world as of the engine's last step. Neither the forces nor the impulses applied this tick show in any entity's velocity until the next step, so what a controller decides doesn't depend on the order in which entities are stepped (`005 §3.4`).
- A move through other entities, such as a phase's, waits until every entity has been stepped, and happens before the engine's step, so that it doesn't show to controllers stepped later in the same tick either. The controller defers it until after the entity steps, with `IDefer.RunAfterEntitySteps`, which the world runs once it has stepped every entity, whether the game is playing or paused for a controller (`022 §2.1`). The move sweeps the body toward where it goes against tiles only, sliding along any it meets, and leaves the engine's step to push apart whatever ends up overlapping. It resets physics interpolation, so the body isn't drawn sliding to where it went (`015 §4.2`, `§5.21`).
- On the tick a phase is made, the contacts are from before the move, so the player's controller neither runs nor jumps then, and the phase clears its coyote time and buffered jump (`015 §5.21`). By the next tick, the engine has stepped from where the bodies went, whether the phase was made during play or while paused (`022 §3.1`). An entity asked to move through entities has no motors in this tick's step; its friction for that step is still from where it stood before the move (`019 §4.2`).
- Gravity is set in one place: the project setting `physics/3d/default_gravity`, which the test project copies. Core reads it as `Constants.Gravity`, for what needs gravity without the gravity scale (`025 §2.2`). An entity's weight uses the engine's figure for the gravity on it, which includes its gravity scale. Only a phase's grab changes the scale after an entity is built, to 0, so a held entity has no weight, and a walker with none doesn't run (`024 §1.1`). A change of gravity scale takes effect between two steps: the entity makes it after the engine's step, with `IDefer.RunAfterPhysicsStep`, at the start of the next tick. So every controller on that tick, and that tick's step, see the gravity from before the change, whatever order the entities are stepped in (`024 §1.2`, `027 §7.1`). A phase released while the game is paused restores gravity at the start of the next tick, before the engine steps again.

## Ground contact, friction and traction

Rationale: decision record `000 §2.2`, `000 §2.5`, `001 §2.1`, `005`, `019` and `027`.

- **Friction is the engine's.** Each tick, after the entity steps, every entity sets its own coefficient for this tick's step, from what it touched during the last step (`027 §1`, `§1.6`):
  - A walker has none in the air, so that it slides along walls (`027 §1.2`). Other bodies keep their own.
  - On the ground, a walker has its ground coefficient, and it is rough, so that it wins over what the walker stands on, tiles or a body (`027 §1.5`).
  - A body that stands on a carrier, a body with a carrying coefficient such as a walker, takes the carrier's coefficient as its own, rough, so that it wins over the carrier's, even while the carrier slips (`027 §1.4`). While it touches something at its side, it has its own instead, not rough, so that it doesn't grip a wall with the carrier's (`027 §4`). A body whose tuning says it can't be carried, the player or a platform, keeps its own.
  - Anything else has its own.
- The engine takes the smaller of two bodies' coefficients, unless one is rough, which then wins, and of two rough ones the larger: Godot passes a rough coefficient to Jolt as negative, and Jolt takes the absolute value of the smaller. It multiplies the coefficient by the contact's normal force, so a grip grows with what is stacked above, and a landing's impact comes with friction (`027 §1.4`).
- A body's coefficient is the same for all its contacts. So a walker on the ground grips a wall it touches as well as the floor, at its ground coefficient, and a block stacked on a carried block grips it at the carrier's (`027 §5.1`).
- A walker's friction is set whether or not its controller runs. Its coefficients, for the ground and for what it carries, are its body's tuning, which reads them from the features (`019 §4.5`, `§6.1`).
- **Slipping.** A controller may let its entity slip, with `SlipAfterEntitySteps`: its coefficient is 0 for this tick's step, so that only what the controller does with its grip moves it against its support. It lasts one tick.
- **Traction** is the motor of a joint between a walker and what it stands on, or the world when it stands on tiles: the grip. The walker owns it, so that it lives as long as the walker does (`005 §1.1`, `019 §1.2`). It has every limit off, so that its only effect is its motor, along X, at the contact with what the walker stands on. It is anchored again whenever that changes, or the contact is more than 0.05 tiles above or below it (`019 §2.2`, `§4.6`).
- A joint's motor pushes along the axes of what the entity stands on, at the entity's point of the joint, which is fixed in the entity when Godot builds the engine's joint. Godot builds it only when a node path changes to a new value, so `Joints.Anchor` puts the joint where it should act, level, and clears both node paths before setting them (`019 §2.2`).
- `ApplyTraction` sets the grip's target speed, top speed in the direction of drive, and its limit, the drive (`027 §2.1`). An idle walker calls nothing, and brakes with its friction. One driving the way it is already going slips, so that friction doesn't fight its drive. One braking doesn't slip: its friction helps to stop it, and stops at no slip, so only the drive pushes it on past zero (`019 §4.8`). One jumping slips, whichever way it drives, so that a jump leaves with the walker's speed, a buffered one on landing included (`019 §6.5`). One driving into something it touches at its side is driving, not braking, whatever its speed against its support, so it slips, and doesn't grip what it pushes against (`027 §6`).
- A motor's push lasts one tick, like a force: an entity switches its motors off at the start of its step, before its carrier steadies it and its controller runs. In the air the grip is off.
- The engine solves the motors and the friction together with the tick's contacts, so they take account of every force on the body, and of what other bodies do to it in the same tick.
- A character's speed is measured relative to what it stands on: tiles, or another body.
- Traction and friction push back on the body underneath with equal force. Tiles take it without moving.
- An idle character on a moving body stays exactly where it stands, and leaves with that body's velocity when it jumps or walks off.
- Jumps from a body are as high as jumps from tiles.
- In the air there is no traction. Air drive is a plain force with nothing pushing back.
- When what an entity stands on is freed, its motors are off from the next tick (`019 §3.3`).

## Carrying

Rationale: decision records `019` and `027`.

- A block on a walker's head stays on it as it would on a platform. It grips the head with the walker's carrying coefficient, which wins over the block's own and over the walker's (`019 §1.4`, `027 §1.4`), and the player's is higher than a block's.
- A body whose tuning says it can't be carried isn't. A platform keeps its own coefficient on an entity it rests on, so on a walker it holds at the walker's ground coefficient, and slides off while the walker slips (`025 §5.1`). The player keeps its own ground coefficient on a robot, and isn't steadied, so that it can run off the top of a robot going the other way (`027 §1.7`).
- **The player steadies what it carries**, like a hand held up behind its head: a joint's motor, at the carried body's center, holds the center's speed to the player's, with a limited force, so that friction at the body's base needn't push it, and doesn't tip it. The same joint's angular motor turns the body towards level, the nearest quarter turn, with a limited torque, so that a block caught tilted settles flat. The player lets go once the body's center is past the side of its head, so a block mostly off the head tips off it (`019 §5.1`, `§5.2`). The torque matters most: the steadying force is 0 (`027 §5.2`).
- Nothing is steadied while its carrier is in the air, so that jumping or falling can drop what it carries (`027 §5.4`).
- The steadying and the torque are light. A block stays on through running, starting and stopping. It comes off when the player reverses hard; when it jumps from idle to the side, or reverses in the air, since in free fall the block barely presses on the head and friction can't hold it; and when it runs into a tunnel 2 tiles high, whose lip knocks the block off behind (`027 §4.3`, `§5.4`). Running off a ledge with a light block and stopping on landing may throw it off, since it may have tipped in the air.
- Robots don't steady or hold what they carry; their carrying friction alone keeps it on. Being indifferent to what the player drops on them fits the story (`019 §4.10`).
- Moving through entities, as a phase does, leaves what the player carries where it was, to fall (`019 §3.2`).

## NPC movement

TODO

## Open questions

- Not yet checked: jumping up through a 1-tile gap without perfect alignment.
- For a level that wants a comb-shaped floor (1-tile gaps back to back) as a puzzle: how long must the comb be for the chance of crossing it to fall below 1%? What would make crossing reliable, such as bevelled bottom corners on the player's shape? (`007 §1.3`)
- Landing on a ledge is unreliable. With an overhang of 0.16 tiles or less and a fall of under about 3 tiles, the player sinks past the corner in most cases and is eased out sideways. Longer falls land, except with the smallest overhangs, 0.02 tiles or less, which continuous detection used to catch (`028 §3.2`). Not yet tried: more physics ticks per second; a cap on falling speed; our own downward sweep before the step (`001 §1.3`).
- The player can land on a seam partway down a vertical wall. Boxes can't avoid this. Candidate fix: one concave mesh made of the exposed tile faces (`001 §1.2`).
- When two moving bodies meet, they overlap for a tick or two: by up to 0.15 tiles when the player runs into a robot at top speed. The engine tests collisions between moving bodies once per tick, with no continuous detection (`028`). Is that acceptable, given the principle that player and NPCs can't occupy the same space? Bodies at rest against each other don't overlap.
- A block on a platform stays on over 30 seconds of patrolling, but nothing guarantees that for other speeds and sizes. Measured when a wall stopped the platform dead at each end, where the block slid; platforms now slow to turn around before the end of their line (`025 §3.2`).
- Body stability and simulation time under the current settings are to be watched over longer testing.
- A robot that walks into an idle player turns round without moving it, because the player's braking grip is far stronger than the robot's drive. By hand, a robot can push the player in other situations, which no test covers yet.
- Sprinting on a patrolling robot stalls it within a couple of ticks, because the robot's drive is far weaker than the player's. Should a robot hold its patrol speed more firmly when something pushes on it?
- Should a block stacked on a carried block keep gripping it with the carrier's coefficient, as it does because a body's coefficient is the same for all its contacts? Tall stacks play fairly stable, held by motors only at the bottom (`027 §5.1`).
- A walker on the ground grips a wall it touches at its ground coefficient. Driving into the wall it slips (`027 §6`), but one pressed into a wall by something else, such as a robot, while riding a platform past it, would hold the platform. Not seen in play yet.
- An idle rider on a platform ended 30 seconds 0.04 tiles from where it stood, having slid up to 0.19 each time a wall stopped the platform dead. Our own traction stage ended within 0.003. Not investigated (`005 §1.3`), and not measured again since platforms slow to turn around (`025 §3.2`).
- Traction is one use of a joint motor: along X, against what the entity stands on, while it is on the ground. Grip on other axes, against other anchors and with other gates is wanted, for more ways to cheat physics than walking and running. What the joint offers and where it stops is in `005 §3.7`. Every entity's joints are its own (`019 §1.2`).
- How the player drops what it carries (`019 §5.3`). Now: by reversing hard; by jumping from idle to the side or reversing in the air, since nothing is steadied in the air (`027 §5.4`); and by running into a tunnel 2 tiles high. Still on the table: an input that lets go; phase. To be talked over with the playtester once they have played the game as it is and made puzzles.
- A block can be left tipped on the player's head, held, after a reversal at speed: friction at its base holds its corner to the head, steadying holds its center, and turning about its center would move the corner, so the leveling torque can't turn it. Perhaps friction at the base should drop while the player steadies the block, since the hand is carrying it then (`019 §5.1`). Not checked since the steadying force went to 0 and the torque up (`027 §5.2`).
- Not yet checked by playing since carrying stopped boosting the drive (`026 §2.1`, `§2.2`): that a loaded player no longer shoves a platform; and how carrying feels at the slower speed, which if too slow means retuning the drive.
- Should the steadying force go altogether? The torque does most of the work of keeping a block on, and the force is 0 (`027 §5.2`).
- A block landing on a walker's head has its own coefficient for the impact, since what it stands on is from the step before, and the carrier's from the next tick. Fine for now: players can't catch a falling block, and needn't. Whether to fix it (`027 §5.3`).
- `RobotCarryFriction`, how hard a robot's head grips what it carries, is to be tuned for what puzzles need, perhaps lower (`019 §4.10`).
- What a motor costs, for a body that has one on every tick, is unmeasured. Building a joint again costs about 17 microseconds (`019 §3.3`, `§3.4`).
- Surfaces such as ice will need their own rule. A walker's rough ground coefficient wins over any surface that isn't rough, and of two rough ones the larger wins, so a slippery surface can't win over a walker's grip as friction works now (`019 §1.4`, `027 §1.5`).
- What stops the player for about a second after it lands fast on a platform that is nearly still, which turning continuous detection off fixed, and which no test has reproduced (`028 §2.3`, `§3.4`). If continuous detection is ever used again, this and the freezes of `028` need checking first.
