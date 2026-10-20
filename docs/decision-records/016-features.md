# 016: Features

## Context

Inciting event (2026-10-20): work on issue #58, "Add a `Features` class to `BodyCheck.Core` to store feature toggles and values centrally". The issue names graphics settings, most of `PlayerTuning`, and other knobs scattered through the code that are worth changing at run time. A developer menu over everything in `Features` follows in another PR.

Goals:
- One place for the values that shape gameplay, so that configurations of them are easy to change and, eventually, to share.
- A way to reset them to their defaults, for tests now and for a menu option later (Neil).

Anti-goals: the developer menu, loading from or saving to JSON, and the metadata either needs, such as ranges and labels.

Tension: the design avoids second sources for state the engine holds. `Features` is meant to be the only source of what it holds, so it must not be copied into state that would then be out of date (Neil).

## 1. Where it lives

- [x] 1.1 Core's root uses no subsystem, so a `Features` there can't hold a `PlayerTuning` or a `PhaseThrow`, which are in `Controllers`. Options:
  - (a) `Features` in the root holds plain values. `PlayerTuning`'s values move into it, and controllers read it.
  - (b) A subsystem that nothing else in Core uses, which pushes values into what uses them. Cleaner layers, more plumbing, and not central.
  - (c) Change the rule.
  - Decision (Neil): (a). What of `PlayerTuning` isn't a feature moves into `PlayerController`, and `PlayerTuning` is dropped, so it is no longer passed around. As it turned out, every value of `PlayerTuning` is a feature, and `PhaseThrow` moves to the root with them.

## 2. Its shape

- [x] 2.1 A static global, or an instance passed down to what uses it? Decision (Neil): a static global, with a way to reset it to its defaults. Tests reset it in their fixtures, so a test may change features safely, and a menu option will reset it later.
- [x] 2.2 How it is held and changed. Decision (Claude, agreed by Neil): an immutable record, `Features`, whose defaults are its properties' initializers, behind a static `Features.Current`. `Features.Set` replaces it and raises `Features.Changed`; `Features.Reset` is `Set(new())`. The defaults are written once, every change goes through one place, a test changes one value with a `with` expression, and loading from JSON will be reading a record and setting it. Properties are uniform, for reflection by the menu later.
- [x] 2.3 Do changes need telling? Decision (Neil): a single `Changed` event, even unused, since one line of code is shorter than a comment on how to add one. Its doc comment says to avoid needing it: to read features where they are used, and not derive from them or cache them.

## 3. What goes in

- [x] 3.1 What may be a feature? Decision (Neil): simple values that are never derived from or cached elsewhere, since `Features` is the only source of truth and reset must take effect without anything propagating it. Not `BodyTuning.HalfSize`, for example: the collision shapes are built from it.
  - Nuance (Claude, agreed by Neil): an action has to cache or derive from features, such as the jump's thrust ticks, counted from take-off, or the velocity given by `JumpSpeed`. That is state in flight, not a derived fact, and it is fine for it to be out of date until the action ends: a change takes effect at the next action.
  - Phrasing (Neil, Claude): changes are eventually consistent, with a bound. What reads a feature sees the change at once, except an action in progress, which goes on with the values it started with. Bare eventual consistency would allow a cache that catches up later, which this rules out.
- [x] 3.2 Graphics settings? Decision (Neil): not now; they stay in `LookSwitches` and `ColorPass`. Two reasons (Claude):
  - Every one of them is derived into the engine or the GPU: the viewport's size, its anti-aliasing, the colour pass's uniforms. That is the propagation §3.1 rules out.
  - Their lifecycle differs. A shared gameplay configuration shouldn't change someone's anti-aliasing, and resetting gameplay to its defaults shouldn't turn the colour pass back on. They belong to a machine and a viewer.
- [ ] 3.3 Graphics settings' lifecycle and persistence: where they live, how they are saved per machine, and whether they get a record of their own with the same pattern. Open (Neil).
- [x] 3.4 Other knobs? Decision (Neil): `Features` is for gameplay. Not `CameraRig`'s values, the slowdown to the phase pause, the controller's dead zone for directions, or the debug toggles. `BodyTuning` stays out too (Claude): its drag would qualify, but it is per kind of entity, beside values that are built into the engine's bodies.
- [x] 3.5 Robots' and platforms' tuning? They are simple gameplay values too, read each tick (Claude). Decision (Neil): features, since a playtester designing puzzles will want to change them. `RobotTuning` and `PlatformTuning` are dropped like `PlayerTuning`.
- [x] 3.6 Flat or nested? Decision (Claude, agreed by Neil): flat, so that a change is one `with` expression, not one nested in another. Names: robots' and platforms' values are prefixed with `Robot` and `Platform`, since their names clash with the player's. Changed (Neil): the player's are prefixed with `Player` too, for uniformity.

Closed on 2026-10-20. Outcomes are in [architecture.md](../designs/architecture.md) and [progression.md](../designs/progression.md); unresolved items are in architecture.md's open questions.
