# 021: Sound

## Context

Inciting event (2026-10-21): issue #75, "Add event sound infrastructure and sound for the phase ability", on branch `phase-sounds`. The game has no sound yet. Neil is gathering placeholder sounds; the real ones will be our own, inspired by mid-1990s games.

Goal: sound for starting a phase and for the throw, in a shape that later sounds can follow, and background music, which is the best way to test muffling (section 2) by hand.

Anti-goals, for now: sounds for NPCs and other abilities. Choosing music by world or room.

Overarching questions:
- How does logic, which knows when something happens, get a sound played without knowing the game's assets?
- How is every sound in the game muffled while a phase is aimed?

The engine's pieces, for reference:
- An `AudioStreamPlayer` plays one sound. `AudioStreamPlayer3D` plays one at a place in the world.
- The `AudioServer` mixes players into **buses**. Every player sends to a bus, `Master` by default, and a bus may send to another. A bus has a chain of effects, such as `AudioEffectLowPassFilter`, whose `cutoff_hz` can be changed while it plays. The layout of the buses is saved in `default_bus_layout.tres`.
- An `AnimationPlayer` clip can have audio tracks, which play sounds at times on its timeline, and method tracks, which call methods.

Tensions:
- Aiming a phase slows the game down over half a second and then pauses the tree (`015 §5.12`). An entity's `AnimationTree` stops while the tree is paused, and so, in Godot 4, does an `AudioStreamPlayer` that doesn't process while paused (to be checked).
- A phase's throw happens on one tick. Animation trees read state, not events, and run on frames, not ticks.

## 1. Where sounds come from

- [x] 1.1 Who decides that a sound plays? Decision (Neil, 2026-10-21): logic raises events, such as a phase starting or its throw, and something in the game listens and plays the sound. Controllers and entities stay logic, and only the game knows the sounds.
- [x] 1.2 What about sounds that follow a body's animation, such as footsteps? They are audio tracks in the animation's clips, in the character's scene, timed by the artist, and need no code: the scene is the interface between code and art, as for animation (`006 §4.1`). None are planned yet.
  - Tried (Neil, 2026-10-22) with a jump sound on Bob's `bob_rig|jump`. It took a dozen steps across two dialogs, since an imported animation must first be saved to a file of its own to take a track (now in [models.md](../designs/models.md#adding-an-audio-track-to-an-imported-animation)). The sound goes on the track's key, not on the player, whose stream the editor clears on saving. And it played late, sometimes very late, and sometimes not at all: a track plays only when the state machine reaches its clip, after the transition's blend. Jumping became a cue, `Jumped`, which was easy and plays on the tick.
  - So a cue is for a sound that marks when something happens, and a track for one that follows a motion: footsteps, which keep time with the run cycle's speed; points along a long animation, such as a door's creak or the stages of a death; an idle's fidgets. A cue can start such an animation, and its tracks time the sounds within it.
  - They can't carry phase's sounds: the aim begins as the tree pauses, phase is not something the body animates, and a throw that lasts one tick is easy for an animation tree to miss.
- [x] 1.3 Where do phase's events live? Decision (Neil, 2026-10-22): D, named cues, because the sounds then belong to the art, as animation does. Options:
  - A. C# events on `PlayerController`, raised by `ApplyPhase` when an aim begins and when it throws, each with the `PhaseAction`. A node in the game subscribes through `WorldNode.PlayerController`, as `PhaseAimMarker` reads the controller's `Phase`. Core already raises events this way (`Features.Changed`). Leaning (Claude).
  - B. On `PhaseAction`. There is a new one for each aim, so a listener would need to hear of each new one first, which is A's start event anyway.
  - C. On the entity, which the controller tells, as it publishes its intent for animation (`006 §4.3`). Then sounds could live in the character's scene beside its animation. But entities would learn of phase, which is behaviour (Entities' `intent.md`), or would need a general way to raise cues, which is deferred below.
  - D. Named cues, explored at Neil's asking (2026-10-22). An enum of cues in Core's root, `EntityCue` as built,, such as `PhaseBegan` and `PhaseThrown`, beside the features that already name phase's values; Entities can't hold it, since it names behaviour. `Entity` has a method that controllers call with a cue and an event that it raises. In the character's scene, beside its `AnimationTree`, a node holds one player per cue, each named after its cue, under one generic script from the game that subscribes to the entity it is under and plays the player named after each cue, or nothing if there is none. Sounds are then chosen and placed in the character's scene, as animation is (`006 §4.1`), and a new sound for a new cue needs no new code in the game. As with the tree's parameters, a misspelt name is silent.
    - The sounds would be under the entity, which pauses with the tree, so phase's start sound stops when the pause begins, half a second after it starts. Neil: fine, if the sound is shorter than that or has faded enough by then (2.6).
    - Sounds with no entity need something else. Doors are to be entities, and will talk, so that leaves music (section 5).
  - Named `EntityCue` (Neil, 2026-10-22), not `SoundCue`: a cue names what happened to the entity, not what the game does about it, so particles, one-shot animations, camera shake or rumble can react to the same cues later. Not plain `Cue`, since the game will have other kinds of cue.
  - Events are not state, so A is no second source for it. The throw's event is raised on release, while the game is still paused; the move happens on the next tick, too soon to hear the difference.
  - Deferred: a general way for any entity to raise named sound cues that the game routes to sounds. Worth it once several abilities and NPCs make sounds and one listening node for each becomes repetitive.
