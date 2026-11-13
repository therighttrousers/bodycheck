# Entities

Relevant decision records:
- [005: Jolt's joint motors for traction](../../../docs/decision-records/005-jolt-motors-for-traction.md)
- [006: Models, their base point, and animation](../../../docs/decision-records/006-models-and-animation.md)
- [015: The phase ability](../../../docs/decision-records/015-phase.md)
- [019: Carrying blocks on walkers](../../../docs/decision-records/019-carrying-blocks.md)
- [021: Sound](../../../docs/decision-records/021-sound.md)
- [027: Engine friction for walkers and carrying](../../../docs/decision-records/027-engine-friction.md)
- [029: Tunable bodies](../../../docs/decision-records/029-tunable-bodies.md)
- [032: Player-only situational friction](../../../docs/decision-records/032-player-only-situational-friction.md)
- [034: A controller is built with its entity](../../../docs/decision-records/034-controller-built-with-its-entity.md)
- [035: One shape for actions](../../../docs/decision-records/035-one-shape-for-actions.md)
- [043: Triggers, actions and scripts](../../../docs/decision-records/043-triggers-and-scripts.md)
- [046: Actions as async methods](../../../docs/decision-records/046-async-actions.md)
- [047: `Once`, and routing actions](../../../docs/decision-records/047-once-and-routing.md)
- [049: Doors and their stops](../../../docs/decision-records/049-doors-and-stops.md)
- [050: The cutscene mode](../../../docs/decision-records/050-cutscene-mode.md)

## What it is

The things in a room that have a body, as the engine's own rigid bodies: their shape and mass, their contacts, and the motors with which they grip what they stand on: friction, a walker's traction, and a carrier's steadying of what it carries. It also says what a controller of a body is, without any particular one, and names the cues an entity raises, for whatever draws it to react to.

## What it is not for

Behaviour: what the player's input or an NPC makes a body do. Where entities start, or which ones a room has. Saving and loading.

## Bodies

- An `Entity` is one thing in a room that has a body: the player, a robot, a platform, a block. It is the engine's rigid body itself, so what the engine reports about bodies is about entities. Its body values are a `BodyTuning`, and it is given what to draw.
- A `BodyTuning` includes the body's physics layer and mask, how it grips what it stands on and what it carries, whether the player can phase through it, and whether it is locked on X, as a door is, so that nothing pushes it sideways (`043 §6.3`); its presets read those, its mass and its engine friction from features, through functions of no arguments that read `Features.Current`, so that each is read when it is used, as an action's tuning is (`019 §6.1`, `029 §1.1`, `035 §3.1`). The engine holds the mass, so whatever steps the entities puts each one's there when the features change, before any entity steps (`029 §2.1`, `§2.5`).
- An entity's origin is the centre of its body, where its collision box is centred. What it draws has its origin at its base, and the entity lowers it by half the body's height (`006 §2.1`).
- A subclass is a kind of body that gives its controller more to move it with: `WalkerEntity`, whose controller drives it with `ApplyTraction`, and `PlayerEntity`, a `WalkerEntity` whose controller can also let it slip, with `SlipAfterEntitySteps`, and whose friction changes with what it touches and does (`005 §3.8`, `032 §3.1`, `§3.3`). A robot is a `RobotEntity`, a `WalkerEntity` with a laser. A platform is a `PlatformEntity`, which its controller tells how hard it is working, for its model's face. A block is a plain `Entity`, and so is a vertical door.

## Motors and friction

An entity owns its motors, so that they live exactly as long as it does: each is a joint to what the entity stands on, which it anchors again whenever that changes. Every entity has the steadying, for its carrier to hold it with, and a walker also has the grip, for its traction; `Joints` makes and anchors them. When its world removes another entity, an entity forgets it, wherever it remembers it from the engine's last step: as what it stands on, what is above it, or what a motor is anchored to, so that it never acts on a freed entity; one that remembered it is woken, so that its next step finds what it touches now (`056 §3.5`). Each tick an entity switches its motors off, then applies its carrier's steadying, before its controller runs. Its friction is the engine's, at its tuning's coefficient, which it puts in the engine when the features change; only the player sets its own after it steps (`019 §1.2`, `§5.1`, `027`, `032 §3.1`). See [movement.md](../../../docs/designs/movement.md#ground-contact-friction-and-traction).

## Moving through things and querying

Every entity can move through other entities, but not through tiles, at once, or straight to a place found to be clear of tiles, can find the colliders that overlap a shape, and can find how far a box can move before it meets colliders, as of the last step (`015 §4.1`, `§5.20`, `§5.21`). It can also cast a ray, with `Entity.CastRay`, so that any kind of body can aim a laser (`035 §6.1`).

## Controllers

- Any entity may have a controller, which moves it with forces, impulses and, for a walker, traction; a block has none (`005 §3.6`, `034 §1.3`). The player and the NPCs use the same interface, `IController<TEntity>`.
- A controller is built with its entity, as the kind of body it needs, so the wrong controller for a body doesn't compile, and it controls only that one: `Entity.Controller` throws if given a controller built for another entity (`034 §1.1`, `§1.4`). `TEntity` is covariant, the kind of body the controller's `Entity` is, so `Entity.Controller`, an `IController<Entity>`, takes a controller of any kind (`034 §1.6`). The interface names only `Entity`, so entities don't depend on any controller (`034 §1.5`).
- The interface's `Apply` and `ApplyPaused`, `IDefer`, and the pause request are described in [how a tick runs](../architecture.md#how-a-tick-runs).
- **Changes of the game's mode.** `IController.GameModeChanged(from, to)`, a default member that does nothing, is where a controller changes state that would mean something else in the new mode, such as remembered input. The world calls it on the tick, before the controller steps, when the mode differs from the one it last stepped its controllers in, played or paused; so a trip to the menu or the map and back is no change, since no controller runs meanwhile. It is called for every change, the phase's slowdown and pause included, so a controller tests for the modes it cares about. Not an event, which would change a controller's state off the tick (`050 §4.3`).
- **What a controller sees of the world.** `IWorldView` is what a controller can see beyond its entity: the player, the player's block and the game's mode: what a mode means for a controller is the controller's to decide. It is also where a controller can `Remove` an entity, and `Entity.IsLive` says whether an entity is still in its world, from the moment it is asked to be removed (`056 §3.5`). The simulation implements it, and gives it to each controller through `IController.Ready`, a default member that does nothing, called once the world is built, or at once for an entity added to a built world, before the controller runs. So a controller has no world before the world is whole, and needn't know the order it is built in. A controller keeps it there, and reads it each time it needs it, never once and kept, so that an action that waits across ticks sees what is current. It grows when a controller needs more (`050 §2.3`).
- **Actions.** `IActionTarget` is what a route can run an action on. It has a method for each `TriggerAction`, such as `Close`, which takes the `ITick` of whatever runs it and returns a `TickTask` that finishes when the action does. A controller runs its action on that tick scoped to its entity being live, since a controller means nothing without its entity, so that removing the entity cancels the action (`056 §3.4`); each is a default member that throws, so a target implements only the actions it responds to, often as `async` methods (`047 §2.2`). A body's controller is the target of the actions routed to its entity, since behaviour is a controller's, so `IController` extends `IActionTarget` (`043 §2.1`, `§3.2`). The world checks every route when it is built, so no target is asked for an action it doesn't respond to.

## The robot's laser

`RobotEntity.Laser` is the laser's state each tick, which its controller sets: its stage in the firing timeline, and where its beam starts and ends, for its scene to draw. The beam starts at `RobotEntity.LaserOrigin`, which the robot reads once from its visual, as `Entity` finds its `AnimationTree`: where a node named `LaserOrigin` is in the model's rest pose, with any bone it is attached to at rest, as a height and a distance ahead in the direction it faces, so that animation never moves it (`035 §6.1`).

## Cues

`EntityCue`, an enum beside `Entity`, names what can happen to an entity. A controller raises one with `RaiseCue`, which raises the entity's `CueRaised` event (`021 §1.3`).

## Timing in names

An entity's methods follow [the naming convention for timing](../../../docs/designs/architecture.md#conventions): `Entity.SetFrictionAfterEntitySteps`, `PlayerEntity.SlipAfterEntitySteps` and `Entity.SetGravityScaleAfterPhysicsStep`. An entity's `_Process` only changes what is drawn or heard.
