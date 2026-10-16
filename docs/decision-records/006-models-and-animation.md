# 006: Models, their base point, and animation

## Context

Inciting event (2026-10-15): issue #19, on branch `initial-models-animation`. Liberally licensed placeholder models replace the box meshes: Bob the Robot for the player, a Dalek for robots and a Portal cube for blocks, each scaled to the entity it draws. Each is a glTF file with a wrapper scene, `scene.tscn`, beside it in `game/assets/`. The player is animated, and it turns to face the way it runs; robots turn.

Goal: decide early what the asset pipeline will ask of a model, before anyone models for it. Neil's son, who may make the 3D characters, is used to modelling Roblox characters. Neil's daughter may make the 2D art.

Closed on 2026-10-16. Outcomes and unresolved items are in the design docs: [models.md](../designs/models.md) and [architecture.md](../designs/architecture.md).

Overarching questions:
- Where is a model's base, the point that sits on the ground? It should be standard, and easy for an artist to set or change in Blender or the Godot editor.
- How do the body's collision shape and its model line up, without `HalfSize.Y` offsets all over the code?
- What is the interface between code and art for animation?

Tensions:
- Physics wants a body's origin at its centre. Art wants a model's origin at its feet.
- `005 §3.3` hands looks to the entity, because the test projects can't see the game's files. `RoomNode` now loads models itself, and tests build a `RoomNode` (item 5.1).

## 1. Where a model's base is

- [x] 1.1 What does a model ask of the artist? Decision: the model's origin is its base, where it stands on the ground: between the feet for a character, the middle of the bottom face for a block.
  - This was already the convention when the item was raised, and it is the usual one: Unity, Unreal, Mixamo and most Blender-to-engine workflows put a character's origin on the ground between its feet.
  - Godot has no separate pivot property. A node's origin is its pivot, so the base is the origin of the scene that the game instantiates.
- [x] 1.2 How Roblox does it, for comparison. Roblox doesn't take the base from the mesh.
  - A character is a Model with a `Humanoid` and a `HumanoidRootPart`: an invisible part at the torso, which is the model's PrimaryPart and the root of its rig.
  - `Humanoid.HipHeight` is the distance from the ground to the root part when the humanoid is standing. For an R15 rig the root part's centre stands at `0.5 * RootPart.Size.Y + HipHeight`, and the legs aren't used. For R6 it is an offset on top of the legs' height. Checked in Roblox's `Humanoid` reference, 2026-10-15.
  - So Roblox's physics root is at the middle of the body, and the ground comes from separate numbers. Ours is the same, with `HalfSize.Y` in place of half the root part's height plus the hip height (item 2.1).
  - Studio sets a model's pivot by hand with its Pivot Editor; that is the part of Roblox an artist would compare item 1.3 with.
- [x] 1.3 Where the artist adjusts a model that doesn't have its base at its origin. Decision (Neil, 2026-10-16), after seeing the demonstration below: in the wrapper scene, whose root is a plain `Node3D` with no transform, and whose child is the imported glTF. The artist moves, scales and turns the child until the base is on the root's origin and the model faces +Z. Code moves and turns only the root.
  - Today `portal_cube/scene.tscn` puts its scale (0.25) and offset on the root, the node that code turns. Godot keeps a node's scale and position when its `Rotation` is set, so it works, but the artist and the code share one transform.
  - The child is an instance of the glTF scene, so reimporting the glTF keeps the wrapper's edits.
  - A model made for this game has its base at its origin in Blender (item 1.1) and needs no adjustment. The wrapper is for models from elsewhere.
  - The glTF importer's `nodes/root_scale` also scales a model (Bob is imported at 0.85). Whether scale belongs there or in the wrapper is not settled.
  - 2026-10-16: the Portal cube's wrapper was rebuilt this way so that Neil could see it, with the root named `PortalCube` and the child `Model`. Loaded headless, the root is at identity and the cube's mesh spans y = 0.0025 to 1.164, so its base is on the origin. Bob's wrapper is unchanged.
  - Why the layout matters: the child's transform applies before the root's, so the game's turn is about the root's origin, the base, whatever the glTF's own origin was. With one node, Godot applies scale, then rotation, then translation, so the turn was about the glTF's origin and the artist's offset was added after it. For the cube the two agree, because its glTF origin is on the vertical axis through its base.
  - A wrapper with this layout can't be made with **New Inherited Scene** on the glTF, whose root is the glTF's own root. It is a new scene with the glTF instanced as a child. Neil rebuilt Bob that way as `joe_the_robot` and found one order that matters: **Editable Children** has to be on before the `AnimationTree` is added, or its `AnimationPlayer` can't be picked.

