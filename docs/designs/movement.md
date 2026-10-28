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
- [030: Gameplay speed](../decision-records/030-gameplay-speed.md)
- [032: Player-only situational friction](../decision-records/032-player-only-situational-friction.md)
- [033: Corner correction](../decision-records/033-corner-correction.md)

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
- The lowest jump clears a ledge 2 tiles up but not 3, and the highest clears 4 tiles but not 5. Each aims at the middle, with room to spare at the ledge and no temptation to try the next: the feet rise about 2.5 tiles and about 4.5 (measured 2.50 and 4.50).
- Jump speed is about top speed, so a jump at full running speed starts at about 45 degrees. Held, it stays within 3 degrees of 45 for as long as thrust lasts.
- Jump acts on a press only: holding it through a landing doesn't jump again. There is no jump in the air.
- A press in the air a few ticks before landing jumps on landing: jump buffering. If jump has been let go of by then, it is a tap.
- The impulse and the thrust push down on what the player stands on, with equal force.

### Edges and gaps

- For a few ticks after moving off an edge without jumping, the player can still jump, and drives as on the ground: coyote time. A coyote jump is as high as one from the ground.
- Jump thrust runs on game time, so the slowdown before a phase and a lower gameplay speed stretch it, and a jump rises as high at any speed. It is tuned in game seconds. A tick that the thrust's time covers only part of gets that part of the thrust, so the thrust adds the same speed whatever the ticks' length: the highest jump rises 4.50 tiles at speed 1, and 4.58 at speed 0.5, where shorter steps integrate the rise more closely (`030 §4.5`). Coyote time and jump buffering measure the player's reactions, so they run on real time: they are tuned in ticks at 60 per second, and always last exactly that many ticks (`018 §1.1`, `§2.1`, `030 §2.4`, `§2.6`).
- 1-tile gaps don't belong on running paths: at any speed, a running player may cross one or catch on its far edge and fall in, depending on where the ticks land (`007 §1.1`, `007 §1.2`).
- Jumping up through a 1-tile gap doesn't need perfect alignment: the player has 0.2 tiles of slack in it, and upward corner correction, below, adds to that on each side.

### Corner correction

Rationale: decision record `033`.

- Rising, a player whose head would bump the corner of a tile in this tick's step is nudged sideways round it, by up to `PlayerUpwardCornerCorrection`, so that it doesn't. It is nudged by the smallest nudge, in quarter pixels, that clears, to a side its velocity doesn't lean away from by more than `PlayerUpwardCornerCorrectionAngle` (`033 §4.2`, `§4.4`, `§5.1`). Nudged against the lean, it drifts back towards the corner during the step, so the rise must be clear from where the drift leaves it (`033 §5.2`). Any rise counts, not only a jump, on the ground too, since the engine still reports the floor for a tick after a jump (`033 §4.3`).
- The bump is predicted, before the engine's step, from the player's vertical velocity: as of the last step, plus this tick's jump impulse, which the engine shows only after its step. So nothing is lost: velocity and thrust go on as they were (`033 §1.1`, `§4.3`).
- The engine gives a contact to a corner that starts a step within its speculative contact distance, 0.02 tiles, and the contact knocks the player away from it. So a corner that the head would only pass within that distance of counts as a bump: the predicting cast is widened across the motion by that much and some slack, on each side as far as there is room (`033 §4.1`).
- Only tiles are corrected round, and only when a tile is the first thing the head would meet, so a body, such as a block carried on the head, is bumped as usual. The nudge, and the rise after it, must be clear of everything the player collides with; otherwise there is no nudge, and the player bumps (`033 §1.3`–`§1.5`).
- The nudge is made after the entity steps, like a phase's moves, and is drawn interpolated, as a slide. Corrections can chain, one per tick (`033 §1.6`, `§1.7`).
- `CornerCorrection.UpwardCorrect` takes any entity's velocity, so anything that rises, even an entity that flies, can use it. It is built on a correction along either axis, so a sideways correction, nudging up or down, can use that with its own nudge and angle (`033 §1.8`, `§4.3`).

## Physics engine

Rationale: decision record `001`.

