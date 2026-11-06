# 049: Doors and their stops

## Context

Inciting event (2026-11-05): issue #163, the door that slams shut. Under `043 §6`, a `VerticalDoor` is an `Entity` with a `DoorController` that responds to `Close` only: it slams the door with an impulse, and freezes it once it has settled where a ray cast down from it found the floor. Building it, Neil found that a door slammed shut much faster than expected even with no impulse: the engine was pushing it out of the tiles it overlapped. A door hides in the tiles above its doorway when it is open, and with the default collision mask it collides with them; with a mask that leaves out tiles, nothing stops it.

Goals:
- A door that opens into tiles and closes onto the floor, moved by forces, as `043 §6.3` chose.
- Levels in which a door can't be left without what stops it.

Closed on 2026-11-05. Outcomes are in the architecture docs of Core, `Entities`, `Controllers`, `WorldData`, `Levels`, `Simulation` and `Tasks`.

## 1. Stops

- [x] 1.1 What a door stops against (Neil). A door's mask leaves out tiles, so that it can pass into them, and takes in a new physics layer, `DoorStop`, which nothing else collides with. Each door has two required fields in LDtk, `OpenStop` and `CloseStop`, each a cell, and the world builds a collider on `DoorStop` in each, as one static body for every door's stops, beside the tiles' (Neil, built by Claude). This supersedes `043 §6.3`'s ray cast down to the floor.
- [x] 1.2 Rejected (Neil): stops as special tiles, or as special entities, that the level designer places. Both make it easy to leave out the stops; required fields on the door don't.
- [x] 1.3 Also considered (Claude): a joint from the world to the door, with a limit on Y between the two stops and X locked, which needs no layer and no colliders. Not taken: a limit reports no contact, so whether the door has reached a stop becomes a check of its position, and Godot's joints have quirks of their own. To revisit if the stops misbehave.
- [x] 1.4 The room checks a door's stops (Claude, agreed by Neil): both are in its columns, its open stop above it and its close stop below it, and it doesn't touch both, so that it can move. A door may start anywhere between them (Neil): it starts closed if it touches its close stop, and open otherwise, so that it closes when asked. Neil first had it start open or closed by the nearer stop, and Claude proposed that it start touching exactly one; Neil wanted the door of `Entrance` to start a little low and open higher.

## 2. Moving

- [x] 2.1 A door responds to `Open`, `Close` and `ToggleOpenClose`, not `Close` only (`043 §6.2`). It moves by thrust, a force of a fraction of its weight, `DoorThrustFrac`, up to open and down, with gravity, to close, rather than by one impulse, so that it can open as well as close. It freezes when it stops, and is frozen while it isn't moving, so that nothing but its own action moves it (Neil).
- [x] 2.2 A door that meets something, such as a block, stops there, and counts as having closed or opened (Neil). What a blocked door should do may change; for now this is the simplest rule that finishes. A door also stops after `DoorMaxOpenOrCloseTime`, so that a run always finishes.
- [x] 2.3 Its actions share one `ReentrantTask`, with `Queue`, so that an action asked while another runs follows it, and a toggle decides which way to go when it starts, from where the door is then (Neil).
- [x] 2.4 How the thrust's stall is timed: `ITick.Dt`, the length of the tick being run, so that an async action can count game time without being given it (Neil).
- [x] 2.5 Bodies standing on a door while it unfreezes and freezes again keep working: a test has the player ride a door down, then run off it. Jolt rebuilds nothing that the player's grip needs.

## 3. Re-entry

- [x] 3.1 Neil's first toggle asked its own `ReentrantTask` to `Open` or `Close`, from inside the run the toggle had started. With `Queue`, that run waited for the toggle that waited for it: a deadlock, but only when another run was in progress, so rarely. Options (Claude): (a) a request made while a run is being started joins that run; (b) such a request throws, under every policy, whether or not another run is in progress; (c) the method builder tracks the current run, so that a request from inside a run is caught even after an `await`, at the cost of ambient state in `Tasks`, which `046 §1.3` avoided; (d) a convention only. Decision (Neil): (b), so that the mistake shows every time it is made. Work that shares a `ReentrantTask` calls the work itself, not the requests that go through it. A request from inside a run, after an `await`, is not caught, and with `Queue` still deadlocks, every time.

## 4. Not decided here

- [ ] 4.1 What a blocked door should do in the end, such as open again, or keep pushing (item 2.2).
