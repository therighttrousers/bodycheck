# Models

Relevant decision records:
- [006: Models, their base point, and animation](../decision-records/006-models-and-animation.md)
- [019: Carrying blocks on walkers](../decision-records/019-carrying-blocks.md)
- [021: Sound](../decision-records/021-sound.md)
- [024: Playtesting feedback](../decision-records/024-playtesting-feedback.md)
- [031: Tests in the game's project](../decision-records/031-tests-in-the-game-project.md)
- [037: Resizable platforms, and a first try at the game's style](../decision-records/037-resizable-platforms-and-first-style.md)

What the game asks of a 3D model, and how code and art meet in it. The models in the game now are placeholders.

## A model

- **Made in Blender**, exported as glTF, and imported by Godot. The import settings are in the `.import` file beside the glTF, and the Godot editor changes them in its Import dock.
- **Godot imports only the glTF**, never a `.blend` file: the project setting `filesystem/import/blender/enabled` is off. Importing a `.blend` runs Blender, so every machine that opens the project would need it installed and configured, CI's included. Where it isn't, the model doesn't load.
- **A wrapper scene** beside the glTF, `scene.tscn` in the model's folder in `game/assets/`, is what the game loads. It holds what Godot adds to the model: an animation tree, material overrides, and any adjustment to the model's position, size or direction.
- **The wrapper's layout** (`006 §1.3`): its root is a plain `Node3D` with no transform, which the game moves and turns. The glTF is the root's child, named `Model`, which the artist moves, scales and turns. Each transform has one owner. The child's transform applies first, so the game turns the model about the root's origin, which is the model's base, wherever the glTF's own origin was.
- **Units:** one unit is one tile. The player is 0.8 tiles wide and 1.8 tall ([movement.md](movement.md)).
- **The anchor is the origin** (`006 §1.1`, `037 §1.6`). The anchor is a spot on the body's box. For most bodies it is the base, where the model stands on the ground: between a character's feet, or the middle of a block's bottom face. For a hovering platform, which stands on nothing, it is the center. A model made for this game has its anchor at the origin in Blender. A model from elsewhere is adjusted in its wrapper scene until it does.
- **It faces +Z**, towards the camera. The game turns it to face left or right.
- **A variant**, such as a robot in the player's shape with another colour, is an inherited scene of the model's wrapper scene, with what differs set in the editor. `robot.tscn` beside Bob's wrapper scene overrides the body's material.

## The platform's model

A platform's model fits any width (`037 §4`). It is the first model in the game's style: simple dark beveled shapes, with glowing traces 1 pixel wide.

- **Its parts** are separate objects in one `.glb`. `CapLeft` and `CapRight` are 6 pixels wide, with their origins at their outer ends, so `AssetLibrary` puts them at the ends of the body. `Center` is one unit wide, and `AssetLibrary` stretches it between the caps. Its art is only horizontal traces, so stretching doesn't show.
- **The art** is `platform_traces.png`, an emission map at the game's resolution. The front face, level with the tiles' fronts, is exactly 16 pixels to the tile, so a texel lands on one pixel. Edit its pixels and the game shows them, with no export.
- **Its material** is `body.tres`, which replaces the glTF's in the `.glb`'s import settings, because Godot ignores a glTF's nearest filtering. The body's color and the glow are set there, not in the `.blend`. Keep its `emission` black: the material adds the map to it.
- **The face** is `Eyes`, `MouthSmile`, `MouthStraight` and `MouthFrown`: quads just in front of the front face. A mouth changes by showing one and hiding the others.
- **Keep lit pixels off mesh edges.** MSAA dims them. A quad's edges run through an empty margin of its art, and the art on the caps and center repeats one column past each end in the atlas, for the seams between them (`037 §4.7`).

## A model made in Blender

1. **Keep the `.blend` in the model's folder** in `game/assets/`, beside its export. It is the source; the export is what the game uses.
2. **Export it** with **File → Export → glTF 2.0**, as a `.glb` beside the `.blend`, named after it, with **+Y Up** on (the default). The exporter turns Blender's Z up into glTF's Y up, so a model that faces -Y in Blender faces +Z in Godot.
3. **Export again after every change**, and commit the `.blend` and the `.glb` together. Godot reimports the `.glb` when its editor next has focus. Nothing reads the `.blend`, so a change saved only there doesn't reach the game.

Then wrap it as below, from step 2, using the `.glb` where the steps say `.gltf`.

## Adding a model

1. **Copy the model's folder** into `game/assets/`: the glTF (a `.gltf` with its `.bin`, or a `.glb`), its textures and its licence. Godot imports it when its editor next has focus.
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

- Each entity is given its model when it is made, as a child. The entity's own origin is the centre of its body, so it moves the model to the body's anchor, which `BodyTuning` sets (`006 §2.1`, `037 §1.6`).
- A body starts with its anchor at the same spot on its box in LDtk, which is usually larger: a character on the bottom of its box, and a platform in the middle of its box (`037 §1.3`).
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

- **What a platform publishes for the tree:**

| Property | Meaning | Set by |
|---|---|---|
| `Strain` | How hard it works against what pushes it around, from 0 to 1, its most. Moving itself along its line doesn't count, nor does a load that carries it the way it is going (`037 §3.4`). On a platform 3 tiles wide the player gives about 0.3, or 0.05 going down, a block about 0.5, and anything that stops it 1. | The controller |

- **Parameters** that code sets each tick, because a blend space and a time scale take a number, not an expression:
  - `parameters/Move/BlendSpace1D/blend_position`: the absolute ground speed, in tiles per second.
  - `parameters/Move/TimeScale/scale`: the same, so that a clip with a custom timeline of length `n`, stretched, plays one cycle per `n` tiles.
  - A model whose tree has no such parameter ignores it. A misspelt parameter name is ignored too, silently. A misspelt name in an expression is an error.
- **Bob's tree**, the player's and the robots' model: a state machine of `Idle`, `Move`, `Jump` and `InAir`. `Move` is a blend space of idle at 0 tiles per second, walk at 4 and run at 15, followed by a time scale.
- **The platform's tree:** a state machine of `smile`, `straight` and `frown`, whose clips show one mouth and hide the others. Its `AnimationPlayer` is in the wrapper scene, since the `.glb` has no animations. It is straight above a `Strain` of 0.15 and frowns above 0.75. Clips last 1 second and transitions switch at their end, so `Strain` needs no smoothing: a one-tick spike, such as when the player lands on a platform or jumps off it, makes a frown that lasts a moment (`037 §3.5`).
- **Rest pose:** the import setting `animation/import_rest_as_RESET` is on for animated models. The editor plays the tree while the scene is open, and without a `RESET` animation it would save the pose it happened to show into the wrapper scene.

## Sounds

A character's sounds are in its wrapper scene, as its animation is: the scene is the interface between code and art for both (`021 §1.3`).

- **Cues.** A controller raises an `EntityCue` on its entity when something happens to it that the scene may react to, such as `PhaseBegan`. To give cues sounds, add a `CueSounds` node to the wrapper scene, and under it an `AudioStreamPlayer3D` for each, named after its cue, on the `Sfx` bus, so that the sound comes from where the character is. A cue with no player plays nothing; so does a misspelt name, silently.
- **The cues:** `Jumped`, `PhaseBegan`, `PhaseThrown`, `PhaseCancelled` and `SpawnCancelled`, all raised on the player.
- **When to use which.** A cue says *when* something happens, on the tick it happens. An audio track in a clip says *where in a motion*, and plays only when the animation tree reaches that clip, which may be late or not at all after a transition's blend. So a sound that marks an event, such as a jump, is a cue. A sound that follows a motion is a track: footsteps, which keep time with the run cycle as its speed changes; sounds at points along a long animation, such as a door's creak as it swings or the stages of a death; sounds in an idle's fidgets. A cue can start such an animation, and its tracks then time the sounds within it (`021 §1.2`). A sound that lasts as long as a state, and stops when the state ends however it ends, follows the state: the node that shows the state plays it, as `LaserBeam` plays a node under it named `Sound` while the laser is locked or firing.
- A character's sounds pause with the world. The game pauses half a second after `PhaseBegan`, so its sound lasts 0.5 seconds or less, or it is cut off by the pause (`021 §2.6`, `§4.2`).

### Adding an audio track to an imported animation

An imported glTF's animations can't be edited, so an animation that gets a track is saved to a file of its own first, keeping the tracks added to it when the glTF is imported again.

1. In the FileSystem dock, double-click the character's `.gltf` file to open **Advanced Import Settings**.
2. In its Scene tree, under the `AnimationPlayer`, select the animation.
3. On the right, under **Save to File**, turn on **Enabled** and **Keep Custom Tracks**, and set **Path** to `<animation name>.tres` beside the `.gltf` file.
4. Click **Reimport**. The `.gltf.import` file grows by about 2,000 lines of empty slice settings, which the editor writes; commit it with the `.tres`.
5. Open the character's wrapper scene. Right-click its root, choose **Add Child Node…**, and add an `AudioStreamPlayer3D`. Not under the `AnimationPlayer`: that is a plain `Node`, so a player under it doesn't move with the character, and plays from the world's origin. One serves every animation. Set its **Bus** to `Sfx`, and leave its **Stream** empty: the track's keys carry the sounds, and the editor clears the player's stream when it saves.
6. Select the `AnimationPlayer`, and in the Animation panel pick the animation.
7. Click **+** on the left of the panel, which saving to a file enabled, and choose **Audio Playback Track…**, and the player.
8. Right-click the track's timeline, choose **Insert Key…**, and drag the key to its time. Or drag the sound file from the FileSystem dock onto the track.
9. Select the key, and set its **Stream** in the Inspector.

## Tests

The game's tests load the same wrapper scenes as the game, since they are in the game's project (`031`).

## Open questions

- Whether game-resolution art with nearest filtering is the style, as on the platform, or only suits the front face (`037 §4.3`).
- Whether a model's size belongs in its import settings (Bob is imported at 0.85) or in its wrapper scene.
- A walker's walk is wrong at very low speeds, such as the player's when a tunnel knocked a block off its head (`019 §6.4`).
- Animation that follows what a walker is trying to do, not only how fast it moves. A walker pushed by something it faces walks backwards at the speed it's pushed, and an idle walker that faces the camera and is pushed walks towards the camera. Issue #25.
- Turning a walker's body with a motor, so that stacked walkers turn each other (`006 §3.1`). It needs yaw unlocked and a cylinder in place of the box.