- Bodies are simulated by Godot's 3D physics engine, Jolt. Each is an `Entity`: a `RigidBody3D` with a box shape, with movement along Z and rotation locked, except that a block can rotate about Z.
- The player is not a `CharacterBody3D`: that kind of body has no mass and can't be pushed (`001 §1.1`).
- A walker is a `WalkerEntity`: the player or a robot. It moves itself with its controller's traction, below (`001 §2.1`). The player is a `PlayerEntity`, the one body whose friction changes with what it touches and does: it grips what it stands on at its ground coefficient, which it has only on the ground, so that it slides along walls in the air (`027 §1.2`, `032 §1.1`).
- No body uses continuous collision detection, which stopped fast walkers against what they touched (`028`).
- Every other body uses the engine's friction at its own coefficient, all the time: robots; blocks, which have no controller and are only pushed, carried and stood on; and hovering platforms, so that what rests on them is carried (`032 §1.2`).
- Tiles have friction 1. The player's ground coefficient is rough, so that it wins over the tiles' and a block's, below (`027 §1.5`).
- Bounce is 0 on everything. Drag is our own force, not the engine's damping, which gives lower jumps under the same tuning (`005 §2.1`).
- All solid tiles are shapes of one static body, merged along rows into larger boxes, so that a floor seam can't catch a sliding body (`001 §1.2`).
- Bodies are on physics layers: tiles on the static layer, blocks on the prop layer, and the player, robots and platforms on the character layer. Every entity collides with all three, so the layers change nothing about what collides yet; they let queries choose what they see. See [architecture.md](architecture.md).
- Four project settings make contacts exact and corners sharp (`001 §1.3`): collision margin fraction 0.005, penetration slop 0, continuous collision detection's maximum penetration 0, which has no effect while nothing uses continuous detection (`028`), and 60 physics ticks per second.
- Once per tick, before the engine's step, the room steps every entity: its motors are switched off, its carrier steadies it, its controller runs, then its drag is applied. After the entity steps, the player sets its friction for this tick's step (`005 §3.3`, `019 §4.1`, `027 §1.6`).
- A controller pushes its entity with the engine's own forces and impulses, and a walker's also with traction, below.
- A motor earns its place when its target speed binds, as in braking or holding against pushes, or when its push must land on another body. Otherwise a plain force does the same with less. So a platform moves itself with forces: one that cancels its weight; a drive along its line toward a target speed, which falls near the end so that it brakes to a stop there; and a critically damped PD controller of our own that holds it to the line and sags under a rider. The drive and the hold are capped, so that enough load overpowers them (`005 §3.6`, `025 §2.1`, `§2.3`).
- A controller sees the world as of the engine's last step. Neither the forces nor the impulses applied this tick show in any entity's velocity until the next step, so what a controller decides doesn't depend on the order in which entities are stepped (`005 §3.4`).
- A move through other entities, such as a phase's, waits until every entity has been stepped, and happens before the engine's step, so that it doesn't show to controllers stepped later in the same tick either. The controller defers it until after the entity steps, with `IDefer.RunAfterEntitySteps`, which the world runs once it has stepped every entity, whether the game is playing or paused for a controller (`022 §2.1`). The move sweeps the body toward where it goes against tiles only, sliding along any it meets, and leaves the engine's step to push apart whatever ends up overlapping. It resets physics interpolation, so the body isn't drawn sliding to where it went (`015 §4.2`, `§5.21`).
- On the tick a phase is made, the contacts are from before the move, so the player's controller neither runs nor jumps then, and the phase clears its coyote time and buffered jump (`015 §5.21`). By the next tick, the engine has stepped from where the bodies went, whether the phase was made during play or while paused (`022 §3.1`). An entity asked to move through entities has no motors in this tick's step; its friction for that step is still from where it stood before the move (`019 §4.2`).
- Gravity is set in one place: the project setting `physics/3d/default_gravity`, which the tests run under too. Core reads it as `Constants.Gravity`, for what needs gravity without the gravity scale (`025 §2.2`). An entity's weight uses the engine's figure for the gravity on it, which includes its gravity scale. Only a phase's grab changes the scale after an entity is built, to 0, so a held entity has no weight, and a walker with none doesn't run (`024 §1.1`). A change of gravity scale takes effect between two steps: the entity makes it after the engine's step, with `IDefer.RunAfterPhysicsStep`, at the start of the next tick. So every controller on that tick, and that tick's step, see the gravity from before the change, whatever order the entities are stepped in (`024 §1.2`, `027 §7.1`). A phase released while the game is paused restores gravity at the start of the next tick, before the engine steps again.

