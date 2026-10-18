# Levels

## What it is

Reading the levels that are authored in LDtk into a world of rooms, one per level, with every value checked as it is read. It holds the types that describe LDtk's JSON.

## How it relates to the rest

It produces the world's data, so it depends on the subsystem that holds that data and on Core's root. It doesn't read files itself; it is given a way to read them, so the game and tests can each use their own.

## What it is not for

Saving or loading the game's state. Deciding what a room means once it is read. Formats other than LDtk's.
