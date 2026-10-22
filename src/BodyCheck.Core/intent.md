# `BodyCheck.Core`

## What it is

The game's rules, as a class library, split into subsystems, one per subdirectory. The root holds only what several subsystems, or the game, share: utilities, such as telemetry; constants; the cues that entities raise for the game to react to, such as with sounds; and the features, the values that shape gameplay, which the subsystems read and the game will let developers change.

## How it relates to the rest

Every subsystem may use the root. The root uses no subsystem, so it stays at the bottom of every dependency chain.

## What it is not for

Anything that belongs to one subsystem, even if it is small, except a feature: a value that shapes gameplay belongs in the features, even if only one subsystem reads it. Anything derived from a feature or cached from one, and settings that belong to a machine rather than to gameplay, such as graphics. Anything that knows the Godot project's scenes or asset paths: those are in `game/`.
