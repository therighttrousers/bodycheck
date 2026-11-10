# 058: A map of where the camera may be

## Context

The camera framing of 042 weighs, each frame, how much of the painted camera boundary each candidate view would show against how far it is from the wanted framing (`042 §6.1`). Tuned in play, it works as Neil expects a camera to work, in open space and against the world's concave boundary (`042 §6.6`). It fails where the player's space is lower than the view, under a thin painted band with a room above: a view through the band costs no more than one that stays below it (`042 §6.3`).

Visibility from the player was decided as the fix (`042 §6.5`), and hard visibility built first (`042 §6.7`). In play it kept the camera in good positions, but made the quick pans worse and left hardly anywhere to look up or down: the problems of the first version, with valid positions defined by the tiles, if not as badly (`042 §6.8`). It's parked on the branch `camera-visibility`.

Neil proposed backing out and trying something very different, from morphological image processing: a map, computed when the world loads, of where the view's centre may be.

Goals:
- Computed once, at load, not each frame.
- Visible: the map can be drawn in the game, for debugging and tuning both the processing and the painting.
- Painting rules that are intuitive and that the drawn map makes easy to check.
- Narrow spaces get a line the view can follow; gaps of 4 or 5 tiles between rooms give a path the view can pan along smoothly.

Anti-goals:
- A native dependency in the build, CI and exports.
- Per-frame cost that grows with the world.

## 1. The map

- [x] 1.1 What it holds (Neil): a binary image, at a resolution finer than the tiles, of the points where the view's centre may be. It starts from `CameraBoundary` inverted: outside the rooms and on painted tiles, 0; everywhere else, 1.
- [x] 1.2 How it's made (Neil):
  1. Scale the image 3 times across and 4 times up and down, for a view of 4:3. The swapped numbers are on purpose: each thinning step then removes a third of a tile across and a quarter up and down, so the steps eat into the space at the view's own aspect.
  2. Thin it a fixed number of steps, which removes the centres whose view would show the other side of a boundary, but leaves a skeleton in narrow areas. Where the view must show some boundary, the skeleton is where it shows the least, evenly on both sides.
  3. Grow and smooth it a few steps, by dilation or closing, so that the view may show the boundary tiles themselves.
- [~] 1.3 Details of 1.2:
  - Upscaling: nearest neighbour first. Smoother upscaling followed by a threshold is an option.
  - Thinning steps: Neil guessed about 40. Claude: the view less its 1-tile margin reaches 9 tiles across and 6.5 up and down from its centre, which is 27 and 26 pixels at 3 by 4, so about 27 steps. Thinning by less than the view could stand in for step 3.
  - Growing and smoothing: Neil meant one that only grows and smooths; he wrote opening, which Claude pointed out would erase the skeleton, since a line 1 pixel thick doesn't survive the erosion. Closing smooths without removing thin lines.
  - Between steps 2 and 3, the image could be scaled across to restore its 1:1 aspect with the tiles, if step 3 should grow it evenly. With nearest neighbour, step 3 would also smooth the jaggies; with smoother scaling, a threshold makes it binary again.
  - Only 4:3 for now. Other aspects need their own scale, so the map would be made for the window's aspect, and remade when it changes.
- [x] 1.4 Written in C#, not with OpenCV (Neil's first proposal, OpenCvSharp). Claude: OpenCV's thinning runs until nothing changes, with no fixed number of steps, so the loop would be written by hand anyway, and the native library would be needed in every build, CI run and export. Erosion and Zhang–Suen thinning are each short, and fast enough at load. Neil agreed. Claude writes it.
- [x] 1.5 The simple version first (Neil): scaling and a fixed number of steps, as in 1.2. If it works well enough, it stays. Claude's alternative, in reserve: find for each pixel how large a view fits centred there, and thin in order of that size down to a threshold. That handles any aspect exactly, with no scaling, and would give the drawn map a gradient.
- [x] 1.6 Pathologies are for painting rules, not processing (Neil). Thinning keeps every connection, so any gap at all in a band gives a strand of skeleton through it: the game takes it that the player should see through it. Claude expects Neil's band of alternating painted and unpainted tiles to give a ladder of strands into the room above. The drawn map makes it clear what happened when a boundary is painted oddly. Processing such as dropping connections narrower than the player is in reserve.
- [ ] 1.7 Later, perhaps: making the map a step of an asset pipeline, at build time rather than at load (Neil). Not yet.

