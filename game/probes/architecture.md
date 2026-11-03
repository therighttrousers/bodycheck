# `game/probes/`

Relevant decision records:
- [031: Tests in the game's project](../../docs/decision-records/031-tests-in-the-game-project.md)

## What it is

Scripted measurements, run headless with `--probe=` and a name, which print what they find. Each loads a world of one test room and drives it, a physics tick at a time, before the world's own tick.

## What it is not for

Checking that something holds: that is a test's job. A probe measures, for tuning and for design decisions.

## How probes run

Probes are only in the editor's build, `Debug`, where Godot defines `TOOLS`; exports leave them out (`031 §2.3`). `Main` runs the probe the command line names, and quits when it is done. Probes run with nothing pausing for them, so a probe's player aims a phase while the world runs.
