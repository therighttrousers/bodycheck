# 034: A controller is built with its entity

## Context

Inciting event (2026-10-29): issue #108. `005 §3.3` kept controllers apart from entities and passed the entity to `Apply`, so that an entity's controller could be swapped, or a controller used with another entity. But the player's and robots' controllers keep state that belongs to one body: timers, the phase being aimed, the laser's timeline. A controller can't control a second entity without carrying the first one's state over. Neil: might as well make the relationship official.

A side effect of passing the entity to `Apply` is the type parameter of `ICharacterController<TEntity>`. Each controlled kind of body declares its own `Controller`, and `PlayerEntity` and `RobotEntity` hide `WalkerEntity.Controller` to get one of their own type (`032 §3.3`), each overriding `RequestsPause`, `Control` and `ControlPaused` to call it.

Goals:
- A controller is given the entity it controls when it is built, and controls only that one.
- The wrong kind of controller for a body still doesn't compile.

Anti-goals: what controllers write on their entities for drawing, such as `Facing` and `Laser`. That goes the other way, and is unchanged.

## 1. The controller and its entity

- [x] 1.1 A controller's constructor takes its entity, as the kind of body it needs: `PlayerController` a `PlayerEntity`, `RobotController` a `RobotEntity`, `PlatformController` any `Entity`. Decision (Neil): yes. So the entity is built first, then its controller. The wrong kind of body for a controller doesn't compile, at the constructor instead of at `Controller`.
- [x] 1.2 `Apply` and `ApplyPaused` no longer take the entity, so `ICharacterController` has no type parameter. Decision (Neil): yes. Options (Claude):
  - (a) Keep `Apply(TEntity entity, …)` and add the constructor's parameter. The controller then has its entity twice, and nothing says they are the same.
  - (b) Drop the entity from `Apply` and the type parameter with it. Chosen: with no type parameter, every entity's controller has one type, so it can live in one place (§1.3).
- [x] 1.3 `Controller` lives on `Entity`, as do `RequestsPause`, `Control` and `ControlPaused`, which call it. Decision (Neil): yes. The hiding of `WalkerEntity.Controller` in `PlayerEntity` and `RobotEntity`, and their overrides, go. `WalkerEntity` overrides `Control` only to update its animation after its controller runs. This supersedes the part of `005 §3.8` that gives the base `Entity` no controller, and `032 §3.3`'s hiding.
  - `SimpleControlledEntity` held only a controller, so it goes, and a platform is a plain `Entity` with a controller. A block is one without.
- [x] 1.4 What stops a controller built for one entity from being given to another (Claude). Decision (Neil): (a).
  - (a) `ICharacterController` has the entity it controls, as an `Entity`, and the `Controller` setter throws if that isn't the entity it is set on.
  - (b) The entity's constructor takes a function that builds its controller from it. Then the mistake can't be made, but every kind of body's constructor grows a parameter, and the entity, not what builds the room, would call the function that gives it its controller.
- [x] 1.5 The entities subsystem doesn't depend on the controllers (Neil). The interface stays in `Entities`, and names only `Entity`. Nothing in `Entities` names a controller of any kind, as before.
- [x] 1.6 Decision (Neil, after building §1.1 to §1.5): the interface is `IController`, which is shorter to type than `ICharacterController`, and it has a type parameter again, `out TEntity`, the kind of body its `Entity` is. So code that holds a particular controller gets its entity as the kind of body it is, such as a `PlayerController`'s as a `PlayerEntity`. This supersedes §1.2's dropping of the type parameter, but not of the entity from `Apply`. The parameter is covariant, so `Entity.Controller`, an `IController<Entity>`, still takes a controller of any kind of body, and `Controller` still lives once on `Entity` (§1.3).