## 2. Seeing it

- [x] 2.1 F1 toggles drawing the map in the game, at Z = -0.5, between the tiles' front and back faces, opaque or partly transparent (Neil). F2 to F5 are taken.
- [x] 2.2 The first step is the map and its drawing alone. `CameraFraming.Frame` is unchanged (Neil).
- [x] 2.3 With the framing (Neil, after playing the first version, "a good direction", with tuning of the parameters and the algorithm to come): the drawing also shows the wanted framing and the view's actual centre, reported in telemetry, so that telemetry serves both drawing and tests. Later, points of interest that attract the view's centre. `CameraMapView` then becomes `CameraDebug`. Claude: as `CollisionDebug` already draws the corner correction from telemetry, and the F3 overlay shows reported values for free. `Frame` is still tested directly; telemetry is for watching the rig as it runs, such as a probe measuring how far the view jumps in a frame. Points of interest are the world's, so the drawing reads those from the world.

## 3. Placing the view

- [ ] 3.1 How the view's centre is found from the map, given the player and the wanted framing. Options:
  - (a) Neil: walk pixel by pixel from the player toward the wanted framing until a valid pixel; keep walking until an invalid one, and take the one before it. If none is valid, the player; if the walk ends valid, the wanted framing.
  - (b) Claude: the valid pixel nearest the wanted framing, among those that keep the player in view (`042 §6.4`). Later, perhaps, a soft minimum over valid pixels, the cost of 042 with the map in place of the hidden area, to smooth the jumps where the nearest pixel switches from one part of the map to another. Claude's concern with (a): where the walk misses the map, as for a player above the skeleton of a low room who wants to look up, it returns the player, and the view goes through the band again.
  - Neil: start even simpler, and see what happens.
- [ ] 3.2 Only the player's own area (Claude): thinning never joins areas that weren't connected, so if the view's centre may only be in the connected part of the map that the player is in, the room above a solid band can't win. That gives the regions of `042 §6.5` (b) without painting them.

## 4. The first version, as built

- 4.1 As built (Claude), in `CameraMap`, made when the map is first drawn:
  - The space that isn't boundary, scaled 3 across and 4 up and down, nearest neighbour. At that scale a view of 20x15 tiles is 60x60 pixels, so thinning by half of it is the same number of steps both ways.
  - Zhang and Suen's thinning, 30 steps, half the view. Each step is two halves, which together take a pixel off every side. It keeps a pixel whose removal would cut the space, and the end of a line.
  - Grown by the margin, 1 tile: 3 pixels across and 4 up and down, a dilation by a rectangle, in place of the closing of 1.3, so that the margin is exact both ways and the image stays at the thinning's scale.
  - `CameraMapView` draws it at Z = -0.5, a pixel to a pixel of a texture with nearest filtering: green, partly transparent, where the view's centre may be, and dim red on the boundary.
- 4.2 Measured (Claude), in Debug: 90 ms for `Testing`, 73x55 tiles, and 65 ms for `Main`, 91x15. It's linear in the world's area, so a world of 16x16 rooms would take seconds; the thinning could then visit only the pixels on the space's edge.
- 4.3 Seen in images of the worlds' maps (Claude):
  - `Main`, exactly as high as the view, is a line along its middle, ending 9 tiles from each end, where the view less its margin fits.
  - `Testing` is one skeleton, connected through the gaps between rooms, with no space anywhere wide and high enough for a view, so there are no areas, only lines.
  - Where a space is lower than the view, its line stops shrinking at its ends once its sides meet, so its ends are nearer the space's ends than a view less its margin allows: in a space 13 tiles high, about 7.7 tiles from the end rather than 9, which shows about 1.3 tiles more boundary there. Thinning that went on shortening lines' ends, to the view's half width, would fix it.

