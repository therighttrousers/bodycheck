# Models

Relevant decision records:
- [006: Models, their base point, and animation](../decision-records/006-models-and-animation.md)

What the game asks of a 3D model, and how code and art meet in it. The models in the game now are placeholders.

## A model

- **Made in Blender**, exported as glTF, and imported by Godot. The import settings are in the `.import` file beside the glTF, and the Godot editor changes them in its Import dock.
- **A wrapper scene** beside the glTF, `scene.tscn` in the model's folder in `game/assets/`, is what the game loads. It holds what Godot adds to the model: an animation tree, material overrides, and any adjustment to the model's position, size or direction.
- **The wrapper's layout** (`006 §1.3`): its root is a plain `Node3D` with no transform, which the game moves and turns. The glTF is the root's child, named `Model`, which the artist moves, scales and turns. Each transform has one owner. The child's transform applies first, so the game turns the model about the root's origin, which is the model's base, wherever the glTF's own origin was.
- **Units:** one unit is one tile. The player is 0.8 tiles wide and 1.6 tall ([movement.md](movement.md)).
- **The base is the origin** (`006 §1.1`). The base is where the model stands on the ground: between a character's feet, or the middle of a block's bottom face. A model made for this game has its base at the origin in Blender. A model from elsewhere is adjusted in its wrapper scene until it does.
- **It faces +Z**, towards the camera. The game turns it to face left or right.
- **A variant**, such as a robot in the player's shape with another colour, is an inherited scene of the model's wrapper scene, with what differs set in the editor. `robot.tscn` beside Bob's wrapper scene overrides the body's material.

## Adding a model

1. **Copy the model's folder** into `game/assets/`: the glTF, its `.bin`, its textures and its licence. Godot imports it when its editor next has focus.
2. **For an animated model, import the rest pose.** Select the `.gltf` in the FileSystem dock, open the **Import** tab, check **Import Rest as RESET** (under Animation), and click **Reimport**.
   - If you don't do this, Godot will make changes to your `.tscn` file whenever the editor plays an animation.
3. **Make the wrapper scene.** **Scene → New Scene**, and choose **3D Scene** for the root, which makes a plain `Node3D`. Name it after the model.
4. **Add the model.** Drag the `.gltf` from the FileSystem dock onto the root, or select the root and click **Instantiate Child Scene** (the chain link, Ctrl+Shift+A). Name the new child `Model`.
5. **Make its children editable.** Right-click `Model` and check **Editable Children**. Without it, the nodes inside the glTF can't be selected: the `AnimationPlayer` for the tree, or a mesh to override.
6. **Place it.** Move, scale and turn `Model` until its base is on the root's origin, it faces +Z, and its size is right in tiles. Leave the root alone.
7. **For an animated model, add the tree.** Add an `AnimationTree` as a child of the root, named `AnimationTree`, and set its **Anim Player** to the `AnimationPlayer` inside `Model`. Build its graph (see [Animation](#animation)).
8. **Save** it as `scene.tscn` in the model's folder. Right-click it in the FileSystem dock and choose **Copy Path** for the `res://` path that the game loads.
   - This path can be passed to a call to `GD.Load<PackedScene>(path)` in the C# code to load the model and its animations.
9. **Commit everything in the model's folder**, including the `.import` files Godot generated and the `.tscn` file you made.

To make a variant of the wrapper scene; for example, to create a model with a different skin but with the same geometry and animations:
1. Right-click `scene.tscn` for the model.
2. Choose **New Inherited Scene**.
3. Make your changes.
4. Save the scene as a new `.tscn` file in the same folder as the wrapper.

## How the game places a model

- Each entity is given its model when it is made, as a child. The entity's own origin is the centre of its body, so it lowers the model by half the body's height, and the model's base meets the bottom of the body (`006 §2.1`).
- A walker turns its model about the vertical axis at a fixed rate, towards facing right, left or the camera, as its controller sets `Facing` to 1, -1 or 0. The body doesn't turn (`006 §3.1`).

## Animation

Godot plays a model's animations with two nodes:
- The **`AnimationPlayer`**, which the importer makes from the glTF's animations, holds the clips, such as `bob_rig|walk`.
- The **`AnimationTree`**, which the artist adds to the wrapper scene's root, decides which clips play and how they mix. It is built in the editor's AnimationTree panel and saved in the wrapper scene. The game finds it by its name, `AnimationTree`.

The tree is the interface between code and art (`006 §4`).

- **Transitions** between the states of the tree's state machine are expressions, such as `IsGrounded && IsMoving`. The game makes the entity the base node of the tree's expressions, so an expression can use any public property of the entity whose type Godot understands.
- **What a walker publishes for the tree:**

| Property | Meaning | Set by |
|---|---|---|
| `IsGrounded` | On the ground, as far as animation is concerned. For the player, that includes the coyote ticks after leaving it. | The controller |
| `IsMoving` | Trying to walk or run. | The controller |
| `IsJumping` | Pushing upward in a jump. | The controller |
| `Facing` | 1 for right, -1 for left, 0 for the camera. | The controller |
| `GroundSpeed` | Speed along X relative to what it stands on, in tiles per second, signed. | The entity |
| `OnGround` | Whether touching the ground this tick. | The entity |

- **Parameters** that code sets each tick, because a blend space and a time scale take a number, not an expression:
  - `parameters/Move/BlendSpace1D/blend_position`: the absolute ground speed, in tiles per second.
  - `parameters/Move/TimeScale/scale`: the same, so that a clip with a custom timeline of length `n`, stretched, plays one cycle per `n` tiles.
  - A model whose tree has no such parameter ignores it. A misspelt parameter name is ignored too, silently. A misspelt name in an expression is an error.
- **Bob's tree**, the player's and the robots' model: a state machine of `Idle`, `Move`, `Jump` and `InAir`. `Move` is a blend space of idle at 0 tiles per second, walk at 4 and run at 15, followed by a time scale.
- **Rest pose:** the import setting `animation/import_rest_as_RESET` is on for animated models. The editor plays the tree while the scene is open, and without a `RESET` animation it would save the pose it happened to show into the wrapper scene.

## Tests

The game's tests load the same wrapper scenes as the game: the test project's build links `game/assets` into it and runs Godot's import (`006 §5.1`).

## Open questions

- Whether a model's size belongs in its import settings (Bob is imported at 0.85) or in its wrapper scene.
- Animation that follows what a walker is trying to do, not only how fast it moves. A walker pushed by something it faces walks backwards at the speed it's pushed, and an idle walker that faces the camera and is pushed walks towards the camera. Issue #25.
- Turning a walker's body with a motor, so that stacked walkers turn each other (`006 §3.1`). It needs yaw unlocked and a cylinder in place of the box.
