# 023: The player's spawned block

## Context

Inciting event (2026-10-22): boost, as built on branch `boost-ability`, was underwhelming. Added to the player's momentum in any direction, it let a jump plus a boost reach a ridiculous height, built speed over time, and by itself barely moved the player. Changed to top up the player's speed along the direction held, with the engine's bounce for a while after, it worked as designed, but Neil didn't like it more. Phase is the game's most interesting ability, and he would rather have one that beats it in interest or makes it better. Boost is parked on that branch.

Phase's main limit for traversal is that it needs an entity: none may be near a high passage, or the only one may be too dangerous to get close to. Neil's choice of replacement, from a brainstorm: a block the player spawns, to phase through, stand on, hide behind and carry. It looks useless, and with phase it may be overpowered.

Puzzle isolation: the game has none yet, and will need it. It is easy to move an entity far from where it started, so it is hard to make a puzzle that needs a fresh target. Neil wants to isolate puzzles by gameplay rules and level design rather than by limits on distance or triggers that remove things, though heavy-handed ways may still be needed. He wants to take The Talos Principle's approach: some isolation the player can get around with effort and understanding, and rewards for doing it. So there will be absolute isolation (known to be impossible to get around while Godot and Jolt behave), strong (believed impossible), and weak (takes effort, understanding and persistence). A block the player spawns lets the player break puzzles it has already solved.

Neil's constraints: at most one block at a time; a limit in space rather than time, so that taking time to think isn't punished; one button, with no direction, since his laptop's keyboard doesn't register Z with up and a side held.

## 1. The block

- [x] 1.1 How many? Decision (Neil): one. Spawning it again takes it from wherever it was. Maybe two later, though he doubts it.
  - As built: `WorldNode` builds the one block with the world, disabled and hidden, which takes it out of the engine's space, and gives it to the player's controller. Spawning enables and shows it. Being one entity for the world's whole life, it needs no code to create or free entities while the world runs.
- [x] 1.2 Is it removed when it is far from the player? Decision (Neil): no. With at most one, the player can't lose it, and removing it would undo what the player built in a puzzle.
- [x] 1.3 Its body. Decision (Neil): a block's size, with mass 1, the player's, to start with. The existing blocks are cumbersome to carry; this one should be less so. To be tuned by playing.
- [x] 1.4 Its looks. As built: the same model as a block, a cube like Portal's companion cube without its markings.
- [~] 1.5 Neil: the block talks. It is the only thing in the world that can't move by itself, and talks; even doors open and close. It has to accept whatever life throws at it, or whatever throws it at life. Not built: there is no speech yet. The block raises a cue when it is spawned, for whatever reacts.

## 2. Spawning

- [x] 2.1 Where does it appear? Decision (Neil): always under the player, so that spawning needs no direction.
- [x] 2.2 How does it make room? Options: (a) check first that the player and the block fit, 1 tile by 2.8, and fail if not; (b) Neil: move the block through entities from the player's center downward, without a check. Decision (Neil): (b). The check would be complicated to predict, and two-tile-high tunnels, where (b) would leave them overlapping, will be rare, since they are hard to traverse while carrying and keep the player from jumping.
  - Measured (2026-10-22): on the floor, the block stopped at the floor, inside the player's feet, and the engine never pushed them apart. The block is wider than the player, so their least overlap is sideways, and any push would have been sideways too.
  - As built: then the player moves up through entities by as much as the block fell short, onto it. In the open air that is nothing, and on the floor it is the block's height. Either move stops at a tile.
- [x] 2.3 How does it move when spawned? Options: at rest, so a player in the air lands on it and pushes it down; or with the player, so a player in the air stands on it at once and can jump off it, a double jump that leaves a block behind. Decision (Neil): build both, start at rest, and choose by playing. The feature `PlayerSpawnMotion`.
- [x] 2.4 The button. Decision (Neil): Z, boost's, which becomes `spawn`. Also L, and the right trigger on a gamepad.
- [x] 2.5 How is it kept from becoming flight? Decision (Neil): a cooldown on spawning, which may be long, since it isn't a movement ability. It limits spawning only, not phasing through the block or jumping off it, unless playing shows they need limiting too. The feature `PlayerSpawnCooldownTime`, in game time.
  - Measured (2026-10-22), with probes that played the obvious strategies for 12 seconds, starting from the floor:
    - Jump, holding it for a full jump; spawn as soon as the player falls; jump off the block on landing; repeat. At rest, with a cooldown of 1 second, the player climbed about 1.75 tiles a second without end: flight. With 1.25 seconds or more, the best was about 9.9 tiles. With the player's motion and 1 second, the best was 9.4.
    - Jump; spawn as soon as the player falls; phase up through the block; repeat. At rest, with cooldowns from 1 to 2.5 seconds, the best was 7.3 to 8.3 tiles. Nothing paused the game for the aim.
  - As built: 1.5 seconds, for a margin over 1.25.

## 3. Open

- [ ] 3.1 Puzzle isolation, by the block's size: a block wider than a tile can't go into a one-tile shaft, and, if spawning needed room, couldn't be spawned in one. Raised by Claude; not decided.
- [ ] 3.2 Puzzle isolation, by lasers that destroy the block when it crosses them, which a clever player may get around by hiding behind it or baiting a robot. Raised by Claude; not decided.
- [ ] 3.3 Does boost come back, in some form, or does the block replace it?
