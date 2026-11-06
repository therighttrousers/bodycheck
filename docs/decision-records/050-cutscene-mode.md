# 050: The cutscene mode

## Context

Inciting event (2026-11-06): playtesting The Call, issue #168. Two things went wrong:

- **The player doesn't stand up for a dialogue.** Dialogue pauses the scene tree (`044 §2.1`), so the player stays in whatever pose it had, such as the fall's. `DialogueRunner` waits for the player to be on the ground, but it presents on the tick the player lands, before its animation has left the fall, and while it may still be sliding. Running animations while the tree is paused isn't enough: `WalkerEntity` sets what its AnimationTree reads, such as `IsGrounded` and `GroundSpeed`, in `Control`, which isn't stepped while paused. So the animations would only replay the last tick's state.
- **The jump that ends a dialogue makes the player jump.** The player's input is polled, and sees the held key on the first played tick after the dialogue (issue #174).

Neil: freezing the world is a symptom of a bigger question. At least animations should keep going, including shader animations such as flowing water. Physics might keep running while controllers are paused or taken over.

Goals:
- During dialogue, the world looks alive: animations, shaders and the camera keep going, and the player comes to rest standing.
- One set of rules for every cutscene, whether the player is talking with a character or being scripted to walk up to a powerup.

Anti-goals:
- Redesigning input across the game's modes. That is issue #174; this record settles only what the player's controller sees during a cutscene.
- The impulse on the player at the spawn, from #168, which waits for scripted control of the player.

## 1. What dialogue stops

- [x] 1.1 What could pause, and what pausing would mean for each (Claude):
  - **Physics** (bodies, gravity, joints, motors): stopped, or running, so that things settle.
  - **The player's controller**: not stepped, or stepped with neutral input, as a scripted takeover would step it.
  - **Other controllers** (robots, platforms, doors): not stepped, which needs physics stopped too, or bodies drift without their motors; held, braking to a stop; or running.
  - **Scripts and triggers**: they run only on played ticks.
  - **Game time** (coyote time, cooldowns, a door's timing): follows physics.
  - **Animations** and **shader animations**: should run, and look right only if what drives them runs too.
  - **The camera**: already runs while paused.
  - **Sound**: muffled in every mode that pauses.
  - **Input**: the box needs jump, up and down; the player's controller shouldn't act on them.
  - **The menu and the map**, opened from dialogue: still a full pause.
- [x] 1.2 What dialogue pauses. Decision (Claude, agreed by Neil): not the tree. Physics, every controller, scripts, game time, animations, shaders and the camera run as in play, and the player's controller gets neutral input, through `WorldNode.PlayerInput`, which `Main` already supplies. The player then falls, lands, slows to a stop and stands by the normal path, with no special case. Rationale: how animations are driven, from properties the controllers set as they step, already means the world's simulation has to run for them to look right. Neil: "I like the idea that the game world exists and moves independently of the player's story." Cost (Neil): keeping physics running makes scripting cutscenes and dialogue harder, a challenge he expects to be fun.
- [x] 1.3 Other controllers keep running, and aren't held (Claude, agreed by Neil). Holding them would need a hold behaviour in each, such as a robot braking and not firing, before any dialogue needs it. Dialogue is placed where the world can't harm the player, as in The Call.
- [x] 1.4 `DialogueRunner`'s wait for the player to be on the ground (`044`'s, up to 2 seconds) was there so the game didn't pause with the player in the air. With the world running, the player lands under the box. Decision (Neil): the wait is gone; it was unnecessary.
- [ ] 1.5 Dialogue asked for while a phase is aimed. The time scale goes back to 1 at once, and neutral input releases phase with no direction held, which cancels it. Neil, playing: starting a dialogue while aiming cancels the phase, and the player moves on; phase held through the dialogue aims again as soon as it ends (#174's held button). Claude, from `PhaseAction`: with a direction held when the dialogue starts, the player's controller sees phase and the direction let go of on the same tick, so the release leniency (`018 §3.1`) phases in that direction on the cutscene's first tick. Confirmed by Neil in play. To be changed, likely once controllers can see the game's mode (item 2.3).

## 2. Cutscenes

- [x] 2.1 The mode is `Cutscene`, not `Dialogue` (Neil): the same game rules should apply whether the player is being scripted to walk up to a powerup or talking with a character. When `GameModeManager` has dialogue state to track, it becomes optional state of the cutscene mode.
- [x] 2.2 Controllers, and other parts of `Simulation` and `Entities`, are given the game mode (Neil). Whether or not robots come to stop attacking or walking in a cutscene, other rules will need to know: kinetic damage, if it is built, would likely be off in cutscenes.
- [ ] 2.3 How the mode reaches them. `GameMode` is the game's, in `GameModeManager`, and Core can't see it; a type for it in Core must fit the subsystems' dependency rules. To be designed soon, with the plumbing.

## 3. Time and sound

- [x] 3.1 Shader time. Neil, from reading online: a shader's `TIME` follows `Engine.TimeScale` but not the tree's pause. So the slowdown before a phase would slow every shader animation for half a second, and releasing the phase would snap it back to full speed. Decision (Neil): the game defines a time uniform of its own and advances it, when the first animated shader comes. No issue for it: anyone would notice the first time an animated shader is on screen.
- [ ] 3.2 Which clock that uniform runs on: real time, as `AnimationStepper` counts its steps, or game time that the phase's slowdown and pause leave alone. Open until the first animated shader.
- [x] 3.3 Muffling. The cutscene mode doesn't muffle sound (Neil). He liked dialogue muffled, but cutscenes in general shouldn't be, and he is considering musical cues during dialogue, or motifs played after the player's responses, which need sound clear.

## 4. Input

- [x] 4.1 The press that ends a dialogue doesn't reach the player's controller during the cutscene, since it gets neutral input then. A button still held when play resumes is #174's: for example, play ignores a button until it is released.
- [x] 4.2 The game's viewport processed always so that the dialogue box got input while dialogue paused the tree (#174), and the world in it was made pausable. With the tree running for cutscenes, dialogue no longer needs either. Neil took both out and the game, the map included, still works, so the viewport pauses with the game again. The dialogue box processed always too, for the same reason; it now pauses with the game, so its typing stops while the menu is open over a cutscene (Neil).
