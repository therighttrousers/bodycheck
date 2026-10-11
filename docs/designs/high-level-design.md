# Body Check: high-level design

"Body Check" is a code name. The game is an unnamed 2.5D metroidvania.

Relevant decision records:
- [000: Initial design](../decision-records/000-initial-design.md)

## Vision

The player is a maintenance robot whose job is to activate all of a facility's defenses. The world gets more dangerous as the player progresses, because the facilities and its characters regard the player as an enemy.

## Story

- The guardian robots are intelligent.
- Themes are existential: purpose and agency.
- Enemies are robots and turrets, and the critters that the defenses fire at.

## Art direction

- 3D models, 3D tiles and 3D rendering, drawn at low resolution so that it looks a lot like pixel art (and might contain some).
- Solid geometry and interesting lighting matter more than texturing: physically based lighting, reflection, refraction.
- Inspiration: Tron.
- The target look is Star Fox era SNES 3D with modern rendering features: anti-aliasing, shadows, transparency, ambient occlusion, refraction, reflection, subsurface scattering. It should look like something that would have astonished a teenager in 1993, while still passing as somewhat believable 1993 technology.
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

- Objects and enemies that do contact damage always show it, e.g. with red spikes.
- Some do contact damage from one direction only, e.g. an enemy that must be hit with a boost from behind, or after a duck and slide.

### Player and enemies can't occupy the same space

- Passing through any enemy requires using a special ability.
- Bodies collide without bouncing and share momentum, so each can push the other. Boost adds the bounce.
- The player can stand on enemies, and is carried by them.
- Enemies can stand on and be carried by the player.

### Simple Newtonian movement and combat

- Movement comes from forces and impulses: gravity, drive, friction, drag. Top speed emerges from drive against drag.
- Momentum is properly tracked. A feel cheat is always a force or an impulse, never a direct edit to velocity.
  - Contact with immovable objects is an exception; e.g. ground tiles absorb momentum without moving.
- A force between two bodies pushes both. Jumping off a robot pushes it down; running on one pushes it back.
- Mass matters: phase throws lighter enemies farther, and boost knockback depends on the masses of both.
- Bodies are axis-aligned boxes. Rotation and mass distribution of players, enemies and projectiles are not modelled.

See [movement.md](movement.md).

### Most progression happens by changing the world

- World changes often use the player's abilities, and often make the world more dangerous.
- Progression often opens new ways to traverse, as in almost all metroidvanias, and often makes traversal harder, unlike almost all of them.
- Risk: low variety. It is too easy for a world change to be just "a new thing appears to phase through or boost off".
- Risk: poor telegraphing of future routes. Something has to say "you can eventually get way up there".

See [progression.md](progression.md).

## World

- The whole game is one seamless, tile-based map, with no screen transitions.
- The map is divided into sections, each with its own look and personality.

## Architecture

- **Engine:** Godot 4.7.2, .NET build, Forward+ renderer, desktop only. The engine is installed into the repository by a script and is not on the PATH.
- **Language:** C# on .NET 10.
- **`src/BodyCheck.Core`:** the simulation and the game rules, as a plain class library with no Godot dependency. It owns bodies, collisions, traction, character controllers and rooms. It runs on a fixed tick and is deterministic.
- **`tests/BodyCheck.Core.Tests`:** unit tests for the library. Coverage is deliberately light for now: mechanisms and stated design goals.
- **`game/`:** the Godot project. It reads input, steps the simulation once per physics tick, and draws the result in 3D. Each node copies its body's position; Godot's own physics is not used.
- **Levels:** authored in 2D in LDtk, in `game/levels/`, and turned into 3D at load. The LDtk JSON will be read at runtime at first, and moved to an asset pipeline only if the loading code proves too slow, too complex or too fragile. No loader exists yet.
- **Test rooms:** a library of named rooms in `Rooms.cs`, described as text. Design docs and tests refer to them by name.
- **Camera:** Phantom Camera, to be added. Out-of-the-box tools are preferred.
- **Modelling:** Blender.

## Design docs

- [movement.md](movement.md): how the player and other bodies move, collide and grip.
- [progression.md](progression.md): abilities, world changes, lasers and water, in the order the player meets them.

## Open questions

Story:
- Who are the other characters?
- Do the guardians have to shoot the player, or do they want to? What do they think about it, and how does the player find out?
- Why does the player keep activating defenses that hurt it? What is the facility defending against?

Failure and saving:
- How are saves between the stages of a multi-stage challenge done? Options: explicit save points spread out in space, so the player walks to the next stage; or restoring to the current stage after death.
- Is there damage from falling, and does it apply to enemies?

World:
- On one seamless map, when do world and enemy scripts run?
- What is the respawn policy for enemies, characters, and other entities?

Tech:
- Low-resolution rendering is untried. First to try: render at twice the target size (e.g. 640x480), average 2x2 blocks down to the target (e.g. 320x240), quantize to a fixed palette or a fixed number of bits per color channel, then upscale with nearest-neighbour. Hardware anti-aliasing may be a better way to get the first two steps.
- Phantom Camera is unverified for this project: use from C#, and camera limits in 3D. Its limit, lookahead and newer framed-follow features are described for its 2D camera only.
- Would Godot's 3D physics, with gameplay locked to one plane, serve better than our own simulation? To be investigated later.
