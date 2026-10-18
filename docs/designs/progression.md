# Progression

Relevant decision records:
- [000: Initial design](../decision-records/000-initial-design.md)
- [014: The phase ability](../decision-records/014-phase.md)

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

- The player teleports to the other side of an object.
- The target is the nearest entity whose box is within 1 tile of the player's box. Every kind of entity can be phased through (`014 §1.2`, `§1.3`).
- Phase acts on a press only: Left Shift or J on the keyboard, X on a gamepad (`014 §1.1`).
- The player's momentum is replaced: fixed magnitude, in one of eight directions chosen by player input. With no direction held, the direction is from the player through the target's center, snapped to the nearest of the eight (`014 §2.1`).
- The player's position is replaced: a fixed distance from the object's center, in the direction of the new momentum. The distance, 2.5 tiles, clears every entity in every direction (`014 §2.2`).
- The new speed is 16 tiles per second. Phasing straight up off a robot on the floor reaches a ledge 6 tiles up (`014 §3.1`).
- A phase is blocked if the player's box doesn't fit at the destination, or a tile lies on the line from the target's center to the destination, so a phase never crosses a wall. A blocked phase does nothing at all (`014 §2.3`).
- A movable object is thrown backwards, opposite to the player's exit direction. Lighter objects are thrown farther.
- A phase ends a jump's thrust and coyote time.
- Try it in the test room "Phase".
- Traversal: phase through objects and NPCs to go faster and reach high places.
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
- What momentum does the target receive? Option: the opposite of the player's new momentum, so every phase throws the same, but total momentum is not conserved. Option: the opposite of the player's change in momentum, which is conserved, and makes the throw depend on how the player was moving. To be decided by trying both: both are built, chosen by `PlayerTuning.PhaseThrow`, and the first is the default (`014 §3.2`).
- A blocked phase gives the player no sign that it failed. That waits for effects (`014 §2.3`).
- Tuning to be checked by playing: the reach of 1 tile, the distance of 2.5 tiles and the speed of 16 (`014 §3.1`).
- Phasing straight down through a grounded target puts the player on the floor and sends the target up. What is that good for? Idea: a short fall stuns the target.
- Should a phase launch the player faster off heavy or immovable targets? That would make turrets give bigger launches in general, and make phasing upward off a robot stronger than phasing sideways.
- Should dead NPCs remain as objects that can be phased through and boosted off?
- Phasing through a hovering platform throws the platform and leaves its riders behind. That is built, as [characters.md](characters.md) says every NPC can be phased through, but is only a leaning (`014 §1.3`).

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
