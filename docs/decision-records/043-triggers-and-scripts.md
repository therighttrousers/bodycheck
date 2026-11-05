# 043: Triggers, actions and scripts

## Context

Inciting event (2026-11-04): breaking down the first slice, "The call and the lock-in" (issue #142). It needs the first parts of issue #139: trigger areas laid out in LDtk, and story beats. The call plays in the level `Outside`, from fire-once triggers that start the Voice's dialogue, each clearer than the last. In `Entrance`, a trigger the player can't avoid has the Voice speak, then slams the door shut behind the player.

Goals:
- Story beats are driven by ordinary gameplay where possible, so the player keeps a sense of agency, which the game is about (Neil).
- What happens runs on game time, inside the tick, in a known order, as controllers do: nothing depends on when frames are drawn.
- A level designer wires a beat in LDtk, from a few kinds of control entity, each read and validated like the rest of the level data. C# is for what wiring can't say.
- The control entities are a small language, so language design applies: uniformity and compositionality. Each kind does one thing well, and they combine (Neil).
- Room for what later slices need: the interact input, scripted input for the takeover, inventory, and robots that wake. None of them is designed here.

Anti-goals:
- A scripted camera. Overriding what the player chooses to look at needs a good reason (Neil); the door's trigger is placed so that the door is in view when it shuts (§6.4).
- Saving state. That is the game state's job (issue #2); until then, state lives in memory, and rebuilding the world forgets it.

## 1. The language

- [x] 1.1 Its pieces (Neil). Every entity a route can reach responds to actions, from the LDtk enum `TriggerAction`: `ToggleOnOff`, `On`, `Off`, `ToggleOpenClose`, `Open`, `Close` and `Default`. A kind that does one thing responds to `Default`. Running an action finishes at some point, and whatever ran it can wait for that. The control entities, in the `Control` layer:
  - **`Trigger`**: on being touched, or on `Default`, runs its `Action` on its `Target`, and finishes when that does. It can be `Once`.
  - **`Sequence`**: on `Default`, runs each of its `Actions` on the matching one of its `Targets`, in turn, each when the one before has finished. It finishes when the last does. It can be `Once`.
  - **`Fanout`**: on `Default`, runs each of its `Actions` on the matching one of its `Targets`, all at once. It finishes when all have. It can be `Once`.
  - **`Dialogue`**: on `Default`, presents the title `DialogueTitle` of the dialogue file `DialogueFile`, garbled by `Garble` (`044 §3`). It finishes when the dialogue does.
  - **`Script`**: on `Default`, runs a temporary instance of the C# class it names (§5).
  - **`Respawn`**: on `Default`, becomes the player's respawn. See §4.4 for respawns now.
  - Entities with bodies respond too, through their controllers (§2.1): the `VerticalDoor` to `Close`.
- [x] 1.2 With these, a beat is mostly routing: the first slice needs no script. `Entrance` wires a `Trigger` to a `Sequence` of the Voice's `Dialogue` and then the door's `Close` (Neil).
- [x] 1.3 Routes are checked when the world loads. LDtk lets `Target` be any entity, so the reader checks that each target's kind responds to the action, and rejects the level if not. Each kind lists the actions it responds to (Claude, agreed by Neil).
- [~] 1.4 `Default` is accepted only by kinds that respond to one action, so that it never stands for one of several by convention (Claude). A `Trigger` is one: being touched is not an action.
- [~] 1.5 `Sequence` and `Fanout` hold their steps as two arrays, `Targets` and `Actions`, which LDtk can't pair. The reader checks that they are the same length. The alternative, for the record: steps that are `Trigger`s, each binding one target to one action, so that `Sequence` and `Fanout` take only `Targets`, all run with `Default`. More uniform, but a beat of three steps takes three more entities.
- [~] 1.6 No cycles (Claude). A route that leads back to where it started could run forever within a tick, so the reader rejects a level whose routes, `Trigger` to target, `Sequence` and `Fanout` to each target, have a cycle. Repetition, when something needs it, becomes a kind of its own.
- [ ] 1.7 An action asked of an entity that is still running one. A `Trigger` that isn't `Once` may be touched again while its target is still running, and two routes may reach one entity. Options: ignore it until the first finishes, and finish when it does; run it again alongside; or restart. Claude: ignoring it, for anything that runs others (`Trigger`, `Sequence`, `Fanout`, `Script`), since none of them is safe to run twice at once; and for a controller, its own choice, such as a door already closing, which finishes when it closes.
- [x] 1.8 `Once` is a field of each kind that runs others. One that has run doesn't run again. Saving it needs the game state; until then it is in memory.
- [x] 1.9 State that an action changes, such as what a toggle toggles, belongs to the controller that responds to it, which also makes sure it is saved (Neil). The router holds none, so persisted state isn't spread between them.

## 2. Running actions

- [x] 2.1 Controllers respond to actions, not entities, since behaviour is a controller's (Neil, Claude). A control entity has no body and no controller, so `Simulation` builds a small runner for each from its configuration. Both have one interface for running an action.
- [x] 2.2 Responding to an action records it, and the controller acts on it in its next `Apply`, so the tick's order holds. The `Dialogue` runner asks the game's dialogue interface to start, and `GameModeManager` changes mode at its own point in the tick (Claude, agreed by Neil).
- [x] 2.3 Running an action is awaitable: whatever ran it can wait until it finishes, so that a `Sequence` can run its next step (Neil). What finishing is for each: a door's `Close` when it is frozen shut; a `Dialogue` when the dialogue ends; a `Respawn` at once; a `Script` when its method returns; a `Trigger`, `Sequence` or `Fanout` as §1.1 says.
- [~] 2.4 What is awaited. Options:
  - (a) `Task`, with `async` and `await`. Rejected: Godot's C# sets a `SynchronizationContext` on the main thread, which runs continuations when Godot processes them, not at a point in our tick. To be checked how and when it does.
  - (b) An awaitable of our own, whose continuations the world runs at a fixed point in its tick. `async` methods can return it through a method builder of our own. Scripts use it too (§5.1).
  - (c) Godot's `ToSignal`. Rejected: its continuations run whenever Godot emits the signal; signals go on while the tree is paused, so an action could move on behind the menu; Godot's timers have their own rules for pausing and time scale; and an exception in a continuation is logged by Godot rather than failing the tick, so the tick's deferred actions would run against a broken state. What we give up is awaiting any Godot signal with no code of ours. The game's side of the dialogue interface turns Dialogue Manager's signal into something our awaitable completes.
  - Leaning (Claude): (b).
- [ ] 2.5 Where in the tick awaited actions resume and touched triggers fire. Options: before any entity steps, so that what they start acts in the same tick as the controllers; or after the entity steps and their deferred actions, beside the respawns' update, so that it starts with the next tick's controllers. Claude: after, which leaves controllers alone in their part of the tick; the delay of a tick can't be felt.
- [ ] 2.6 Whether a chain of actions that finish at once, such as a `Sequence` of respawns, runs to its end in one tick or takes a tick a step. Claude: in one tick, so that wiring adds no delays of its own; §1.6 keeps it finite.
- [~] 2.7 Only played ticks run routing. A dialogue finishes while the game is paused for it, and what waits for it resumes at the next played tick. Rebuilding the world drops everything in progress with it. An exception in an action fails the tick, as one in a controller does.

## 3. Where it lives in Core

- [x] 3.1 A new subsystem, `Scripts`: the routing, the runners of the control entities, and the C# scripts (Neil).
- [ ] 3.2 The dependencies (Claude). `Simulation` uses `Scripts`, to build the runners and resume them in its tick. Controllers respond to actions, so the interface for running one can't be in `Scripts` if `Scripts` uses `Controllers`. Proposal: the interface is in `Entities`, beside `IController`, as a default interface member that responds to nothing, so that only controllers that respond have it; `Scripts` uses `Entities` only, and a C# script acts on an entity by running an action on it through the routing, not by reaching its controller. No cycle.
- [x] 3.3 The dialogue backend is an addon in the game, so the `Dialogue` runner reaches it through an interface Core defines and the game implements, as `IAssetLibrary` does for assets (Neil).

## 4. Touch

- [~] 4.1 What a `Trigger` is in the engine. Options:
  - (a) An `Area3D` subclass on the `Trigger` physics layer, which already exists (Neil). The engine finds the overlaps, any kind of body can be detected, and the collision debug view shows it.
  - (b) A box test in code, as respawns are found now: each tick, `WorldNode` measures the gap between the player's box and each respawn's. Its timing is plain, but it detects only what the code measures, and it is a second way of doing what the engine does.
  - (c) A shape query each tick, with the space's `IntersectShape`. The engine's broadphase without a node, but a query per trigger per tick.
  - Leaning (Neil, Claude): (a).
- [~] 4.2 How touches reach the tick. Godot emits `body_entered` when the engine reports its step, outside our tick's order. Options:
  - (a) Poll (Claude): at the point of §2.5, each trigger reads `GetOverlappingBodies`, as of the last step, and compares it with the bodies it held at the last tick, which it keeps. A body that is in it now and wasn't then has touched it.
  - (b) Handle `body_entered`, and queue the touch for that point (Neil).
  - Both need what a trigger holds, so neither is hard (Neil); (a) has no handlers whose order matters. To be checked: that Jolt's areas update their overlaps in the step, and the cost of polling a few dozen triggers.
  - Tunnelling (Neil asked): an area's overlaps come from where bodies are at the end of a step, with no continuous detection, so a body that crosses a thin area within one step is missed by both. A trigger must be as thick as anything that must set it off moves in a tick: at 60 ticks a second, a body at 30 tiles a second moves half a tile. To be checked with a test that throws a body through a thin trigger.
- [ ] 4.3 What can set a trigger off. Options: the player only, found among the overlapping bodies; a physics layer of the player's own, so that the trigger's mask filters, as [the root's doc](../../src/BodyCheck.Core/architecture.md#physics-layers) prefers; or a mask per trigger in LDtk, for later uses such as a block on a pressure plate. Claude: the player only is enough for the first slice.
- [ ] 4.4 Respawns made current by triggers, not by distance (Neil), so that designers don't have to shape a level around the respawns' reach. A respawn is the target of the triggers that make it current, which §1.1 already allows. For the migration, a respawn that no trigger targets could make a default trigger around itself, shown with the control entities on F2. Not in the first slice: respawns keep their reach until then.

## 5. Scripts

- [~] 5.1 A `Script` runs a temporary instance of a C# class, made for each run and dropped when it finishes (Neil). Its method is `async` and returns the awaitable of §2.4, so that it can wait for actions, for time on the game's clock, and for dialogue. Earlier options, now settled by the routing's need to await: a class stepped each tick, as a controller is, which makes a sequence a hand-written state machine; and C# iterators, which can't return values or `await` other methods.
- [ ] 5.2 How a `Script` names its class. It holds a fully qualified class name, which the reader checks names a class that is a script. The field is optional in LDtk now; Claude: required, since a `Script` with none does nothing. Renaming a class breaks the levels that name it; the load-time check, and a test that loads every world, make that a failure in CI rather than in play.
- [ ] 5.3 What a script can reach. Options: an interface given to it, with what scripts use, such as running an action on an entity, waiting on the game's clock, and `IDefer`; or the `WorldNode` itself. Claude: the interface, so that a script, like a controller, can't reach past what it is given, and tests can give it a fake.
- [x] 5.4 Neither a script nor anything awaited can be saved partway: one in progress when the game saves restarts from the game state, so scripts and chains stay short.

## 6. The door

- [x] 6.1 The door is a `VerticalDoor`, a plain `Entity`, drawn as the debug box for now, with a new `DoorController`. It has no animation yet, and cues don't need a subclass (Neil).
- [x] 6.2 It responds only to `Close`: it slams down and then beeps, as two cues, and finishes when it is frozen shut.
- [x] 6.3 What holds it shut. Options:
  - (a) A `StaticBody3D`, or an `AnimatableBody3D` to move it. Never moved by anything, cheapest in the engine, and touched as tiles are. But it isn't an `Entity`: no controller, no cues, no features, not stepped by the world, and a kind of body with its own path through every part of the code. Its slam would be animated, not pushed by forces, against [the high-level design](../designs/high-level-design.md#simple-newtonian-movement-and-combat).
  - (b) An `Entity`, frozen once shut. The same entity, controller and cues throughout, and unfreezing it later would let it open. Changing a body's freeze changes its motion type in the engine, so it is done through `IDefer`, and the bodies anchored to it, such as a player standing on it, may need their motors anchored again. To be checked with Jolt.
  - (c) An `Entity` held by its controller, very heavy, with a strong drive and no freezing. The purest Newtonian answer, but anything strong enough to push it can, and holding still against a push may jitter.
  - (d) (Neil) An `IEntity` that a `StaticEntity` and a `RigidEntity` both implement, so that a static door could have a controller. Rejected: implementing it twice is tedious and fragile, and the slam would still be animated.
  - Decision (Neil): (b), simplified. The controller slams the door with an impulse, and freezes it once it has settled where a ray cast down from it found the floor. It is locked on the X axis, so that nothing pushes it sideways. It must not be a target for the phase, which now takes any entity in reach. Both need new `BodyTuning` properties, beside `CanBeCarried`. Whether it starts frozen while open is for the implementation.
- [x] 6.4 No scripted camera (Neil). The trigger that shuts the door is one the player can't avoid, past the door and close enough to it that the door is in view when it slams, and far enough that the player is never under it.
- [ ] 6.5 Stretch goal: the door explains why it can't open, from a trigger in front of it, with no interact input. A `Dialogue` with the door as its speaker, when `Dialogue` gets a `Target` for one (Neil).

## 7. Related, not decided here

- 7.1 Dialogue: its backend, its mode in the game, its files, its text box and the garble are in `044`.
- 7.2 Saving what has run, and each controller's state, waits for the game state (issue #2).
- 7.3 The interact input, scripted input and inventory (issue #139) wait for the slices that need them.
