# 061: Environments per room

Status: closed. The default blend's width (§3.3) is in [`game/main/`'s doc](../../game/main/architecture.md)'s open questions, and fading background layers (§5.1) in [rendering.md](../designs/rendering.md)'s, for issue #202.

## Context

Issue #203 asks for an environment per room: its lighting and its sky. Issue #202, background layers, is to follow. Before this, `Main` built one `DirectionalLight3D` and one `Environment` in code for every world.

Neil had already:
- added an `Environment` enum field to every LDtk level, with the values `Default` and `Outside`;
- moved the light and the environment out of `Main` into scenes, `assets/environments/default.tscn` and `outside.tscn`, so that each can be made and tuned as art in the editor, with its own lighting, and loaded the `Default` one in `WorldNode`'s constructor through `IAssetLibrary`.

Goals:
- Environments are scenes, edited in the Godot editor; as little as possible is in C#.
- Changing rooms changes the environment, smoothly rather than at once.

## 1. Environments as scenes

- [x] 1.1 What a scene holds. Decision (Neil): a `WorldEnvironment` named `WorldEnvironment` and a `Node3D` named `Lighting`, holding the lights, so that code finds them by name. Anything else in the scene, such as background layers later, comes along.
- [x] 1.2 Post effects. SSR, SSAO, SSIL and glow are switched on in code, by `AssetLibrary`, for every environment. Decision (Neil): so that nobody has to remember to tick exactly those boxes when making a new environment. SSIL is new with this change.
- [x] 1.3 Backgrounds. Godot can't blend a sky into a colour background. Decision (Neil): an environment's background is a `ProceduralSkyMaterial` or a colour, which is a procedural sky of that colour throughout, so the mixed environment always has a procedural sky. Anything fancier is for background layers (#202).

## 2. Rooms and their environments

- [x] 2.1 Where the enum lives. It was in Simulation, but Levels reads it, and Levels uses only WorldData. Decision: `RoomEnvironment`, in WorldData, beside `TileIndex`, as `Room.Environment`. Text rooms are `Default`.
- [x] 2.2 Reading it. The reader reads each level's `Environment` field by name, as it reads an entity's enums, and rejects a missing field or an unknown name.
- [x] 2.3 Which rooms are near a point. Nothing at run time knew which room a point is in (`013 §1.1`). Neil wanted the rooms near a point, several at once, within a distance. Decision: `World.RoomsNear`, with each room's signed distance from the point: below 0 inside the room, by how far the point is from its nearest edge, so that a blend can weigh by it. The signed distance is `Boxes.SignedDistance` in Maths, beside `Boxes.Distance`, so WorldData now uses Maths.

## 3. Changing environments smoothly

- [x] 3.1 Options:
  - A. Swap at once on entering a room.
  - B. A timed crossfade, about a second, on entering a room. It needs state; bouncing on an edge, respawns and switching worlds become special cases; and it ignores where the camera is.
  - C. A palette fade through black, the classic. It's too much for rooms crossed often, though it may suit a script's action for doors or story beats later.
  - D. Draw both environments and crossfade the images: faithful, but twice the cost to draw.
  - E. Blend by position: weigh each room by how far the view's centre is inside it, in a band across its edges. Nothing is kept from frame to frame, so going back undoes it exactly, and F5, respawns and switching worlds need nothing special. Bouncing on an edge can't flicker.

  Decision (Neil): E.
- [x] 3.2 The point. Decision: the camera's position in the world's plane, which is the view's centre, rather than the player's: the look follows what is on screen, and the framing's spring already smooths it, so the blend eases with the camera.
- [~] 3.3 The weights. Each room weighs `clamp(0.5 - d / w, 0, 1)` for signed distance `d` and blend width `w`, so two rooms that meet weigh a half each on their edge; an environment weighs what its rooms do, and the weights are scaled to add up to 1. With no room within `w / 2`, the nearest room's environment is the look. Leaning: `w` of 6 tiles, chosen without play; to tune by eye.
- [x] 3.4 A width per room. Neil asked for one. Each room weighs by its own width, so two rooms that meet still weigh a half each on their edge, and the band is lopsided where their widths differ: a long fade from open country, a sharp one through a doorway, which is about the room's shape, so per room rather than per environment. Decision (Neil): a level field, `EnvironmentBlend`, a float in tiles, not negative, and 0 must work: no band, a step at the edge, for rooms whose sky background layers cover. A wide blend on a small room tints its neighbours' entrances, so it's for the level's author to judge. 6 tiles is the default, and §3.3's weight is each room's.

## 4. Mixing

- [x] 4.1 Lights. Every environment's `Lighting` stays in the tree, each light's energy scaled by its environment's weight: light adds, so this is how two rooms' lights would combine. A scene with no weight is hidden, so its shadows cost nothing.
- [x] 4.2 The environment. One `Environment` of the mixer's own, on the only `WorldEnvironment`, copied from the first environment, with its sky replaced by one `ProceduralSkyMaterial` of its own. Both are blended property by property, through Godot's property list, for the properties in which the environments differ: floats, colours and vectors by their weights, and everything else, such as switches, choices and resources, from the heaviest, so it changes where the weights cross. It is generic, so that whatever an artist changes in an environment blends with no code. Only what has changed is set, and only when the weights change.
- [x] 4.3 Where it lives. Neil had it in `WorldNode`, through `IAssetLibrary`. Claude moved it into the game, as `EnvironmentMixer`, which `Main` adds to each world: it needs the camera, which is the game's, and Simulation's doc says the world has no lights for the scene, so tests and probes build worlds with no environment. Neil plans to have the world's own lights reveal the map, the shroud, turned on as the player moves through the facility; those are the world's state, unlike the environment's lighting, so Simulation's doc needs clarifying then. Decision (Neil): the game.

## 5. Background layers (#202)

- [ ] 5.1 Each environment's scene could hold its own layers, which then show and hide with it. To fade them with the weights, Claude suggested a dissolve in the colour pass's 2x2 Bayer pattern rather than transparency: it suits Mode X, is cheap, and has no sorting problems.
