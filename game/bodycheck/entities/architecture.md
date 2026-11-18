# `game/bodycheck/entities/`

Relevant decision records:
- [021: Sound](../../../docs/decision-records/021-sound.md)
- [035: One shape for actions](../../../docs/decision-records/035-one-shape-for-actions.md)

## What it is

The scripts on the nodes of entities' scenes, which make an entity look and sound as its state and its cues say: `CueSounds` and `LaserBeam`.

## What it is not for

What an entity does, which is Core's, or which scene an entity of each kind gets.

## The scripts

- `CueSounds`, in a character's scene, or in a door's look, which is built in code, plays the sounds for its entity's cues (see [models.md](../../../docs/designs/models.md#sounds)). Characters' sounds are `AudioStreamPlayer3D`s, which the camera hears from where it is. It can only because `WorldViewportContainer` turns on 3D audio listening in the game's viewport, which the camera is in: a `SubViewport` leaves it off, and then no 3D sound is heard at all. Entities' sounds pause with the tree.
- `LaserBeam`, in the robot's scene, draws the robot's laser from its state each tick, and plays its sound from when it locks until it stops firing, and stops it if the laser gives up first: a sound that lasts as long as a stage follows the stage, not a cue (`035 §6.1`).
