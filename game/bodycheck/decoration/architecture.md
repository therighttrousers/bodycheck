# `game/bodycheck/decoration/`

Relevant decision records:
- [062: Background layers](../../../docs/decision-records/062-background-layers.md)

## What it is

The decorators, which add things to each room of an environment, such as a back wall, made procedurally from the room: `RoomDecorator`, their base, a `Resource` whose exported properties are a decorator's settings; `RoomDecorations`, the node named `Decorations` in an environment's scene that lists its decorators; and `BackWall`, with its `BackWallGrowth`s. See [rendering.md](../../../docs/designs/rendering.md#environments).

## What it is not for

Anything that depends on the scene tree or the game's state, or that changes as the game runs.

## Decorators

A decorator is pure and deterministic, depending only on the room, its settings and Core's `IAssetLibrary`'s meshes, never on the scene tree or the game's state, and makes engine nodes in the room's coordinates, so that it could run in the editor to preview a room (`062 §3.6`, `§3.7`).
