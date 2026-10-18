# WorldData

## What it is

The world as data: its rooms, each a grid of tiles at a place in the world, and which kinds of entity start where. It also holds the libraries of rooms described as text, for playing and for tests.

## How it relates to the rest

It depends only on Core's root, and knows nothing of entities as bodies, only their kinds. Other subsystems produce the world's data or turn it into something that runs.

## What it is not for

Anything that runs: engine nodes, bodies or colliders. Reading files in an editor's format. Which room an entity is in once it moves, which is a question about what runs.
