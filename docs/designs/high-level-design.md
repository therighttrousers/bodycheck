# Body Check: high-level design

"Body Check" is a code name. The game is an unnamed 2.5D metroidvania.

Relevant decision records:
- [000: Initial design](../decision-records/000-initial-design.md)
- [001: Godot's 3D physics engine](../decision-records/001-godot-3d-physics.md)
- [002: `BodyCheck.Core` depends on GodotSharp](../decision-records/002-core-depends-on-godotsharp.md)
- [003: Rendering pipeline for the Star Fox SNES look](../decision-records/003-snes-3d-look.md)
- [005: Jolt's joint motors for traction](../decision-records/005-jolt-motors-for-traction.md)

## Vision

The player is a maintenance robot whose job is to activate all of a facility's defenses. The world gets more dangerous as the player progresses, because the facilities and its characters regard the player as an enemy.

## World and story

- Themes are existential: purpose and agency.
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
- Bodies are axis-aligned boxes. Rotation and mass distribution of characters are not modelled.

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
- **Physics:** Godot's 3D physics engine, Jolt, with gameplay locked to the XY plane. The engine detects collisions, resolves contacts and moves bodies. A walker's grip is a motor inside the engine, which our rules set each tick (`005`). Drag is ours, applied on top of the engine each tick. See [movement.md](movement.md#physics-engine).
- **`src/BodyCheck.Core`:** the game's rules, as a class library: bodies, traction, character controllers, tuning, rooms and tiles. It uses Godot's value types, such as vectors, but no nodes, so it runs outside the engine. It references the GodotSharp assembly of the installed engine, so `dev-setup.ps1` has to be run before it builds (`002`).
  - A `Body` is the rules' view of one box. Controllers change its motion only by adding forces and impulses and by setting its traction.
  - `World.Step` turns each body's forces, impulses and drag into one impulse for the engine.
- **`game/`:** the Godot project, which holds the nodes.
  - `BodyNode` is the engine's side of a `Body`. Each tick it copies the engine's position, velocity and contacts into the body, and gives the engine the body's impulse and, for a walker, its traction.
  - `RoomNode` is one room, simulated: tiles, bodies and controllers, with no camera, lights or input of its own, so tests use it as it is.
  - `Main` adds the camera, the lights and the player's input to a `RoomNode`, and switches rooms.
  - `look/` holds the colour pass and the tools for judging the rendering. See [rendering.md](rendering.md).
  - `probes/` holds scripted measurements, run headless with `--probe=` and a name.
- **`tests/BodyCheck.Core.Tests`:** unit tests for the library, in xUnit. They run without the engine.
- **`tests/BodyCheck.Game.Tests`:** tests that need the engine, run inside Godot through gdUnit4Net by plain `dotnet test`. Godot runs headless at a fixed frame rate, so physics ticks faster than real time. The project is a Godot project of its own, whose settings are copied from the game's on every build. It can't load the game's scenes or assets by `res://` path.
- **Levels:** authored in 2D in LDtk, in `game/levels/`, and turned into 3D at load. The LDtk JSON will be read at runtime at first, and moved to an asset pipeline only if the loading code proves too slow, too complex or too fragile. No loader exists yet.
- **Test rooms:** a library of named rooms, described as text. `Rooms.cs` has the ones to play in; `TestRooms.cs` has the ones that tests and probes measure in. Design docs and tests refer to them by name.
- **Camera:** Phantom Camera, to be added. Out-of-the-box tools are preferred.
- **Modelling:** Blender.

## Design docs

- [movement.md](movement.md): how the player and other bodies move, collide and grip.
- [progression.md](progression.md): abilities, world changes, water, in the order the player meets them.
- [characters.md](characters.md): characters that populate the game world.
- [rendering.md](rendering.md): the low-resolution pipeline, the palette, and the tools for judging them.

## Open questions

Story:
- Who are the other characters?
- Do the guardians have to shoot the player, or do they want to? What do they think about it, and how does the player find out?
- Why does the player keep activating defenses that hurt it? What is the facility defending against?

Failure and saving:
- How are saves between the stages of a multi-stage challenge done? Options: explicit save points spread out in space, so the player walks to the next stage; or restoring to the current stage after death.
- Is there damage from falling, and does it apply to NPCs?

World:
- On one seamless map, when do world and NPC scripts run?
- What is the respawn policy for NPCs and other entities; e.g. free-falling blocks?

Tech:
- Phantom Camera is unverified for this project: use from C#, and camera limits in 3D. Its limit, lookahead and newer framed-follow features are described for its 2D camera only.
- Can the Godot editor attach a script to a scene only when the script is in the Godot project itself? Believed so, which is why node classes stay in `game/`.
- gdUnit4Net is used outside its stated support (.NET 8 and 9, Godot 4.4 to 4.5), and `gdUnit4.api` is a prerelease. To be revisited when it releases.
- Would Godot's 3D physics, with gameplay locked to one plane, serve better than our own simulation? To be investigated later.
