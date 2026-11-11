# 059: The camera's lead

Status: closed. Whether the lead goes back to 0 (§1.8) is in [rendering.md](../designs/rendering.md)'s open questions.

## Context

Issue #195 lists camera quality-of-life features to try. The camera follows the player with a lag, centred 3 tiles above them (`010 §1`), so a running player sees less of where they are going than of where they have been. Three ideas were on the issue:

- Offset the wanted framing by a fraction of the player's velocity, damped.
- Two cameras, one on each side of the player, chosen by the way the player last faced, with a slow tween between them.
- Hold the wanted framing's height through a jump.

Neil tried the first and backed it out: even damped, it snaps back when the player stops. This record is about leading the player across the screen. Holding the height is left for later.

Goals:
- The view shows more of where the player is going.
- No snapback when the player stops.
- Few new moving parts: the framing, its map and its spring work as before (`058 §7`).

## 1. Leading the player

- [x] 1.1 Why an offset by velocity snaps back. An offset of `k v` has to go back to 0 whenever the player stops, so smoothing it only delays that return. With the framing's spring after it, the two springs in series lead while the player moves and then pull the view back once they stop, however they are tuned. Claude: in a platformer, where the player stops all the time, that backward drift is the problem. The way out is an offset that doesn't follow the player's state from moment to moment: one that holds when the player stops and changes only when the player turns, which is the second idea on the issue.
- [x] 1.2 The shape: one lead, not two cameras. Options: two cameras, one either side, and a tween between them; or one horizontal offset, the lead, added to the wanted framing as the look's vertical offset is. Decision: one lead. It is one number with a simple rule, and the framing's spring, map and box keep working on the wanted framing as they do now.
- [x] 1.3 What sets the lead. Options:
  - The player's velocity, with the lead moving toward its full size the way the player moves, at a fraction of the player's speed, holding when the velocity is 0. Claude built this first.
  - `Facing`. Rejected (Neil): it is set for animation, and is 0 in the air and standing, so it is too tied to animation to rely on for behaviour.
  - The horizontal input the player's controller acts on: the user's while playing, and a script's in a cutscene.

  Decision (Neil): input. The lead is then under the player's direct control, which Neil prefers on principle, and pressing against a wall leads the view too, so the player can look a little left or right. Moving platforms don't lead. To watch in play: a phase launches the player with no input, so the lead doesn't follow the launch.
- [x] 1.4 How the lead moves. With input instead of velocity, a lead that moves at a fixed rate changes too abruptly when the input changes, whatever the speed. Decision (Neil): a critically damped spring, as the framing's, with a goal that doesn't always go back to 0, which was the trouble with the velocity offset:
  - While there is input, the goal is the full lead the way it presses, however far the stick is pushed, with smoothing time `CameraLeadTime`, 0.5 game seconds. A tap moves the lead only a little.
  - When the input stops, the goal is the lead where it is, so the spring settles and holds.
- [x] 1.5 How far. Decision (Neil): 2.5 tiles, `CameraLead`. It almost exactly makes up for the framing's lag behind a player running at full speed, so a running player is about centred across the screen.
- [x] 1.6 When it moves. Decision (Neil): only while playing and in a cutscene, the modes in which the input moves the player. In every other mode it stays where it is: paused, the input does nothing, and while slowing for a phase or phasing, it aims the phase. A cutscene's script leads the view as the user's input does, so `RunRightOffLedge` lets go in the air, and the lead holds, which hints where to go next.
- [x] 1.7 Independent of looking. Neil tried one mechanism for both the look and the lead, and it almost worked, but they need opposite timing: a look takes a while to move the view and goes straight back when it stops (`014 §1.2`), and the lead moves at once and goes back late, if at all. Decision (Neil): they stay separate offsets of the wanted framing.
- [~] 1.8 Going back to 0. Some games bring the lead back after a while; some never do. Leaning (Neil): after the input has stopped for `CameraLeadReturnDelay`, 3 game seconds, the goal is 0, with smoothing time `CameraLeadReturnTime`, 1 game second. Not sure it stays: it needs playtesting.
- [x] 1.9 The map. The lead moves the wanted framing sideways, so, like looking (`058 §7.6`), it could move the goal round a corner of the map or across a gap. Neil, in play: the camera never escaped the map's valid set.
