# Simulation

## What it is

The world brought to life: its tiles turned into colliders, its entities created where they start, each given its controller, and all of them stepped every physics tick. Tests use it as it is, so it has no camera, lights for the scene or input device of its own.

## How it relates to the rest

It sits on top of the world's data and the entity and controller subsystems, and joins them; none of them depend on it. The game gives it what to draw for each kind of entity and where the player's input comes from, so it knows no assets and no devices.

## What it is not for

Presentation: the camera, the scene's lighting, overlays, the rendering pipeline. Choosing or switching worlds. The rules of a single entity or controller, which belong in their own subsystems.
