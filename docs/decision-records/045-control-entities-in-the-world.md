# 045: Control entities in the world

## Context

Inciting event (2026-11-04): starting issue #159, in which the level reader learns the LDtk layout of `043`: the `Control` and `Entities` layers, and the control entities `Trigger`, `Sequence`, `Fanout`, `Dialogue`, `Script` and `Respawn`, whose routes name their targets by LDtk's `Iid`. The question is how a `World` holds them, so that routes can be checked once, when the world is built, and run without lookups.

What was there:
- `World.EntityConfigs`, one list of every configuration, room by room, and `World.Respawns`, the respawns among them.
- `WorldNode` builds an entity for each configuration but a respawn's. `WorldNode.EntitiesOf` pairs its i-th entity with the i-th configuration, so a respawn listed before a body shifts every body after it by one. A level lists its bodies before its control entities, so a world of one level is safe, but in a world of several, one level's respawn comes before the next level's bodies. Nothing has hit it yet, since tests and probes use text rooms.
- Only a respawn has an `Id`.
- Routes in LDtk: a `Trigger` has a `Target` and an `Action`; a `Sequence` and a `Fanout` have two arrays, `Targets` and `Actions`, which LDtk can't pair (`043 §1.5`).
- In LDtk, each entity has a tag, `Character`, `Prop` or `Control`, and each layer takes only some tags, so the editor keeps control entities out of the `Entities` layer and bodies out of `Control`.

Goals:
- Routing at run time is a direct call, with nothing looked up.
- Every check that doesn't depend on the source of the data is an invariant of `World`, whatever its source.
- `043`'s load-time checks: each target's kind responds to its action (`§1.3`, `§1.4`), the arrays are the same length (`§1.5`), and there are no cycles (`§1.6`).

Closed on 2026-11-04. Outcomes are in the architecture docs of Core, `WorldData`, `Levels` and `Simulation`.

## 1. Identity

- [x] 1.1 Every configuration has an `Id`, not only a respawn, since a route's target may be a body, such as the door (Claude, agreed by Neil). From LDtk it is the entity's `Iid`. In a text room it is the room's name and the entity's place, as a respawn's is now.

## 2. How the world holds them

- [x] 2.1 Two lists in place of `EntityConfigs` and `Respawns` (Claude, agreed by Neil): `World.Bodies`, the configurations that build an entity, in the order they are stepped, and `World.Controls`, the rest; and a lookup from id to configuration. `EntitiesOf` then lines up with `Bodies` by construction. What uses `Respawns` takes `Controls.OfType<EntityConfig.Respawn>()`, so the world keeps no second source of them.
- [x] 2.2 `Controls` is a list, in the order of the rooms and then of each room's own, so that whatever goes through the control entities each tick, such as polling triggers for touches (`043 §4.2`), does so in a known order (Neil asked; Claude, agreed by Neil). Neil: a good chance that we test containment ourselves rather than rely on `body_entered`, and a deterministic order is a reason to (`043 §4.1`, option (b)). If the engine finds the overlaps, the order in which triggers fire is still ours, as long as each trigger is asked in turn; the order of the bodies within one trigger's overlaps is the engine's, which doesn't matter while only the player sets one off (`043 §4.3`).
- [x] 2.3 Whether the reader checks that an entity is in the layer for its tag, bodies in `Entities` and control entities in `Control`, which the editor enforces by tag (Claude proposed). Decision (Neil): no. Layers are visual aids, not constraints: the reader reads the entities of both layers alike, and an entity's kind, not its layer, says whether it has a body.

## 3. Routes

- [x] 3.1 A route is a list of steps, each a pair of a target and an action (Claude, agreed by Neil): `Step(TargetId, Action)`. The reader checks that `Targets` and `Actions` are the same length and pairs them. A `Trigger` has one step; a `Sequence` and a `Fanout` have a list of them.
- [x] 3.2 A step names its target by id, not by a reference to its configuration (Claude, agreed by Neil). `MovedBy` copies every configuration into the world's coordinates, so a reference would be to the copy in the room's, and ids keep the data plain.
- [x] 3.3 `Simulation` resolves each step's id once, when it builds the world, to whatever runs the action: a body's controller or a control entity's runner. Running a route is then a direct call (Claude, agreed by Neil).

## 4. Invariants

- [x] 4.1 `World`'s constructor checks the routes, as it already checks player starts and overlaps (Claude, agreed by Neil): ids are unique, every target is in the world, its kind responds to the action, and there are no cycles. The reader wraps the `ArgumentException` with the project's path, as it does now. A target in another level of the same world is allowed, since a world is one space; one in another world isn't found, so it is rejected.
- [x] 4.2 Other checks to move from the reader into `World`, or into `Room`, which checks what is true of one room (Neil asked; Claude). The checks fall into four sorts, with Neil's decisions:
  - Of LDtk's format, which stay in the reader: the JSON version, multi-worlds, sizes and places in whole tiles of 16 pixels, depth 0, layers that cover their levels, the length of the IntGrid, positions and pivots of two numbers.
  - A world with no levels, which `World` also rejects, as having no rooms. The reader's check goes: LDtk doesn't allow a world with no levels (Neil tried it).
  - Of a configuration, whatever its source: a platform's width is positive, its height is 1 tile, and its two ends are different cells. They move to `Room`, so that text rooms' platforms are checked too.
  - Some that nothing checked: every entity's box has a positive size, which `Room` checks. An entity's box inside its room is not checked: LDtk lets some kinds be outside their level, and the control entities but `Respawn` are marked so, to be placed out of the way.
- [x] 4.3 Where the pieces go (Claude, agreed by Neil):
  - The table of the actions each kind responds to (`043 §1.4`) is in `WorldData`, beside `EntityKind`, as a switch over every kind, so that a new kind without an entry is a warning. A test checks that each kind's list is either `Default` alone or a list without it.
  - `TriggerAction` is in the root: configurations in `WorldData` hold it, and the interface for running an action, in `Entities` (`043 §3.2`), takes it, and neither uses the other. It keeps LDtk's name, since the reader reads it by name.

## 5. Not in this issue

- 5.1 That a `Dialogue`'s file holds its title (`044 §5.2`) waits for the dialogue backend. A `Script` is read and rejected as not yet supported, since the slice doesn't use one.