## 5. Placing the view, the first version

- 5.1 As built (Claude), taking "even simpler" as (b) of 3.1 alone, without 3.2 or a soft minimum: `CameraFraming` puts the view's centre at the point nearest the wanted framing on the map, among those in the box round the player of `042 §6.4`. Each pixel of the map stands for its cell, so that along the map's straight edges the centre slides rather than stepping from pixel to pixel; where the wanted framing is on the map, it's the wanted framing. Where nothing in the box is on the map, the wanted framing, kept in the box. The cost framing of 042 is gone. The map is made when the rig starts to follow a world, and `CameraDebug` draws the rig's.
- 5.2 Measured (Claude), in Debug, with a view of 20x15 tiles:
  - 0.17 ms a frame.
  - Walking under a ceiling with a shaft 5 tiles wide through it, wanted 3 tiles above the player: the centre jumps 3.5 tiles in one step as the shaft's line becomes nearer the wanted framing than the space below, which damping turns into a quick pan.
  - Under a band 2 tiles thick, in a space 11 tiles high: the view stays on the space's line, which is about 2.25 tiles thick, so its top can reach 1.25 tiles past the band, into the space above. Looking up or down moves it only within the line.
- 5.3 Playtesting (Neil, 2026-11-08): "This is awesome. I love it." The quick pans and the looking through boundaries still happen, but with the map and the framing drawn, Neil can see what the problem has been, and expects to build enough intuition to solve them. He made the debug drawing partly transparent, so that it takes less attention while playing, and added `TestingOutside`, a level large enough that the map there is an area, not a skeleton.

## 6. Moving the target within the map

- [x] 6.1 Why placing fails (Neil): the quick pans come from the target jumping from one part of the map to another as the wanted framing moves, and the views into the next room from it jumping to a branch on the other side of a boundary. Smoothing the map can't fix either: it always takes in points the camera shouldn't be at, and unless the map is convex, some pair of wanted framings a frame apart makes the nearest point jump, however smooth it is. Claude: in Euclidean space, only convex sets have a nearest point that is unique and moves continuously. Clever ray casting might keep the view from jumping across a boundary, but Neil wants one change that fixes both.
- [x] 6.2 Decision (Neil, 2026-11-08): the target moves within the map, rather than being placed on it, so that it can't jump from branch to branch. Keep it simple, rather than making the target clever. Options:
  - (a) Neil: a potential field toward the wanted framing. Simple, and it often works, but it gets stuck in concave pockets.
  - (b) Neil: a search, from the target, that may not take steps within 45 degrees of directly away from the wanted framing.
  - (c) Claude: move and slide, as a character controller does along walls, which is (a) on the map's pixels. Getting stuck at the locally nearest point is often what a camera should do: across a band from the wanted framing, it stays as near as it can on its own side, rather than going the long way round, which would be the quick pan again, only slower.
  - Decided: (c) first, with (b) in reserve if the target gets stuck where it shouldn't.
- [x] 6.3 Rules (Claude, agreed by Neil):
  - The target moves toward the wanted framing at no more than a speed, on game time, sliding along the map's edges.
  - It stops where nothing nearer the wanted framing is next to it.
  - The view showing the player overrides the map: the box round the player pushes the target, off the map if need be, and off the map it moves back onto the nearest point of it.
  - When the camera teleports, such as when a world starts, the target is placed at the point of the map nearest the wanted framing, as in 5.1.