## 2. Lining up the body and its model

- [x] 2.1 Where is an entity's origin? Decision (Neil, 2026-10-15): at the centre of its body, as before. The model is lowered by `HalfSize.Y` once, in `Entity`'s constructor.
  - First built the other way: the entity's origin at its base, with the collision shape raised by `HalfSize.Y`. The offset then appeared wherever the code meant the body: the collision shape, the box visuals, the player's light, and every test and probe that measures a body's bottom or top from its position. Those tests and probes weren't updated, so they measured the wrong thing.
  - At the centre, the box shape needs no offset, the engine's automatic centre of mass is at the origin, and the tests and probes are right as they were. Only a model wants its origin at its base, so the conversion is in the one place that attaches a model. `RoomNode` still converts a level's start position, which is the middle of an entity's base, to the entity's centre.
  - Neil rewrote the branch's commit to do it this way, and noticed that platforms had been drawn too high in the first version.

## 3. Facing

- [x] 3.1 How does a walker turn to face the way it moves? Decision (Neil, 2026-10-16): the model turns, the body doesn't. Each tick its yaw moves towards ±90° at a fixed rate with `Mathf.MoveToward`, so a model facing +Z (towards the camera) turns through the camera to face +X or -X.
  - Considered: a motor on the body's yaw, so that stacked walkers turn each other through friction. It would need yaw unlocked on bodies that are otherwise locked to the plane, and a cylinder in place of the box, whose corners would otherwise swing into Z. Jolt rounds a convex shape's edges, so the corner and seam probes would have to be run again. Whether riders should be turned by what they stand on is itself undecided.
  - The player's controller and the robot's controller each had a copy of the turning code. Item 4.3 made it one, in `WalkerEntity`.
  - As built, a controller sets `WalkerEntity.Facing` to 1, -1 or 0, and 0 turns the model to face the camera. The player's controller sets 0 when the player is idle on the ground. `characters.md` requires a robot to face the player before it targets them, not the other way round, so the player's third value doesn't affect targeting.

## 4. Animation

The engine's pieces, for reference:
- An `AnimationPlayer` holds clips, such as `bob_rig|idle`, `bob_rig|run` and `bob_rig|jump`, which the glTF importer creates from the file's animations. It plays one clip at a time, and can cross-fade between two.
- An `AnimationTree` decides what an `AnimationPlayer`'s clips do. Its root is a graph of nodes: a state machine, whose states are clips or other graphs and whose transitions are conditions or travel requests; a blend space, which mixes clips by a number, such as speed; a time scale, which speeds a clip up or slows it down. It is built in the editor's AnimationTree panel, and the graph is saved in the scene.
- The graph's inputs are **parameters**, such as `parameters/conditions/on_ground` or `parameters/Run/blend_position`. Code sets them with `Set`, and the tree does the rest each frame.

