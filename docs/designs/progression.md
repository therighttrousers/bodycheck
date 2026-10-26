# Progression

Relevant decision records:
- [000: Initial design](../decision-records/000-initial-design.md)
- [015: The phase ability](../decision-records/015-phase.md)
- [016: Features](../decision-records/016-features.md)
- [018: Game time and real time](../decision-records/018-game-and-real-time.md)
- [023: The player's spawned block](../decision-records/023-spawned-block.md)
- [024: Playtesting feedback](../decision-records/024-playtesting-feedback.md)
- [025: Platforms that move along any line](../decision-records/025-platforms-on-any-line.md)
- [030: Gameplay speed](../decision-records/030-gameplay-speed.md)

Abilities and world changes, in the order the player meets them.

## Damage

- The player never does damage directly. It is a maintenance robot, whose purpose is care (see [story.md](story.md)).
- NPCs are damaged only by designated hazards, such as spikes, pits, lasers and rockets, which the player moves them into or turns on them.
- Bodies aren't damaged by hitting each other or the tiles, however hard. Kinetic damage would be more realistic, but designated hazards give the author control of a room's theme and its solutions, and don't reward smashing every robot in a room first.

## Starter abilities

Run and jump. See [movement.md](movement.md).

## World change: activate robots

- Dimly lit robots that had been background objects light up and become foreground.
- They patrol back and forth, turning round at walls and at anything else solid, including the player.
- New traversal: the player can stand on robots, ride them, and jump off them to reach places.
- They look mean enough that the player expects them to become dangerous.
- A robot is roughly the player's size and has the player's mass.

## World change: activate robot lasers

- All, or almost all, active robots can now shoot lasers at the player or at critters.
- The first armed robot is placed where it can fire at flying critters that continuously fly past it, and hit some.
- The player avoids a shot by jumping, by hiding behind an obstruction, or by being above or below the robot.
- The player can still safely stand on a patrolling robot, so existing routes survive.
- This change might close off some previously accessible areas.

### Lasers as tools

- The player never gets a laser.
- Robots can be baited into breaking blocks with their lasers. This is good for a puzzle or two.
- Some switches can only be activated by a robot's laser.

## Ability: phase

- The player moves at once to the other side of an object, and the object moves too, the other way.
- The target is the entity in reach whose box is nearest a point 0.5 tiles from the player's center in the direction held, or nearest the center itself with none held, so that holding toward an entity favors it (`015 §5.22`). In reach is whatever a sphere around the player's center meets, whose radius is half the player's diagonal, 0.985 tiles, plus a reach of 0.165 tiles, 1.15 in all. Everything touching the player is in reach. It reaches 0.75 tiles beyond the player's sides and 0.25 above and below (`015 §5.16`). Every kind of entity can be phased through (`015 §1.2`, `§1.3`).
- Phase is K or X on the keyboard, X on a gamepad (`015 §1.1`). Holding it with a target in reach, or coming within reach of one while holding it, fixes the target, holds the player and the target together, moving at the velocity of their center of mass, and pauses the game, slowing down to the pause over half a second, showing the target and, with a direction held, arrows for the velocity the player and the target would each leave with, for as long as it is held. Releasing it unpauses and phases in the direction held, or cancels with none (`015 §5.5`, `§5.6`, `§5.7`, `§5.15`).
- While the grab holds them, gravity doesn't pull on the player or the target, so that juggling is more effective. They keep the velocity they had when the aim began, so a pair grabbed while falling drifts down at that speed. Gravity comes back on the release, whether it phases or cancels. Both changes take effect from the engine's step after the tick they are made on (`024 §1.1`, `§1.2`).
- The last direction held still counts for 4 ticks of real time after it is let go of, counting the tick it is let go on, so that letting go of it with phase, or just before, still phases (`018 §3.1`).
- A new aim can't begin until 0.125 seconds of game time after a phase, or 0.25 seconds after a cancel, so that mashing phase can't ride a platform (`015 §5.15`).
- The phase's direction is one of eight, chosen by player input (`015 §2.1`). The player and the target get equal and opposite momentum along it, at the player's speed of 12.7 tiles per second, which either replaces how they were moving or adds to it (`015 §3.2`).
- The player and the target move as a pair, keeping their center of mass. They are sent to the line through it in the direction of the new momentum, the player ahead and the target behind, so each moves in proportion to the other's mass (`015 §5.1`).
- They end far enough apart to clear their boxes in that direction, plus a gap of 0.5 tiles: through a robot, 1.3 tiles apart, center to center, sideways, and 2.3 up or down (`015 §5.1`).
- Each moves straight toward where it is sent, through other entities but not through tiles: where it meets a tile, it slides along it, so it may stop short. The engine pushes apart whatever ends up overlapping. So a phase never fails and never crosses a wall. Phasing down through a robot on the floor swaps them: the player stops on the floor, and the robot is pushed up off it, and thrown up. Phasing sideways through a robot against a wall swaps them too (`015 §5.21`).
- A movable object is thrown backwards, opposite to the player's exit direction. Lighter objects are thrown faster.
- A phase ends a jump's thrust, coyote time and buffered press, and so does cancelling one (`015 §5.21`).
- Try it in the test room "Phase".
- Traversal: phase through objects and NPCs to go faster and reach high places.
- Phasing straight down juggles robots, puts robots on platforms, picks up blocks, and falls through platforms while they can be phased through (`015 §5.18`).
- Combat: throw an NPC into the air, into spikes or into a pit. For example, jump near a spike pit and phase diagonally down through an NPC to lob it up and into the pit.

## Ability: spawn a block

It takes boost's place (`023`). Where it comes in the progression is undecided.

- The player has one block, a see-through ghost of a block, which looks like a companion cube, with the player's mass. Spawn is Z or L on the keyboard, the right trigger on a gamepad, and needs no direction (`023 §1.1`, `§1.3`, `§2.4`).
- Spawning puts the block under the player. It goes at the player's height, as near its center as it fits beside the tiles, and as far straight down from there, through entities, as it can go until it is under the player or meets a tile; then the player moves up through entities by as much as the block fell short, onto it. On the floor, that lifts the player by the block's height; in the open air, the player doesn't move. A block that only just fits, as in a shaft one tile wide, counts as fitting (`023 §2.1`, `024 §2.2`, `§2.3`).
- Where the block doesn't fit beside the tiles, or the player has no room to rise onto it, such as under a ceiling less than 2.8 tiles above the floor, the spawn is cancelled, with phase's cancel sound for now. Spawning can then be tried again after 0.25 seconds of game time (`024 §2.2`).
- Spawning it again takes it from wherever it was. It is never removed for being far from the player (`023 §1.1`, `§1.2`).
- It spawns at rest, or moving as the player does, chosen by the feature `PlayerSpawnMotion` (`023 §2.3`).
- Spawning can't be repeated until 1.5 seconds of game time after the last spawn, which keeps jumping off the block from being flight. Nothing else about the block is limited (`023 §2.5`).
- Traversal: a phase target anywhere, a step, and a shield.
- Puzzles: a puzzle element the player always has, so the player can break puzzles out of their isolation (`023`, Context).
- The block talks: the only thing in the world that can't move by itself, and talks (`023 §1.5`). Not built.

### Upgrade: mirrored block

- Lasers reflect off the block instead of destroying it. The player can hide behind it from a robot's laser, and place it to send the laser into a turret, another robot, or a switch.
- It is what makes the spawned block different from the other blocks, which can have the same mass.
- Combat: one of the player's main ways to do damage without doing it directly.

## Ability: sleep

A possibility, not yet decided.

- The player can tell a robot to sleep, which powers it down. It is a dialogue option, not another button: every robot can in principle talk, and every one gets "Sleep" at every level of its dialogue tree. A robot with nothing else to say gets dialogue anyway, such as "...", with "Sleep" as its only option.
- It fits a maintenance robot, which knows where the off switch is, and the story's tension between collective self-defense and freedom from control: telling a robot to sleep is one robot controlling another.

## World change: activate laser turrets

- Dimly lit turrets that had been background objects light up and become foreground.
- Turrets are stationary. They can be stood on and phased through, and damaged by hazards such as a laser sent back off the mirrored block.
- Laser timing and rules are the same as for robot lasers.

## World change: activate rocket turrets

- Like laser turrets, but they fire rockets, which can be phased through.
- Firing has fixed, repeating timing, so that several phases in a row for great height are consistent. For example, two turrets side by side fire alternately.

## World change: turn on the water

- A giant lake at the top of the map, with waterfalls that feed smaller pools.
- It can be staged: first globally, by opening sluice gates under the giant lake, then in different places.
- Water bends lasers, for refraction puzzles. Some laser switches can't be reached by a laser until the water is on.
- Water pushes every body up in proportion to how much of its box is under water, and drags heavily on it. A body's density, its mass for its volume, decides whether it floats. There is no swimming as such: it comes from these rules and the abilities.
- The player and robots are dense, so they sink. The player walks on the bottom, and drag makes its jumps short and floaty, so a jump off the bottom, even with a jump off the spawned block, gets it little height.
- The spawned block floats, so spawning it under water lifts the player to the surface from any depth. It is the one place where spawning the block climbs without limit, and the surface is the limit. Floating blocks are also the only things to stand on over deep water.
- Carrying something dense sinks the player, and is its only way down through water until the swim drive.
- Deep pools that the player can fall into but not climb out of, and flooded shafts, are gates that the floating block opens.

### Upgrade: swim drive

A late upgrade.

- While under water, the player's drive pushes in any direction held, and top speed emerges from the drive against the water's drag, as on land.
- The floating block only takes the player up through water; the swim drive takes it down and anywhere else.

## World change: activate rocket robots

To be designed.

## Hovering platforms

Where these appear in the progression is undecided.

- A platform moves back and forth along a line between two points, in any direction, set in LDtk (`025 §1.1`). It turns around just before each end, and when something stops it on the way, such as the player: it pushes the player rather than bouncing off, until it stalls (`025 §3.1`, `§5.2`). Nothing carries it, so it slides off the player's head (`025 §5.1`).
- It cancels its own weight with thrust, and drives along its line and holds itself to it with thrust that is limited to half its weight, so it accelerates the same way in every direction, and enough weight overpowers it: it sinks, or can't rise (`025 §2.1`, `§2.2`).
- It sags under load in proportion to the weight it carries, and dips when landed on.
- While a phase's grab holds it, it has no weight to cancel, but still drives along its line and holds itself to it, with the same caps (`024 §1.1`, `025 §2.6`).
- Puzzle material: lower a platform for a while by dropping a heavy object on it, or by adding a rider or two.

## Open questions

Phase:
- What momentum do the player and the target get? `Fixed`: equal and opposite momentum replaces theirs, so every phase leaves them moving the same, which makes juggling easy, but momentum isn't conserved. `Conserved`: it is added to theirs, so momentum is conserved and phases in a row may build speed, but juggling a robot high means meeting it just before the top of its rise. Both are built, chosen by the feature `PlayerPhaseThrow`, and `Fixed` is the default; to be chosen by playing (`015 §3.2`, issue #61).
- Tuning to be checked by playing (issue #61): the reach of 0.165 tiles beyond half the player's diagonal, the cooldowns of 0.125 seconds after a phase and 0.25 after a cancel, the gap of 0.5 tiles, the offset of 0.5 tiles for choosing a target, the speed of 12.7, and the 4 ticks a released direction still counts (`015 §3.1`, `§5.1`, `§5.15`, `§5.22`, `§5.23`, `018 §3.1`).
- Does the cooldown after a phase stay on game time, so that it lengthens as the game slows, or move to real time, as the player's rhythm? Leaning game time; to be checked by playing (`030 §2.3`). The cooldown after a cancel stays on game time, since it keeps the player from hovering by phasing again and again.
- Letting go of a diagonal, or a stick passing neighbouring directions on its way back to the middle, can phase in a direction next to the one meant (`018 §3.2`).
- Should a phase launch the player faster off heavy or immovable targets? That would make turrets give bigger launches in general, and make phasing upward off a robot stronger than phasing sideways.
- Should dead NPCs remain as objects that can be phased through and stood on?
- Phasing through a hovering platform throws the platform and leaves its riders behind. That is built, as [characters.md](characters.md) says every NPC can be phased through, but is only a leaning (`015 §1.3`).

Boost:
- Does boost come back in any form? Damage and swimming no longer need it. A version that tops up the player's speed and bounces is parked on branch `boost-ability` (`023 §3.3`).

The spawned block:
- Does it spawn at rest or moving as the player does? Both are built, chosen by `PlayerSpawnMotion`; to be chosen by playing (`023 §2.3`).
- Tuning to be checked by playing: the cooldown of 1.5 seconds, and the block's mass of 1 (`023 §1.3`, `§2.5`).
- How are puzzles isolated from it? Ideas: make it wider than a tile, so that it can't go into a one-tile shaft, and require room to spawn it; lasers that destroy it when it crosses them, which a clever player may get around (`023 §3.1`, `§3.2`).
- How does the block talk, and what does it say? It accepts whatever life throws at it, or whatever throws it at life. It raises a cue when it is spawned, for whatever reacts (`023 §1.5`).
- Is the mirror an upgrade, or does the block reflect lasers from the start?

Sleep:
- Is it in the game?
- How is it limited, so that it doesn't reward putting every robot in a room to sleep first? Options: a robot must not be attacking the player, so the player approaches from behind or while it is distracted; some robots refuse, perhaps because the majority's hierarchy won't let them; sleep wears off.
- Where does it come in the progression?

Hovering platforms:
- The drive's tuning, its top speed, gain and braking, and the stall's, to be checked by playing (`025 §2.3`, `§5.2`).
- Should a platform's mass come from its width, its `Width` field in LDtk? The caps, being fractions of its weight, would scale with it (`025 §2.2`).

Lasers and water:
- Can robot lasers be designed to add traversal options naturally, beyond breaking blocks and activating switches?
- Do the other blocks float? Floating depends on volume, not mass, so a block of the spawned block's size and mass floats as well. Options: every block floats, which makes them rafts and lifts too, and "only blocks float" a rule to build rooms around; the other blocks are denser, which loses the one-tile jump while carrying one at mass 1, unless they are also smaller; buoyancy is special to the spawned block, which is a special case.
- How does phase behave under water, where drag shortens every throw?
- Do currents, such as below waterfalls, push floating things, for rafts?
- Does water weaken lasers as well as bend them? Probably unimportant, but there could be a puzzle in it.

Structure:
- Besides abilities, world changes and water, what works like doors and keys? Option: actual doors and keys. Option: quests, e.g. "hack A in section B before you can get into C in section D", or "destroy a haywire defense before a door opens".
- What one other ability would have the most interesting interactions with everything above?
