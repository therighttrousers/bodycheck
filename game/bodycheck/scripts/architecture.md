# `game/bodycheck/scripts/`

Relevant decision records:
- [051: The scripted run at the spawn](../../../docs/decision-records/051-scripted-run-at-the-spawn.md)

## What it is

The scripts that `Script` entities run, and `ScriptLibrary`, the game's script library, which finds a script by its class's name without the namespace, so that levels name scripts briefly. See [Core's `Scripts`](../../../src/BodyCheck.Core/Scripts/architecture.md#scripts) for what a script is (`051 §2.2`, `§2.3`).

## What it is not for

What runs scripts, or what they see of the world, which are Core's; or a cutscene that isn't in a world.
