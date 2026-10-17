# Rooms

## What it is

Rooms as data: a grid of tiles, and which kinds of entity start where. It also holds the libraries of rooms described as text, for playing and for tests.

## How it relates to the rest

It depends only on Core's root, and knows nothing of entities as bodies, only their kinds. Other subsystems produce rooms or turn them into something that runs.

## What it is not for

Anything that runs: engine nodes, bodies or colliders. Reading files in an editor's format.
