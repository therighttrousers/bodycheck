# Progression

Relevant decision records:
- [000: Initial design](../decision-records/000-initial-design.md)
- [015: The phase ability](../decision-records/015-phase.md)

Abilities and world changes, in the order the player meets them.

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
- Robots can be baited into breaking blocks with their lasers. This is good for a puzzle or two before the player gets boost.
- Some switches can only be activated by a robot's laser.

## Ability: phase

- The player moves at once to the other side of an object, and the object moves too, the other way.
- The target is the entity in reach whose box is nearest a point 0.5 tiles from the player's center in the direction held, or nearest the center itself with none held, so that holding toward an entity favors it (`015 §5.22`). In reach is whatever a sphere around the player's center meets, whose radius is half the player's diagonal, 0.985 tiles, plus a reach of 0.165 tiles, 1.15 in all. Everything touching the player is in reach. It reaches 0.75 tiles beyond the player's sides and 0.25 above and below (`015 §5.16`). Every kind of entity can be phased through (`015 §1.2`, `§1.3`).
- Phase is K or X on the keyboard, X on a gamepad (`015 §1.1`). Holding it with a target in reach, or coming within reach of one while holding it, fixes the target, holds the player and the target together, moving at the velocity of their center of mass, and pauses the game, slowing down to the pause over half a second, showing the target and, with a direction held, arrows for the velocity the player and the target would each leave with, for as long as it is held. Releasing it unpauses and phases in the direction held, or cancels with none (`015 §5.5`, `§5.6`, `§5.7`, `§5.15`).
- A new aim can't begin until 0.5 seconds of game time after the last began, so that mashing phase can't ride a platform (`015 §5.15`).
- The phase's direction is one of eight, chosen by player input (`015 §2.1`). The player and the target get equal and opposite momentum along it, at the player's speed of 12.7 tiles per second, which either replaces how they were moving or adds to it (`015 §3.2`).
- The player and the target move as a pair, keeping their center of mass. They are sent to the line through it in the direction of the new momentum, the player ahead and the target behind, so each moves in proportion to the other's mass (`015 §5.1`).
- They end far enough apart to clear their boxes in that direction, plus a gap of 0.5 tiles: through a robot, 1.3 tiles apart, center to center, sideways, and 2.3 up or down (`015 §5.1`).
- Each moves straight toward where it is sent, through other entities but not through tiles: where it meets a tile, it slides along it, so it may stop short. The engine pushes apart whatever ends up overlapping. So a phase never fails and never crosses a wall. Phasing down through a robot on the floor swaps them: the player stops on the floor, and the robot is pushed up off it, and thrown up. Phasing sideways through a robot against a wall swaps them too (`015 §5.21`).
- A movable object is thrown backwards, opposite to the player's exit direction. Lighter objects are thrown faster.
- A phase ends a jump's thrust and coyote time, and so does cancelling one (`015 §5.21`).
- Try it in the test room "Phase".
- Traversal: phase through objects and NPCs to go faster and reach high places.
- Phasing straight down juggles robots, puts robots on platforms, picks up blocks, and falls through platforms while they can be phased through (`015 §5.18`).
- Combat: throw an NPC into the air, into spikes or into a pit. For example, jump near a spike pit and phase diagonally down through an NPC to lob it up and into the pit.

## World change: activate laser turrets

- Dimly lit turrets that had been background objects light up and become foreground.
- Turrets are stationary. They can be stood on, phased through, and damaged with boost.
- Laser timing and rules are the same as for robot lasers.

## Ability: boost

- The player briefly speeds up and bounces off an object or NPC, doing damage.
- Knockback works like a collision between billiard balls, with some of the energy lost to damage.
- Traversal: break breakable tiles, and bounce off objects and NPCs to reach high places.
- Combat: the player does kinetic damage.
- Boost is NOT limited by a refresh, such as "one boost in the air, refreshed on landing".

## World change: activate rocket turrets

- Like laser turrets, but they fire rockets, which can be phased through.
- Firing has fixed, repeating timing, so that several phases in a row for great height are consistent. For example, two turrets side by side fire alternately.

## World change: turn on the water

- A giant lake at the top of the map, with waterfalls that feed smaller pools.
- It can be staged: first globally, by opening sluice gates under the giant lake, then in different places.
- Water bends lasers, for refraction puzzles. Some laser switches can't be reached by a laser until the water is on.

## World change: activate rocket robots

To be designed.

## Hovering platforms

Where these appear in the progression is undecided.

- A platform holds its height with thrust and patrols horizontally.
- It sags under load in proportion to the weight it carries, and dips when landed on.
- Its thrust is limited, so enough weight makes it sink.
- Puzzle material: lower a platform for a while by dropping a heavy object on it, or by adding a rider or two.

## Open questions

Phase:
- What momentum do the player and the target get? `Fixed`: equal and opposite momentum replaces theirs, so every phase leaves them moving the same, which makes juggling easy, but momentum isn't conserved. `Conserved`: it is added to theirs, so momentum is conserved and phases in a row may build speed, but juggling a robot high means meeting it just before the top of its rise. Both are built, chosen by `PlayerTuning.PhaseThrow`, and `Fixed` is the default; to be chosen by playing (`015 §3.2`, issue #61).
- Tuning to be checked by playing (issue #61): the reach of 0.165 tiles beyond half the player's diagonal, the cooldown of 0.5 seconds, the gap of 0.5 tiles, the offset of 0.5 tiles for choosing a target, and the speed of 12.7 (`015 §3.1`, `§5.1`, `§5.22`, `§5.23`).
- Should a phase launch the player faster off heavy or immovable targets? That would make turrets give bigger launches in general, and make phasing upward off a robot stronger than phasing sideways.
- Should dead NPCs remain as objects that can be phased through and boosted off?
- Phasing through a hovering platform throws the platform and leaves its riders behind. That is built, as [characters.md](characters.md) says every NPC can be phased through, but is only a leaning (`015 §1.3`).

Boost:
- How is boost kept from becoming flight? Option: a long enough cooldown. Option: boost must always be against a designated object.
- Does boost double as swimming? It could, if water is like air with higher resistance and some buoyancy. This is the current leaning.
- Should boosting into spiky NPCs be safe through invulnerability frames? Options: always, never, or as a later upgrade.

Lasers and water:
- Can robot lasers be designed to add traversal options naturally, beyond breaking blocks and activating switches?
- What else does water do for traversal, and how does it interact with the abilities?
- Does water weaken lasers as well as bend them? Probably unimportant, but there could be a puzzle in it.

Structure:
- Besides abilities, world changes and water, what works like doors and keys? Option: actual doors and keys. Option: quests, e.g. "hack A in section B before you can get into C in section D", or "destroy a haywire defense before a door opens".
- What one other ability would have the most interesting interactions with everything above?