## Ground contact, friction and traction

Rationale: decision record `000 §2.2`, `000 §2.5`, `001 §2.1`, `005`, `019`, `027` and `032`.

- **Friction is the engine's.** Every body has its own coefficient, all the time, except the player. Friction will be wrong in some situation whatever the rule, so only the player's changes with its situation, since it has the most reason to (`032`). Each tick, after the player steps, it sets its coefficient for this tick's step, from what it touched during the last step (`027 §1`, `§1.6`):
  - On the ground, unless it slips, it has its ground coefficient, and it is rough, so that it wins over what it stands on, tiles or a body (`027 §1.5`).
  - In the air, or while it slips, it has its engine friction, 0, so that it slides along walls (`027 §1.2`). Touching something at its side doesn't change it on the ground (`032 §1.6`).
- The engine takes the smaller of two bodies' coefficients, unless one is rough, which then wins, and of two rough ones the larger: Godot passes a rough coefficient to Jolt as negative, and Jolt takes the absolute value of the smaller. It multiplies the coefficient by the contact's normal force, so a grip grows with what is stacked above, and a landing's impact comes with friction (`027 §1.4`).
- A body's coefficient is the same for all its contacts. So the player on the ground grips a wall it touches as well as the floor, at its ground coefficient, and what stands on the player grips it at the player's coefficient while that is rough.
- The player's friction is set whether or not its controller runs. Its ground coefficient is its body's tuning, which reads it from the features (`019 §4.5`, `§6.1`, `032 §3.2`).
- **Slipping.** The player's controller may let it slip, with `PlayerEntity.SlipAfterEntitySteps`: its coefficient is 0 for this tick's step, so that only its traction moves it against its support. It lasts one tick. `RunAction.Run` says when, below. Robots don't slip (`032 §1.3`, `§3.3`).
- **Traction** is the motor of a joint between a walker and what it stands on, or the world when it stands on tiles: the grip. The walker owns it, so that it lives as long as the walker does (`005 §1.1`, `019 §1.2`). It has every limit off, so that its only effect is its motor, along X, at the contact with what the walker stands on. It is anchored again whenever that changes, or the contact is more than 0.05 tiles above or below it (`019 §2.2`, `§4.6`).
- A joint's motor pushes along the axes of what the entity stands on, at the entity's point of the joint, which is fixed in the entity when Godot builds the engine's joint. Godot builds it only when a node path changes to a new value, so `Joints.Anchor` puts the joint where it should act, level, and clears both node paths before setting them (`019 §2.2`).
- `ApplyTraction` sets the grip's target speed, top speed in the direction of drive, and its limit, the drive (`027 §2.1`). An idle walker calls nothing, and brakes with its friction. `RunAction.Run` returns whether the walker should slip, and the player's controller acts on it: a player driving the way it is already going slips, so that friction doesn't fight its drive. One braking doesn't slip: its friction helps to stop it, and stops at no slip, so only the drive pushes it on past zero (`019 §4.8`). One jumping slips, whichever way it drives, so that a jump leaves with the player's speed, a buffered one on landing included (`019 §6.5`). One driving into something it touches at its side is driving, not braking, whatever its speed against its support, so it slips, and doesn't grip what it pushes against (`027 §6`).
- A motor's push lasts one tick, like a force: an entity switches its motors off at the start of its step, before its carrier steadies it and its controller runs. In the air the grip is off.
- The engine solves the motors and the friction together with the tick's contacts, so they take account of every force on the body, and of what other bodies do to it in the same tick.
- A character's speed is measured relative to what it stands on: tiles, or another body.
- Traction and friction push back on the body underneath with equal force. Tiles take it without moving.
- An idle character on a moving body stays exactly where it stands, and leaves with that body's velocity when it jumps or walks off.
- Jumps from a body are as high as jumps from tiles.
- In the air there is no traction. Air drive is a plain force with nothing pushing back.
- When what an entity stands on is freed, its motors are off from the next tick (`019 §3.3`).

## Carrying

Rationale: decision records `019`, `027` and `032`.

