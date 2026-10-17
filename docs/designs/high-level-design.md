# Body Check: high-level design

"Body Check" is a code name. The game is an unnamed 2.5D metroidvania.

Relevant decision records:
- [000: Initial design](../decision-records/000-initial-design.md)
- [001: Godot's 3D physics engine](../decision-records/001-godot-3d-physics.md)
- [003: Rendering pipeline for the Star Fox SNES look](../decision-records/003-snes-3d-look.md)
- [005: Jolt's joint motors for traction](../decision-records/005-jolt-motors-for-traction.md)

## Detailed design docs

- [architecture.md](architecture.md): the tools, the code's structure, and the tests.
- [movement.md](movement.md): how the player and other bodies move, collide and grip.
- [progression.md](progression.md): abilities, world changes, water, in the order the player meets them.
- [rendering.md](rendering.md): the low-resolution pipeline, the palette, and the tools for judging them.
- [models.md](models.md): what a 3D model must be, how the game places it, and the animation interface between code and art.
- [story.md](story.md): story introduction, themes, tensions, setting, story beats, climax, and dialogue guidance.
- [characters.md](characters.md): characters that populate the game world.

## Vision

The player is a maintenance robot who has been asked by a mysterious Voice to repair and reactivate a facility. The world gets more dangerous as the player progresses, because the facility and its inhabitants regard the player as an enemy.

## World

- The whole game is one seamless, tile-based map, with no screen transitions.
- The map is divided into sections, each with its own look and personality.
- Everything that moves is intelligent.
- There are no *enemies* per se - just characters, many of which are dangerous.
- NPCs: robots, turrets, heavy projectiles, platforms.
- NPCs don't always control their actions.
- Some NPCs are willing to converse.

## Art direction

- 3D models, 3D tiles and 3D rendering, drawn at low resolution so that it looks a lot like pixel art (and might contain some).
- Solid geometry and interesting lighting matter more than texturing: physically based lighting, reflection, refraction.
- Inspiration: Tron.
- The target look is mid-1990s 3D games such as Star Fox, Quake and Descent, but with modern rendering features: anti-aliasing, shadows, transparency, ambient occlusion, refraction, reflection, subsurface scattering. It should look like something that would have astonished a teenager in the mid-1990s, while still passing as somewhat believable mid-1990s technology.
- The image is Mode X: 320x240 and 256 colours, with dither. See [rendering.md](rendering.md).
- Art is basic shapes until the mechanics are proven.

## Principles

### Experimentation and failure have low cost

- The player heals by standing still, after a delay.
- Save points are frequent. Quitting saves at the last save point, with no loss of world state or inventory.
- Death restores everything. There are no resource losses, and no ghosts to pick up or kill.
- Runbacks to a challenge are always short, and need only light use of the abilities the challenge needs.
- Multi-stage combat challenges have saves between stages.
- Frequent failure is acceptable as long as it is cheap and not too frequent.
  - This is to be watched in playtesting, because progression makes traversal harder.
- Stretch goal: a few challenges in which failure has a benefit.
  - Requires some change to persist through the restore after death.

### No contact damage by default

- Objects and NPCs that do contact damage always show it, e.g. with red spikes.
- Some do contact damage from one direction only, e.g. an NPC that must be hit with a boost from behind, or after a duck and slide.

### Characters can't occupy the same space

- Passing through any NPC requires using a special ability.
- Bodies collide without bouncing and share momentum, so each can push the other. Boost adds the bounce.
- The player can stand on NPCs and be carried by them.
- NPCs can stand on and be carried by the player.

### Simple Newtonian movement and combat

- Movement comes from forces and impulses: gravity, drive, friction, drag. Top speed emerges from drive against drag.
- Momentum is properly tracked. A feel cheat is always a force or an impulse, never a direct edit to velocity.
  - Contact with immovable objects is an exception; e.g. ground tiles absorb momentum without moving.
- A force between two bodies pushes both. Jumping off a robot pushes it down; running on one pushes it back.
- Mass matters: phase throws lighter NPCs farther, and boost knockback depends on the masses of both.
- Bodies are boxes. Characters are axis-aligned: their rotation and mass distribution are not modelled. Blocks can rotate in the plane of play, so they tip off ledges and spin when run on.

See [movement.md](movement.md).

### Most progression happens by changing the world

- World changes often use the player's abilities, and often make the world more dangerous.
- Progression often opens new ways to traverse, as in almost all metroidvanias, and often makes traversal harder, unlike almost all of them.
- Risk: low variety. It is too easy for a world change to be just "a new thing appears to phase through or boost off".
- Risk: poor telegraphing of future routes. Something has to say "you can eventually get way up there".

See [progression.md](progression.md).

## Architecture

- **Engine:** Godot 4.7.2 (.NET), with C# on .NET 10, for desktop.
- **Physics:** Godot's 3D physics engine, Jolt, with gameplay locked to the XY plane. The engine moves bodies; our rules push them with forces, impulses and motors.
- **Code:** the game's rules are a class library, `BodyCheck.Core`; the scenes and other nodes are the Godot project in `game/`.
- **Content:** levels are authored in 2D in LDtk and turned into 3D at load; models are made in Blender.

See [architecture.md](architecture.md).

## Open questions

Failure and saving:
- How are saves between the stages of a multi-stage challenge done? Options: explicit save points spread out in space, so the player walks to the next stage; or restoring to the current stage after death.
- Is there damage from falling, and does it apply to NPCs?

World:
- On one seamless map, when do world and NPC scripts run?
- What is the respawn policy for NPCs and other entities; e.g. free-falling blocks?
