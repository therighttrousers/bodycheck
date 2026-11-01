# 038: Vertices on a grid, for the jitter of mid-1990s 3D

## Context

Inciting event (2026-10-31): Neil wants the jittery feel of mid-1990s 3D games, but not too much of it. Putting the camera on whole pixels at the tiles' front faces looked pretty good; putting it on whole tiles was awful. Physics stays as it is: too many chances to make it fragile by lowering its precision.

Goals:
- Find which parts of rendering, made discrete, give the look.
- Find how much of it is enough.

Anti-goals:
- Fixed-point arithmetic in itself. 16.16 is far finer than a pixel, so copying it would show nothing. The jitter came from dropping precision at the end (Claude, from memory): the PS1 drew triangles from whole-pixel screen coordinates, and Quake stored animated models' vertices in 8 bits per axis.

## 1. The spike

- [x] 1.1 Screen-space vertex snap. Round each vertex's position on the screen to a grid in the vertex shader. Decision (Neil, by eye): whole pixels. Half pixels are too mild to notice. Two pixels could cause motion sickness in some people. 1.5 pixels was okay but too noticeable at normal character speeds, and people should be able to forget about it while playing. The effect is strongest when the player inches along, as it was in the games of the time. The "Look" room's turning polyhedron feels exactly right. Key 2 turns it on and off. The switch for the low-resolution size goes too: 320x240 is settled, though wide monitors may get more width.
- [x] 1.2 How to reach every material. The engine has no global vertex hook. It also doesn't give out the shader it generates for a `BaseMaterial3D`, because `material_get_shader` isn't bound, so rewriting those shaders doesn't work. As built: each `BaseMaterial3D` on a node that enters the tree is replaced by a `ShaderMaterial` of the same look, from one template. Meshes keep their own materials, so `AssetLibrary` still works on them. Costs of this way: a change to a material after it's replaced doesn't show, and materials with features the template lacks stay unsnapped: the laser beam (alpha hash, changed each frame) and the "Look" room's glass (refraction).
  - Could a post-import script do it, as one might for nearest filtering? Claude: only with a change of approach. It would reach imported models and not materials made in code, and it would have to swap in shader materials, which `AssetLibrary` and `LaserBeam`, written for `BaseMaterial3D`, don't handle. One game shader, used by imported and code-made materials alike, was considered too. Neil: preview while tuning is a must. A `ShaderMaterial` does preview in the editor, but materials converted on import can't be edited there, and one with its uniforms as its only controls is a worse tool than a `StandardMaterial3D`. Decision (Neil): keep `StandardMaterial3D` as what's authored and tuned, and keep replacing it at runtime. The template's render-mode variants are generated as needed, so there's no library of shaders to keep by hand, and a filter set on import flows through it.
- [x] 1.3 MSAA. Neil, by eye: it doesn't do much to mitigate the snap. Decision (Neil): off, and its switch goes. This frees the eye and mouth quads from the margins of `037 §4.7`.
- [x] 1.4 The camera with a grid. Decision (Neil, by eye): no camera snap. With the camera and the vertices both on whole pixels the effect is almost unnoticeable. The Phantom Camera host, which went to manual mode so that the snap could run after it, goes back to automatic.
- [~] 1.5 Next: a lattice in model space, if wanted. Affine textures last, as a PS1 signature that Quake and Descent didn't have.

## 2. Measured

- 2.1 With the snap off, the replaced materials matched the originals on a captured frame of the "Look" room: 0 of 76,800 pixels differ, with the colour pass on and with it off. The first version differed in 1,338 pixels, because the template left the diffuse and specular modes at the shader language's defaults, and read normal maps with the wrong hint. Two captures of the originals are identical, so a difference is real.
- 2.2 No shader errors, in the "Look" room or with `--probe=player`.
- 2.3 Seen in a frame of the moving probe: the outlines of neighbouring tiles differ by a pixel, even with the camera on pixels. Neil, from 1/8 speed: a tile's bevels are 1/16 tile deep, or 1 pixel, and a top-face bevel takes 1 or 2 rows, more often 2 the higher the player is above the tile. It's the crunch he wants.
- 2.4 The platform's eyes and mouth stayed intact in the one frame looked at, with and without MSAA. In motion, Neil sees the faces bob a little when a platform moves a little vertically, which is also the crunch he wants.

## 3. Textures

- [x] 3.1 Filtering. Smoothed textures took hardware that most people didn't have until the late 1990s. Decision (Neil): nearest filtering for imported models, from a post-import script, the default for new ones. `body.tres` stays for now, as the place to tune the platform's body and glow, and it keeps its own filter setting. The script can't change a material saved in its own file: Neil removed the setting and reimported, and the editor showed sharp textures, because it had the changed material loaded, while the game loaded the file and showed blurry ones. So the script leaves such materials alone and warns if one isn't nearest. Following `use_external` to change the file was rejected: an import shouldn't rewrite a tuned file, and CI's "No changes" check would fail. Forcing nearest in `VertexSnap` would cover them, but it would also step the "Look" room's gradient ramp, and the editor would still preview them blurry.

## 4. Animation

- [x] 4.1 Stepped animation. Tried at 15 Hz, then 10 Hz, as Quake had it (Neil, from memory). Decision (Neil, by eye): 10 Hz, on by default, with key 3 to compare. It looks more like an artistic choice than a technical limit. It also saves art: cross-fades as short as Bob's 0.1 seconds are moot, and clips probably needn't be as precise. Steps are counted in real time and advance by game time, so slow motion isn't choppier. All animations step together for now.
- [x] 4.2 The platform's face. Stepping means a tree sees `Strain` only at steps, so the one-tick spike of `037 §3.5` would usually go unseen. Exempting the tree (Claude) would need a way to mark trees. Decision (Neil): smooth `Strain` in the controller, with an average over ticks, and make the tree's transitions immediate, with a frown above 0.4 and no direct path between smile and frown. The controller has more direct control of the face. Supersedes `037 §3.5`.
