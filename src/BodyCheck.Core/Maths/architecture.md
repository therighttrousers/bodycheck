# Maths

Relevant decision records:
- [057: Tile kinds](../../../docs/decision-records/057-tile-kinds.md)

## What it is

Standard mathematics that the game's code uses, each piece self-contained: it knows numbers, vectors and grids, not entities, worlds or the engine's nodes. Each is a technique with a name in some field, such as physics, signal processing or computational geometry, so that its doc comment can say what it is and where to read more.

## What it is not for

Gameplay rules that happen to be arithmetic, such as a controller's tuning or how an action decides; a technique the game has only one use for and has adapted to it; or anything Godot's own maths already does, such as clamping, wrapping, interpolating or moving toward a value. A piece moves here once it is standard and has, or plainly will have, another use.

## What it holds

- **`Rand`**: random numbers from a string seed that are the same on every run and every .NET version (`057 §2.4`).
- **`Spring`**: critically damped springs. Their exact motion over a frame of any length, the force that makes a body move as one, and the smoothing time that makes one peak at a given speed. A spring is given by its smoothing time, about the time it takes to cover 60% of the way.
- **`Smoothing`**: exponential smoothing, the first-order counterpart of a spring, also given by its smoothing time, exact for a frame of any length.
- **`Boxes`**: distances to axis-aligned boxes, given by their centres and half-sizes.
- **`GridRay`**: lines cast through a grid of unit cells, such as tiles, to the first cell a predicate is true of, by Amanatides and Woo's traversal. It needs nothing in the scene, unlike the engine's ray casts.