- Nothing has a carrying coefficient. Whatever stands on a body grips it as any two bodies grip, so anything can carry anything, as a platform carries what rests on it (`032 §1.7`, `§2.1`).
- What the player carries grips its head at the player's rough ground coefficient while the player is idle or brakes, and at its own coefficient against the player's 0 while the player slips, driving or jumping, or is in the air: not at all (`032 §2.2`, `§2.3`).
- A body whose tuning says it can't be carried isn't steadied, below: the player, so that it can run off the top of a robot going the other way, and a platform (`025 §5.1`, `027 §1.7`, `032 §2.4`).
- **The player steadies what it carries**, like a hand held up behind its head: a joint's motor, at the carried body's center, holds the center's speed to the player's, with a limited force. It is what holds a block on while the player runs, since friction then can't. The same joint's angular motor turns the body towards level, the nearest quarter turn, with a limited torque, so that a block caught tilted settles flat. The player lets go once the body's center is past the side of its head, so a block mostly off the head tips off it (`019 §5.1`, `§5.2`, `032 §2.2`).
- Nothing is steadied while its carrier is in the air, so that jumping or falling can drop what it carries (`027 §5.4`).
- The steadying force is below the player's drive, so that the lip of a tunnel 2 tiles high, pushing back on a block, can't stop the player. It is enough to catch a block that lands on the running player's head 5 tiles per second slower than it. A block stays on through running, starting and stopping. It comes off when the player reverses hard; when it jumps from idle to the side, or reverses in the air, since in free fall the block barely presses on the head and friction can't hold it; and when it runs into a tunnel 2 tiles high, whose lip knocks the block off behind (`027 §4.3`, `§5.4`). Running off a ledge with a light block and stopping on landing may throw it off, since it may have tipped in the air.
- Robots don't steady what they carry; their own low coefficient alone keeps it on, so a block slides on a robot's head as the robot speeds up or slows down. Being indifferent to what the player drops on them fits the story (`019 §4.10`).
- Moving through entities, as a phase does, leaves what the player carries where it was, to fall (`019 §3.2`).

## NPC movement

- A robot patrols only while it stands on tiles. On anything else, such as a block, a platform or another robot, it stands still.
- It walks one way until it stalls: its speed in the direction it is walking stays below `RobotStallSpeed` for `RobotStallTime`, as when something is in its way. Then it turns round. So it pauses briefly before every turn. It doesn't walk towards a side it touches, so a robot touching something on both sides stands still, facing the camera. Its first direction is right, unless it touches something there.
- **Overheating.** Each stall adds heat, `RobotStallHeat`, and the robot sheds heat at a steady rate, `RobotStallCooldownRate` per second. So it overheats only if it stalls more often than that rate allows, on average, with `RobotStallMaxHeat` as how many quick stalls it tolerates. When its heat reaches `RobotStallMaxHeat`, it overheats and stands still, facing the camera, until its heat is back to 0. This keeps a robot that can't get anywhere, such as one too loaded to move, from turning round over and over.
- A robot patrols with traction, as a walker, but never slips: its friction is its own low coefficient all the time, so it drives against its friction with the floor. Its drive and top speed are raised to make up for that, so an unloaded robot patrols as it did when it slipped. Its friction with the floor grows with what it carries, so a loaded robot walks slower: about 1.9 tiles per second carrying the player, against 2.7 unloaded (`032 §1.2`, `§1.4`).
- A robot's low coefficient makes it easy to push, for the player and for other bodies (`032 §1.5`).

## Open questions