- [x] 4.1 What is the interface between code and art? Decision (Neil, 2026-10-16), as built: the parameters of an `AnimationTree` in each character's wrapper scene. Code sets a few parameters each tick; the artist builds the graph, picks the clips and sets the blend times in the editor.
  - Today `PlayerController` plays clips by name, sets the playback speed, and checks that the visual has an `AnimationPlayer`. A model whose clips have other names, such as the Dalek's, can't be animated without changing the code.
  - With a tree, the clip names are in the scene, and two models with different rigs can share the code if they have the same parameters.
  - To decide: the names and meanings of the parameters. A first guess, from what the player plays today: `on_ground` (bool), `run_speed` (ground-relative speed, in tiles per second), `jumping` (bool, during the jump's thrust).
  - As built: transitions are advance expressions over the entity's properties (item 4.2). `WalkerEntity` publishes `IsGrounded`, `IsMoving`, `IsJumping`, `Facing` and `GroundSpeed`, beside `Entity`'s own, such as `OnGround`. Code sets two parameters by name, a blend space's position and a time scale, both the absolute ground speed in tiles per second. The list is in [models.md](../designs/models.md#animation).
  - Bob's tree: a state machine of `Idle`, `Move`, `Jump` and `InAir`, where `Move` blends idle, walk and run by speed (at 0, 4 and 15 tiles per second) before a time scale. A first version with separate `Walk` and `Run` states needed 16 transitions; the blend space replaced them.
- [x] 4.2 Does the code set the parameters by name, or does the scene have a script that takes typed values? Setting by name has no C# wrapper and no second source for the engine's state, but a misspelt name fails silently. A Godot script would be in the scene, so it can be in the game project, not in Core.
  - Tried first: a script on the tree in the game project, `PlayerAnimationTree`, whose exported properties wrapped a state object from Core, which the controller found by reflection. It worked, but Core reached into game code by name.
  - Decision (Neil, 2026-10-16): no script. `Entity` sets the tree's `advance_expression_base_node` to itself, so the tree's expressions read the entity's properties directly. Godot's source generators, which Core already uses (`005 §3.2`), register every public property of a Core node class with the engine, exported or not. Checked on 2026-10-15 with a throwaway test: `!OnGround` evaluated against a bare `WalkerEntity` gave `true`, and an unknown name failed with `Invalid named index`. Numbers that aren't expressions, for a blend space or a time scale, are still set by name.
- [x] 4.3 Who sets them? Decision (Neil, 2026-10-16), as built: `WalkerEntity`, from its body's state and from what its controller intends, so that the player and the robots share the code, as they must once robots have the player's model. A controller publishes its intent, such as its facing and whether it is jumping, and doesn't touch the visual. The same code turns the model (item 3.1).
- [ ] 4.4 Animation follows the body's ground speed, not what the walker is trying to do. Found by Neil, 2026-10-16: a walker pushed by something it faces walks backwards at the speed it is pushed, and an idle walker turned to the camera and pushed would walk towards the camera. Not fixed here; issue #25.

## 5. Tests and loaded models

- [x] 5.1 The test project can't load models. Decision (Neil, 2026-10-15): B, built the same day. Found on 2026-10-15: with the models loaded by `RoomNode`, all 30 in-engine tests fail. `GD.Load<PackedScene>` returns null for `res://assets/...` in the test project, whose `res://` is `tests/BodyCheck.Game.Tests`, so `Instantiate` throws in `RoomNode`'s constructor, and each test's `TearDown` throws in turn on the room that was never made. `005 §3.3` foresaw it. Options:
  - A. Hand the looks to `RoomNode`. `Main` gives it the models; tests give it boxes, or nothing. This is `005 §3.3` carried one level up. The tests don't run the code that animates and turns models.
  - B. Make the game's assets reachable from the test project: a directory link to `game/assets`, and an import pass (`Godot --headless --import`) before the tests run. The tests run what the game runs, at the cost of an import in every build, more time, and care that the import changes no file in the repository (CI's "No changes" check).
    - Tried by hand on 2026-10-15, with a junction at `tests/BodyCheck.Game.Tests/assets` and nothing committed. A cold import of the three models took 4.2 seconds, Godot's start included. It changed no file in the repository: the `.import` files are shared through the link, and their `res://` paths are the same in both projects. After it, all 30 tests passed, in about 1 second.
    - The tests run headless, on Godot's dummy renderer, so they compile no shaders and don't need the shader cache. Neil's hitch when switching MSAA after loading a new model is likely that cache, and doesn't apply.
    - Git on Windows can't store a junction without Developer Mode, so the link would be made by the test project's build, beside the target that copies `project.godot`. So would the import, run only when an asset is newer than the last import.
    - Neil (2026-10-15): other files the game reads, such as LDtk levels, would want the same access for integration tests.
  - C. Neil's proposal: when a model fails to load, draw a garish debug visual in its place, such as a pure magenta box of the body's size. People playing the game would see it; tests would run on boxes.
    - It answers the objection to a silent fallback: nothing is hidden from someone looking at the screen. Nobody looks at the screen in CI, though, so a broken path passes the tests.
    - Every test would take the failure path, which the game never should, and Godot logs an error for each failed load, three or more per test.
  - Not considered further: falling back silently to boxes when a load fails, which would hide a missing asset in the game.
  - As built: the test project's build makes the junction if it is missing, and runs Godot's import after the build when a file in `game/assets` is newer than a stamp in `obj/`. Git ignores the junction. On a machine with no import cache, the build took 10 seconds, including the import; a build after it skipped the import. All 30 tests pass, and the build leaves no change in Git.
  - The import logs that it can't open `res://main/Main.tscn`, because the copied `project.godot` names the game's main scene. Harmless.
