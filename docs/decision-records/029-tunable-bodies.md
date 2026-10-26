# 029: Tunable bodies

Closed on 2026-10-25. Outcomes are in [architecture.md](../designs/architecture.md).

## Context

Inciting event (2026-10-25): issue #87, "Make BodyTuning mass settings tunable". A playtester wanted to tune the masses of objects, and had to edit code and rebuild to do it. Neil had wanted to tune a body's engine friction the same day.

`016 §3.4` kept `BodyTuning` out of `Features`, since some of its values are built into the engine's bodies, and `016 §3.1` allows a feature only if nothing derives from or caches it. A body's mass is the engine's: what reads it, such as the phase and the platform's controller, reads the body's. The issue suggests listening to `Features.Changed`, and warns against events keeping nodes alive.

Found while reading the code: `TileDefaultFriction`, already a feature, is copied into the tiles' body when the world is built, so switching configurations doesn't change it until the world is built again.

## 1. Which values

- [x] 1.1 Which `BodyTuning` values become features? Decision (Neil): the mass, and the engine friction. Each is per kind of body, so each kind has its own: `PlayerMass`, `RobotMass`, `PlatformMass`, `PlayerBlockMass`, `BlockMass`, and the engine friction of the bodies that have any: `PlatformEngineFriction`, `PlayerBlockEngineFriction`, `BlockEngineFriction`. They are functions of the features in `BodyTuning`, like its carrying values (`019 §6.1`).
  - Not the half-size, which the collision shapes and the visuals are built from, nor the collision layer or whether a body can be carried, which are what kind of body it is (Claude). The drag would qualify, as `016 §3.4` says, and is read each tick, but no one has asked for it.
  - The engine friction is already read each tick, when the entity sets its friction for the engine's step (`027 §1`), so it needs nothing more.

## 2. Bringing the engine up to date

- [x] 2.1 How does a change to a feature reach the engine's bodies? Options:
  - (a) Subscribe to `Features.Changed`. Something must unsubscribe, or the event keeps what it calls alive. A weak event wouldn't make it safe: a node's C# object can outlive the node Godot frees, so a handler could run on a freed node before the object is collected (Claude).
  - (b) Each tick, copy the features into the engine, before anything reads them. Nothing to subscribe to, so nothing to leak. It costs a few comparisons per entity per tick, light beside the rest of an entity's tick (Neil).
  - Decision (Neil): (b). `WorldNode` does it at the start of each tick, its paused ticks too, before any entity steps: the phase and the platform's controller read other bodies' masses, so if each entity set its own as it stepped, what a controller saw would depend on the order of the steps. Each value is set only when it differs, since Godot reloads a body's material on every change, even to the same value.
- [x] 2.2 This relaxes `016 §3.1` (Neil, Claude): a feature may be copied into the engine at the start of each tick, by whatever owns what it is copied into. A change then takes effect at the next tick, which is within the bound `016 §3.1` sets.
- [x] 2.3 `TileDefaultFriction` is brought up to date the same way, by `WorldNode`, which owns the tiles' body.
- [x] 2.4 Results, 2026-10-25: 136 of 136 tests pass, among them a new one, `A_change_to_a_mass_feature_takes_effect_at_the_next_tick`. Tests that gave the player another mass through its `BodyTuning` set `PlayerMass` instead.
- [x] 2.5 Copy them only when the features change? Neil, after 2.1: listening to `Features.Changed` looked easy, if Godot's nodes, C# objects wrapping the engine's, don't make the lifecycle hard. Tried (Claude): `WorldNode` subscribes in `_EnterTree` and unsubscribes in `_ExitTree`, and its handler only marks the features changed, so they are still copied at the start of the next tick, as in 2.1, but only then. Entering the tree marks them changed too, in case they changed while it was out.
  - Godot takes a node out of the tree before freeing it, whether by `Free` or `QueueFree`, so the event never holds a freed world. Checked by counting the event's handlers after each, after taking a world out of the tree without freeing it, and for a world never in the tree, which never subscribes. 136 of 136 tests pass.
  - Decision (Neil): keep it. It is a few lines, its lifecycle is Godot's usual pairing of entering and leaving the tree, and the tick does nothing when nothing changed (Claude). Copying every tick would have cost little, but it is a proof of concept of listening safely (Neil). An entity's `UpdateFromFeatures` puts its mass in the engine, and is named for whatever else of its tuning the engine comes to hold.