- 6.4 As built (Claude), in `CameraFraming`: each frame, the target is first kept in the box round the player. Then, up to its speed's distance, it moves step by step toward the point nearest the wanted framing in the cell of the map it's in and the 8 next to it, and stops when that point is no nearer than where it is. Where none of those 9 is on the map, it moves toward the nearest point of the map in the box. Its speed is 24 tiles a game second, twice the player's top speed. The rig moves it on game time, as the camera's damping is, and a new framing, made when the rig starts to follow a world, places it as in 5.1.
- 6.5 Measured (Claude), in Debug: about 0.01 ms a frame, a tenth of placing it.
  - Two corridors 4 tiles high, with a solid band 2 tiles thick between them: placed, looking up from the lower one puts the target on the upper one's line; moved, it stays on the lower one's.
  - Walking at 6 tiles a second under a ceiling with a shaft 5 tiles wide through it, wanted 3 tiles above the player: the target moves no faster than its speed and the player's, and rises into the shaft. Past it, it's stuck at the shaft's edge, where every step toward the wanted framing is off the map, until the box drags it off, about 9 tiles on; then it moves down onto the map below and catches up at its speed. This is the case for the search of 6.2 (b), from which a step down and along is allowed.
- 6.6 Playtesting moving the target (Neil, 2026-11-08): snags. In places the player has to run almost off the view before the target catches up, and looking up or down works only where the target is already on a vertical run of the map. After looking, the target sometimes doesn't go back to where it was. Decision (Neil): the search of 6.2 (b), done afresh each frame, with no state kept between frames, however much it costs while investigating. Looking may come to work differently, so for now the target is to follow the player's own moves well; looking is set aside.
- 6.7 As built (Claude): each frame, a breadth-first search over the map's pixels in the box, from those next to the target. A step goes to any of the 8 pixels next to one, but not at more than 135 degrees, in tiles, from the way from that pixel's centre to the wanted framing. The target moves toward the reached point nearest the wanted framing, entering each pixel's cell on the way at the point nearest where it has got to, at its speed; where no reached point is nearer than the target, it stays. Off the map, as in 6.4.
- 6.8 Measured (Claude), in Debug, walking at 6 tiles a second under the ceiling and shaft of 6.5, either way: the target lags the player by at most 3 tiles, rather than 9. It holds on the shaft's line, level with the wanted framing, until the space below, 2.5 tiles under the wanted framing, is nearer, then goes down to it. About 1.7 ms a frame.

## 7. A spring to the nearest point

- 7.1 Playtesting the search (Neil, 2026-11-10), with telemetry and drawing for the search's best path:
  - Sometimes the point nearest the wanted framing took too long to change for a search to be done, and the target stayed where it was meanwhile.
  - With a high enough speed, the search was no better than placing the target; with a low enough one, it often couldn't keep up.
  - No speed fast enough to keep up with the player was slow enough for the target to move smoothly round a corner of the map. So the target must be able to leave the map sometimes.
- [x] 7.2 Decision (Neil, 2026-11-10): the goal is the point of the map nearest the wanted framing, as placed in 5.1, and may jump; the target follows it as a critically damped spring. When the goal jumps across a gap or round a corner, the spring's smoothing time is set about in proportion to the jump, so that the target crosses at about the same speed whatever the jump's size; otherwise the smoothing time shortens toward its least, `CameraMinSmoothTime`, a feature, 0.15 game seconds. A jump is when the map is missing halfway between the goal's last place and its new one. Phantom Camera's damping is off: the spring is the only smoothing. Neil considered returning the smoothing time from the framing and letting Phantom Camera's spring do the smoothing, but that would have left the framing untestable. The search, and Neil's other explorations, are gone.
- [x] 7.3 Looking is separate from the wanted framing (Neil): the rig passes the look as an offset from the target. Decision (Neil): looking casts straight up or down from the target, within the map, and that's all. A view that moves diagonally sideways when looking up or down is weird.
- [x] 7.4 Changes after Claude's review (agreed by Neil):
  - The smoothing time after a jump is `2 D / (e v)`, at least the least, where `D` is the jump and `v` is `CameraPanSpeed`, a feature, 5 tiles a game second. The fastest a critically damped spring moves over a distance `D` is `D w / e`, with `w = 2 / T`, so this makes a pan's speed at its fastest `v`, whatever its size. Neil's first version, the least times `1 + D` in tiles, tended to about 4.9 tiles a second for large jumps, and half that for a jump of a tile. The smoothing time still shortens during a pan, so that a pan speeds up toward its end: Neil wants that, so that the player can't keep the target off the map for long by moving with it.
  - The smoothing time shortens by a factor of `e` to the minus 2 each game second, exactly, rather than by a fraction of the frame's length, which depended on the frame rate.
  - The spring moves by its exact solution rather than a step of an integrator, which oscillates and grows once a frame is longer than about half the smoothing time, as a hitch can be.
  - A jump that would cross the camera boundary, a painted tile, is refused: the goal is then the nearest point of the map that can be seen from the goal's last place without crossing it. Without this, the goal flips to a corridor on the other side of a band 2 tiles thick when the wanted framing is halfway between their lines, and the target pans there.
  - Looking casts up or down from the target to where the map ends, and is eased by a spring of its own with the least smoothing time. With Phantom Camera's damping off, a look that stopped snapped the view back in a frame.
  - The target is kept in the player's box after the spring moves it, not before, with no velocity left toward the box's edge, so that the view always shows the player.
  - The framing returns the goal with the view's centre, and the rig reports both to telemetry, as `CameraGoal` and `CameraTarget`, rather than the framing reporting the goal itself.
