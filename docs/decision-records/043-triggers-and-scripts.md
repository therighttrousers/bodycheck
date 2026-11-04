# 043: Triggers and scripts

## Context

Inciting event (2026-11-04): breaking down the first slice, "The call and the lock-in" (issue #142). It needs the first parts of issue #139: trigger areas laid out in LDtk, and story beats written as small C# scripts. The call plays in a level outside the facility's entrance, from a few fire-once triggers that start the Voice's dialogue, each clearer than the last. A trigger the player can't avoid, inside the entrance, slams the door shut behind it.

Goals:
- Story beats are driven by ordinary gameplay where possible, so the player keeps a sense of agency, which the game is about (Neil).
- Scripts run on game time, inside the tick, in a known order, as controllers do: nothing a script does depends on when frames are drawn.
- A level designer lays out triggers in LDtk and picks what each does from a fixed set of names, read and validated like the rest of the level data.
- Room for what later slices need: the interact input, scripted input for the takeover, inventory, and robots that wake. None of them is designed here.

Anti-goals:
- A scripted camera. Overriding what the player chooses to look at needs a good reason (Neil); the door's trigger is placed so that the door is in view when it shuts (§4.4).
- Saving what has fired. That is the game state's job (issue #2); until then it lives in memory, and rebuilding the world forgets it.

## 1. Triggers

- [~] 1.1 What a trigger is in the engine. Options:
  - (a) An `Area3D` subclass on the `Trigger` physics layer, which already exists (Neil). The engine finds the overlaps, any kind of body can be detected, and the collision debug view shows it.
  - (b) A box test in code, as respawns are found now: each tick, `WorldNode` measures the gap between the player's box and each respawn's (`UpdateCurrentRespawn`). No engine node, and its timing is plain, but it detects only what the code measures, and it is a second way of doing what the engine does.
  - (c) A shape query each tick, with the space's `IntersectShape`, from `WorldNode`. The engine's broadphase without a node, but one query per trigger per tick.
  - Leaning (Neil, Claude): (a). If it works out, respawns could become triggers too, so that there is one way of noticing that the player is somewhere (§1.6).
- [~] 1.2 How a trigger's overlaps reach the tick. Godot emits `body_entered` and `body_exited` when the engine reports its step, outside our tick's order, and in no stated order relative to `_IntegrateForces`. Options:
  - (a) Handle the signals: each records that a body entered, and the world acts on it at a fixed point in the tick.
  - (b) Poll: at a fixed point in the tick, the world reads each trigger's `GetOverlappingBodies`, which is as of the last step, as controllers read the world (see [the conventions](../designs/architecture.md#conventions)).
  - Leaning (Claude): (b). No handlers whose order matters, and "entered" is the difference from the last tick's overlaps, which the trigger keeps. To be checked: that Jolt's areas update their overlaps in the step, so that polling sees the last step's, and the cost of polling a few dozen triggers.
  - Tunnelling (Neil asked): an area's overlaps are found from where bodies are at the end of a step, with no continuous detection, so a body that crosses a thin area within one step is missed by both the signals and polling. Polling loses nothing against the signals (Neil). A trigger is then as thick as anything that must set it off moves in a tick: at 60 ticks a second, a body at 30 tiles a second moves half a tile. To be checked with a test that throws a body through a thin trigger.
- [ ] 1.3 Where in the tick triggers are read. Options:
  - (a) Before any entity steps, so that a script started by a trigger acts in the same tick as the controllers.
  - (b) After the entity steps and their deferred actions, beside the respawns' update, so that a script's effects start with the next tick's controllers.
  - Claude: (b) leaves controllers alone in their part of the tick, and a script, like a controller, acts through forces and deferred actions that the engine's step then applies. The one-tick delay can't be felt.
- [ ] 1.4 What sets a trigger off. Options:
  - (a) The player only, found by comparing the overlapping bodies with the world's player.
  - (b) A physics layer of the player's own, so that the trigger's mask does the filtering, as [the root's doc](../../src/BodyCheck.Core/architecture.md#physics-layers) prefers.
  - (c) A mask in LDtk, per trigger, for later uses such as a block on a pressure plate.
  - Claude: (a) is enough for the first slice. (b) costs a layer and a change to every mask that includes characters.
- [x] 1.5 Firing once. Each trigger has a field, `Once`, in LDtk; one that has fired doesn't fire again until the world is rebuilt. Decision (Neil): fire-once triggers start the call's dialogue. What a trigger that isn't once does when the player stays in it, fire on every entry or every tick, waits for a use.
- [ ] 1.6 Respawns set by triggers, not by distance (Neil), so that designers don't have to shape a level around the respawns' rule of reach. A respawn would be paired with the triggers that make it current when touched. For the migration, a respawn with no trigger could make a default one around itself, shown with the control entities on F2. Not in the first slice.

## 2. Triggers in the level data

- [~] 2.1 How a trigger names what it does. Options:
  - (a) A field, `Script`, an LDtk enum of script names. The game has one C# script for each name, and a test checks that the enum and the scripts agree, as one does for the physics layers.
  - (b) Two kinds of trigger: one that names a script, and one that names a dialogue node and only plays it.
  - (c) A script name and arguments, such as the dialogue node to play and the garble, so that one script serves every dialogue trigger.
  - Claude: (a) with (c)'s arguments as typed LDtk fields read with the trigger, such as a `Dialogue` string checked against the dialogue files at load. The call needs a few dialogue triggers that differ only by node and garble, and one script for them is better than one each.
- [~] 2.2 How a trigger refers to an entity, such as the door it shuts. Options:
  - (a) An LDtk entity reference field, which LDtk checks when editing, read as the entity's `Iid`, as respawns' are now.
  - (b) A name the script knows.
  - Leaning (Claude): (a). The reader checks that the reference is to an entity of the kind the script needs.
- [x] 2.3 The pieces follow the existing path: `Levels` reads a trigger from the `ControlEntities` layer into an `EntityConfig.Trigger`, in `WorldData`, and `Simulation` builds its `Area3D`. Entity fields are now read for triggers and platforms only.

## 3. Scripts

- [ ] 3.1 The shape of a script. A beat is a sequence: play this dialogue and wait for it to end, wait half a second, shut the door. Options:
  - (a) A class with a `Step` each tick, which keeps its own state, as a controller does. Uniform with controllers, but a sequence becomes a hand-written state machine.
  - (b) A C# iterator: the script `yield return`s what it waits for, such as `Wait(0.5)` or a dialogue, and the world resumes it each tick until that is done, as Unity's coroutines do. Plain, deterministic, and a script reads as its sequence. No `try`/`finally` across a `yield` in a `catch`, and no return values from what it waits for.
  - (c) `async` and `await` with our own awaitables, resumed by the world each tick, with no threads and no `SynchronizationContext`. Reads like (b), with return values (which dialogue choice was picked) and `try`/`finally`, at the cost of an awaitable to build and its rules to learn.
  - (d) Godot's `ToSignal` and its timers. Rejected: they resume on Godot's timing, outside our tick's order.
  - Claude: (b) or (c). (c) is the one that handles a dialogue choice cleanly, but the first slice's choices are fake (§3.4).
  - What (d) would give that the others don't (Neil asked; Claude): awaiting any Godot signal with no code of ours, such as a dialogue ending, a tween or an animation finishing, a sound ending, or a `SceneTreeTimer`. Dialogue Manager reports through signals, so the game's side of the dialogue interface turns its signal into something our awaitable completes, which our tick then resumes. What it would cost: the continuation runs when Godot emits the signal, at no stated point in our tick; signals go on while the tree is paused, so a script could move on behind the menu; Godot's timers have their own rules for pausing and time scale; and an exception in a continuation is logged by Godot rather than failing the tick, so its tick's deferred actions would run against a broken state. Neither (d) nor ours can save a script partway: a script in progress when the game saves has to restart from the game state, so scripts stay short.
- [ ] 3.2 What a script can reach. Options:
  - (a) An interface given to the script, with exactly what scripts use: the entity behind an LDtk reference, starting a dialogue and knowing when it ends, the game's clock, and `IDefer`.
  - (b) The `WorldNode` itself.
  - Claude: (a), so that a script, like a controller, can't reach past what it is given, and tests can give it a fake.
- [x] 3.3 Where scripts live in Core, and the dependencies. `WorldNode` runs them in its tick, and they act on entities through their controllers, such as telling a door's controller to shut. A proposal (Claude): a new subsystem, `Scripts`, with the scripts and the interface of 3.2. `Simulation` uses `Scripts`, to run them and implement the interface; `Scripts` uses `Controllers` and `Entities`. No cycle. The dialogue backend is an addon in the game, so starting a dialogue goes through an interface Core defines and the game implements, as `IAssetLibrary` does for assets. Decision (Neil): as proposed.
- [~] 3.4 Fake choices. The beginning has to railroad the player, so every choice the Voice offers in the call leads to the same place, and scripts don't need to read which was picked yet (Neil).

## 4. The door

- [x] 4.1 The door is a plain `Entity`, drawn as the debug box for now, with a new `DoorController`. It has no animation yet, and cues don't need a subclass (Neil).
- [x] 4.2 It knows only how to shut, not how to open. A script tells its controller to shut; it slams down and then beeps, as two cues.
- [x] 4.3 What holds it shut. Options:
  - (a) A `StaticBody3D`, or an `AnimatableBody3D` to move it. Never moved by anything, cheapest in the engine, and touched as tiles are. But it isn't an `Entity`: no controller, no cues, no features, not stepped by the world, and a kind of body with its own path through every part of the code, since C# has neither multiple inheritance nor union types to share `Entity`'s parts with it. Its slam would be animated, not pushed by forces, against [the high-level design](../designs/high-level-design.md#simple-newtonian-movement-and-combat).
  - (b) An `Entity`, frozen. Held up while open, frozen as a kinematic body; on the script's word, unfrozen to slam down under its controller's drive; frozen as a static body once it lands. The same entity, controller and cues throughout, and unfreezing it later would let it open. Changing a body's freeze changes its motion type in the engine, so it is done after the entity steps, through `IDefer`, and the bodies anchored to it, such as a player standing on it, may need their motors anchored again. To be checked with Jolt.
  - (c) An `Entity` held by its controller, very heavy, with a strong drive and no freezing. The purest Newtonian answer, but anything strong enough to push it can, and holding it still against a push may jitter.
  - (d) (Neil) An `IEntity` that a `StaticEntity` and a `RigidEntity` both implement, so that a static door could have a controller. Rejected: implementing it twice is tedious and fragile, and the slam would still be animated.
  - Decision (Neil): (b), simplified. The controller slams the door with an impulse, and freezes it once it has settled where a ray cast down from it found the floor. It is locked on the X axis, so that nothing pushes it sideways. It must not be a target for the phase, which now takes any entity in reach. Both need new `BodyTuning` properties, beside `CanBeCarried`. Whether it starts frozen while open is for the implementation.
- [x] 4.4 No scripted camera (Neil). The trigger that shuts the door is one the player can't avoid, past the door and close enough to it that the door is in view when it slams, and far enough that the player is never under it.
- [ ] 4.5 Stretch goal: the door explains why it can't open, from a proximity trigger in front of it, with no interact input. The same trigger as the call's, playing the door's dialogue.

## 5. Related, not decided here

- 5.1 Dialogue: its backend, its mode in the game, its text box and the garble are in `044`.
- 5.2 Saving which triggers have fired, and the door's state, waits for the game state (issue #2).
- 5.3 The interact input, scripted input and inventory (issue #139) wait for the slices that need them.