- Corner correction: how far it should reach, 0.25 tiles to start against Celeste's equivalent 0.4, and how lax its angle should be, 30 degrees for now (`033 §2.4`, `§4.4`, `§5.1`).
- A player rising against a wall isn't widened into it, so a seam in the wall's face, where boxes stack, can still bump it (`033 §4.1`).
- Sideways corner correction, nudging up a player whose feet clip the top corner of a step: it might make 1-tile gaps reliable to cross (`007`) and landing on ledges easier (`033 §3.3`).
- For a level that wants a comb-shaped floor (1-tile gaps back to back) as a puzzle: how long must the comb be for the chance of crossing it to fall below 1%? What would make crossing reliable, such as bevelled bottom corners on the player's shape? (`007 §1.3`)
- Landing on a ledge is unreliable. With an overhang of 0.16 tiles or less and a fall of under about 3 tiles, the player sinks past the corner in most cases and is eased out sideways. Longer falls land, except with the smallest overhangs, 0.02 tiles or less, which continuous detection used to catch (`028 §3.2`). Not yet tried: more physics ticks per second; a cap on falling speed; our own downward sweep before the step (`001 §1.3`).
- The player can land on a seam partway down a vertical wall. Boxes can't avoid this. Candidate fix: one concave mesh made of the exposed tile faces (`001 §1.2`).
- When two moving bodies meet, they overlap for a tick or two: by up to 0.15 tiles when the player runs into a robot at top speed. The engine tests collisions between moving bodies once per tick, with no continuous detection (`028`). Is that acceptable, given the principle that player and NPCs can't occupy the same space? Bodies at rest against each other don't overlap.
- A block on a platform stays on over 30 seconds of patrolling, but nothing guarantees that for other speeds and sizes. Measured when a wall stopped the platform dead at each end, where the block slid; platforms now slow to turn around before the end of their line (`025 §3.2`).
- Body stability and simulation time under the current settings are to be watched over longer testing.
- A robot that walks into an idle player turns round without moving it, because the player's braking grip is far stronger than the robot's drive. By hand, a robot can push the player in other situations, which no test covers yet.
- Sprinting on a patrolling robot stalls it within a couple of ticks, because the robot's drive is far weaker than the player's. Should a robot hold its patrol speed more firmly when something pushes on it?
- A block stacked on a carried block grips it at their own coefficients, no longer the carrier's, so tall stacks may be less stable than they were, held by motors only at the bottom. Assumed fine; to be checked by playing (`027 §5.1`, `032 §2.5`).
- The player on the ground grips a wall it touches at its ground coefficient. Driving into the wall it slips (`027 §6`), but one pressed into a wall by something else, such as a robot, while riding a platform past it, would hold the platform. Not seen in play yet. Zero friction at a side contact would fix it, but would take the player's footing whenever anything touches its side (`032 §1.6`).
- An idle rider on a platform ended 30 seconds 0.04 tiles from where it stood, having slid up to 0.19 each time a wall stopped the platform dead. Our own traction stage ended within 0.003. Not investigated (`005 §1.3`), and not measured again since platforms slow to turn around (`025 §3.2`).
- Traction is one use of a joint motor: along X, against what the entity stands on, while it is on the ground. Grip on other axes, against other anchors and with other gates is wanted, for more ways to cheat physics than walking and running. What the joint offers and where it stops is in `005 §3.7`. Every entity's joints are its own (`019 §1.2`).
- How the player drops what it carries (`019 §5.3`). Now: by reversing hard; by jumping from idle to the side or reversing in the air, since nothing is steadied in the air and what is carried has no grip on the player there (`027 §5.4`, `032 §2.3`); and by running into a tunnel 2 tiles high. Still on the table: an input that lets go; phase. To be talked over with the playtester once they have played the game as it is and made puzzles.
- A block can be left tipped on the player's head, held, after a reversal at speed: friction at its base holds its corner to the head, steadying holds its center, and turning about its center would move the corner, so the leveling torque can't turn it. Friction at the base is now 0 while the player slips, and its ground coefficient while it brakes (`032 §2.2`). Not checked since.
- Not yet checked by playing since carrying stopped boosting the drive (`026 §2.1`, `§2.2`): that a loaded player no longer shoves a platform; and how carrying feels at the slower speed, which if too slow means retuning the drive.
- How carrying through a jump feels, now that what is carried has no grip on the player in the air. If it drops too easily, the options are to steady in the air too, or to give the player a coefficient in the air, which would grip a ceiling it bumps (`032 §2.3`).
- The steadying force, 60, is close to the player's drive, 67.5, so a block caught by a tunnel's lip may slow the player a lot until the block's center is past its side. Not checked (`032 §2.2`).
- Whether a loaded robot walking slower plays well, and whether a robot's coefficient, which is also how hard its head grips what it carries, suits puzzles (`019 §4.10`, `032 §1.4`).
- What a motor costs, for a body that has one on every tick, is unmeasured. Building a joint again costs about 17 microseconds (`019 §3.3`, `§3.4`).
- Surfaces such as ice will need their own rule. The player's rough ground coefficient wins over any surface that isn't rough, and of two rough ones the larger wins, so a slippery surface can't win over the player's grip as friction works now (`019 §1.4`, `027 §1.5`).
- What stops the player for about a second after it lands fast on a platform that is nearly still, which turning continuous detection off fixed, and which no test has reproduced (`028 §2.3`, `§3.4`). If continuous detection is ever used again, this and the freezes of `028` need checking first.