- 7.5 Measured (Claude), in Debug, walking at 6 tiles a second under the ceiling and shaft of 6.5, wanted 3 tiles above the player: the goal jumps by up to 3.6 tiles, and the view moves at most 0.16 tiles a frame. It lags the player by about 0.9 tiles while following, and by at most 3 past the shaft. A lone jump of about 5 tiles pans at up to 8.3 tiles a second, as the smoothing time shortens on the way. About 0.2 ms a frame.
- 7.6 Playtesting looking by casting (Neil, 2026-11-10): it doesn't always look down when it should, seemingly when the target is on the edge of the map. Riding a platform that moves sideways while looking is wild. Decision (Neil): remove the casting, which is the wrong approach now that its effects are seen. Looking moves the wanted framing again, as before 7.3, which is acceptable for now: it may look a little sideways when looking up or down, which may be something to get used to. Looking isn't to be disabled while the player moves.
- [ ] 7.7 How to support looking up and down without letting the player look into other rooms. Moving the wanted framing lets a look move the goal round a corner or across a gap, into a space the player can't see.

## 8. Lines' ends

- 8.1 Playtesting (Neil, 2026-11-10): lines' ends come too near the ends of their spaces, so the view shows too far past the camera boundary there, as 4.3 says. Not only in spaces lower than the view: in `TestingTooNarrow`, a space 13 tiles wide and much higher than the view, painted all round but at its entrance, the vertical line's ends are too long too.
- 8.2 Why (Claude): Zhang and Suen's thinning never removes the end of a line, so that lines don't vanish. A space thins at a pixel a step from each side, a third of a tile across and a quarter up and down, until it becomes a line; then its ends stop. In a space `H` tiles high, that's after about `2 H` steps, so the line's ends are `2 H / 3` tiles from the space's ends, less the margin, rather than the view's half width less the margin, 9 tiles: 7.7 in a space 13 high, 1.7 in a corridor 4 high. A shaft's line, in a space `W` tiles wide, ends about `3 W / 8` tiles from its end, less the margin, rather than 6.5.
- [x] 8.3 Removing lines' ends too (Claude's first proposal, tried by Neil) shortens them too much, so that the player can run a few tiles past a line's end, and the box drags the view off it, showing more of the boundary. A line one pixel thick is on the edge that both halves of a step peel, so each end went twice a step.
- [x] 8.4 Decision (Claude, agreed by Neil): an end of a line goes in the half of a step that peels the side it faces, and only that one: facing right or down, in the first; facing left or up, in the second. Each end then recedes a pixel a step, as the space's edges do, so after the 30 steps it's where the space's end would be: 9 tiles from it across, and 6.5 up and down, after the margin, as in a space wider than the view. A single pixel never goes, so a space smaller than the view keeps its middle; the two pixels of a line two long face opposite ways, so they never go in the same half. Short lines into dead ends, such as alcoves and shafts that end less than half the view from where they join a larger space, go, so the view doesn't go into them; lines that join two spaces, such as through a gap, have no ends, and stay. No special case for lines across or up and down.