- [x] 1.4 Does the start sound come from the controller's start event, or from the game's mode changing to `SlowingForPhase`? They happen on the same tick or one apart. Decision, following from 1.3: from the controller, which cues it on the player.
  - The mode is the slowdown, and the slowdown is any controller's request for a pause, of which phase is only the first. The start sound belongs to phase; the muffle belongs to the slowdown (2.2).
  - Opening the menu during the slowdown and closing it goes back to playing, and then to slowing for the phase again, so the mode's start sound would play twice for one aim. The controller's event happens once for each aim.

## 2. Muffling while a phase is aimed

- [~] 2.1 How? Leaning: a low-pass filter on the `Master` bus, whose cutoff falls as the game slows down for a phase, over the same half second of real time, and rises when the phase is released or cancelled. Everything goes through `Master`, so it muffles the music, sounds still playing and the start sound itself.
- [x] 2.2 What drives the cutoff? Decision (Neil, 2026-10-22): C. Options:
  - A. `GameModeManager`'s slowdown tween sets it beside the time scale. In step by construction, but the mode manager learns about audio.
  - B. A node in the game listens to `GameModeChanged` and runs its own tween over the same time. Two timers that must agree, and `SlowdownTime` shared between them.
  - C. A node in the game that processes always sets the cutoff each frame from the game's mode and the engine's time scale: open while playing, falling with the time scale while slowing for a phase, closed while phasing. No timer of its own and no state of its own, so it can't drift from the slowdown, and a change to `SlowdownTime` carries over. The time scale is 1 again while phasing, so the mode is needed as well.
  - Why C: the cutoff is a function of the engine's state, not a second copy of it. The curve of 2.3 becomes the cutoff as a function of the time scale, which is the same as over time while the slowdown is linear.
  - With C, 2.4 and 2.5 are answered by what the function gives for each mode: playing is open at once on release, and the menu and map are whatever we choose for them.
- [ ] 2.3 The cutoff's curve: from 20 kHz down to what, and falling evenly in frequency or in octaves? Even in octaves likely sounds even to the ear. To be tuned by ear.
- [x] 2.4 Does sound come back at once on release, as the time scale does, or over a few ticks? At once, as follows from 2.2: the mode is playing again, so the filter is open, and the throw's sound plays unmuffled.
- [x] 2.5 Opening the menu or the map during an aim puts the time scale back to 1. Does the muffle go too? Decision (Neil, 2026-10-22): no. Sound is muffled while the menu or the map is open, whether or not a phase was being aimed. The filter itself, and whether it is the same as the phase's, is to be found by experiment (2.3).
- [~] 2.6 Which sounds keep playing during the pause? If players that don't process while paused are paused with the tree (to be checked), an entity's sounds stop with the world, and the music, which processes always, plays on, muffled. Leaning: that is what we want; phase's start sound is made to end or fade within the slowdown.

## 3. Cancelling a phase

- [x] 3.1 Does cancelling need a sound? Decision (Neil, 2026-10-22), after trying one: yes, a "critical error" sound from Freesound, CC0. Neil (2026-10-21): perhaps not. The sound coming back without the throw's sound may be cue enough. To be decided by playing. As built, the controller raises a `PhaseCancelled` cue, which costs nothing and is silent until the player's scene gives it a sound, so a cancel sound can be tried without code.

## 4. Buses and assets

- [x] 4.1 Separate buses for sound effects and music, sending to `Master`? They are for setting their volumes apart, which nothing does yet; the muffle is on `Master` either way. Decision (Neil, 2026-10-22): yes, `Sfx` and `Music`, now, since they are cheap.
- [ ] 4.2 The start sound lasts 0.5 seconds to match the slowdown, which is a constant in `GameModeManager`. If the slowdown is tuned, the sound won't follow.
- [x] 4.3 Placeholders: Neil downloads only CC0 sounds for placeholders, from Freesound, which has plenty. They need no attribution. The first are in `game/assets/player_sounds/`.

## 5. Background music

- [x] 5.1 How is music played? Decision (Neil, 2026-10-22): one `AudioStreamPlayer` in `Main.tscn` that processes always and plays on start, looping, so it plays through the pause, muffled. The loop is set in the track's import settings.
- [x] 5.2 A placeholder track: Neil found a CC0 one on Freesound, now in `game/assets/music/`. Which music plays where is for later.
- [x] 5.3 Does music play on while the menu or the map is open? Decision (Neil, 2026-10-22): yes, muffled (2.5).
- [ ] 5.4 Run headless with `--quit-after`, the game now reports the music's playback and stream as leaked at exit. Stopping the player in `Main._ExitTree` didn't help. Likely Godot's shutdown with the dummy audio driver; unchecked with a real one. Probes, which quit the same way, will show it.
