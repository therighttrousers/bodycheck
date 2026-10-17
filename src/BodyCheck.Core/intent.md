# `BodyCheck.Core`

## What it is

The game's rules, as a class library, split into subsystems, one per subdirectory. The root holds only what several subsystems, or the game, share: utilities, such as telemetry, and constants.

## How it relates to the rest

Every subsystem may use the root. The root uses no subsystem, so it stays at the bottom of every dependency chain.

## What it is not for

Anything that belongs to one subsystem, even if it is small. Anything that knows the Godot project's scenes or asset paths: those are in `game/`.
