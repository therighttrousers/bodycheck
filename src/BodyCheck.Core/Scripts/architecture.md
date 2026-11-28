# Scripts

Relevant decision records:
- [043: Triggers, actions and scripts](../../../docs/decision-records/043-triggers-and-scripts.md)
- [044: Dialogue](../../../docs/decision-records/044-dialogue.md)
- [045: Control entities in the world](../../../docs/decision-records/045-control-entities-in-the-world.md)
- [046: Actions as async methods](../../../docs/decision-records/046-async-actions.md)
- [047: `Once`, and routing actions](../../../docs/decision-records/047-once-and-routing.md)
- [050: The cutscene mode](../../../docs/decision-records/050-cutscene-mode.md)
- [051: The scripted run at the spawn](../../../docs/decision-records/051-scripted-run-at-the-spawn.md)
- [052: The talking door](../../../docs/decision-records/052-talking-door.md)
- [054: Scoped ticks](../../../docs/decision-records/054-scoped-ticks.md)
- [056: Scripts for entities](../../../docs/decision-records/056-scripts-for-entities.md)
- [063: World state, and talking to entities](../../../docs/decision-records/063-world-state-and-talking-to-entities.md)
- [064: Starting again at a respawn](../../../docs/decision-records/064-starting-again-at-a-respawn.md)
- [065: Persisted game state](../../../docs/decision-records/065-persisted-game-state.md)
- [066: Actions that name what they do](../../../docs/decision-records/066-actions-that-name-what-they-do.md)

## What it is

This subsystem runs the control entities. Control entities are a small language in which a level wires its story beats together. Each kind of control entity that runs other entities has a runner here, and a runner runs actions on its targets. A runner finishes in the way its kind defines (`043 §3.1`).

The subsystem also defines what a script is: C# code that a `Script` entity names. Scripts are for what wiring control entities can't express. The subsystem also defines what a script can see in the world and what it can do there (`043 §3.1`, `051 §1.2`).

## What it is not for

What a body does when an action reaches it belongs to that body's controller. The scripts themselves are the game's content, so they don't live here. Reading and checking routes is the job of the world's data, and building runners from routes is the simulation's job, as is deciding when touches are polled. Tasks and their scheduler, in general, belong elsewhere.

## Runners

A runner receives its steps already resolved, as `Call`s (`045 §3.3`, `047 §2.3`). A `Call` pairs an action with the `IActionTarget` that runs it. Resolving steps in advance means running a route is a direct call with no lookup, and a runner knows nothing of ids or configurations. `Call` is the only place that maps a `TriggerAction` to the target's method for that action.

Each runner runs its steps on the `ITick` it is given, and returns a `TickTask` that finishes as its kind defines (`043 §2.3`).

A runner implements only the actions it responds to: `IActionTarget.Run`, `IActionTarget.Present`, or the switching actions. A runner holds only what its kind needs to decide whether to run. What an action changes belongs to the controller that responds to the action, not to the runner (`043 §1.9`).

A `Sequence`, a `Fanout` and a `Script` start a new run on every `Run`, alongside any run already in progress (`043 §1.7`), because what they run ends up at controllers, and controllers decide for themselves what a second request does. A runner that behaves differently when asked again declares that with a `ReentrantTask`, in one line. A `Trigger` and a `Once` join the run in progress when asked again.

### Sequences and fanouts

`SequenceRunner` runs its steps one at a time. It starts each step after the previous one finishes, and it finishes when the last step does. `FanoutRunner` starts all its steps together, in their listed order, and it finishes when all of them have finished.

### Switched runners

A `Trigger` and a `Once` run their step only while their switch is on. The switch is stored as its `ISwitchState` in the game's state, and it outlives the world. The runner is the only thing that writes that switch (`065 §1.6`).

`TriggerRunner` runs its step when it is set off and the switch is on. It finishes when its step finishes. Setting it off while its step is running starts nothing new (`043 §1.7`). Turning the switch off doesn't stop a run that is in progress.

`TriggerRunner` responds to `TurnOn`, `TurnOff` and `ToggleOnOff`, and those three finish immediately. It doesn't respond to `Run`, so a route can switch a trigger on or off, but can't set it off. Only the player sets a trigger off (`047 §1.1`, `052 §1.1`).

`OnceRunner` runs its step on `Run` when its switch is on, then turns the switch off at the end of its step, not at the start, so that a world rebuilt while the step runs runs it again from the start instead of leaving a beat half done. A `Run` that arrives while the step is running finishes when that run finishes. A `Run` that arrives after the switch has turned off finishes immediately (`047 §1.2`).

Like `TriggerRunner`, `OnceRunner` responds to `TurnOn`, `TurnOff` and `ToggleOnOff`, which lets a route arm it again.

## Dialogue

`IDialoguePresenter` shows dialogue. The game implements it with its dialogue backend (`043 §3.3`), so that Core knows nothing about the dialogue backend or the game's presenter. To present, it receives a tick, a dialogue file's name, a title in that file, and the entities the dialogue is about. Presenting returns a task that finishes when the dialogue ends.

The dialogue may ask its entities for actions through their controllers, and those actions run on the tick the presenter was given (`063 §3.3`).

The presenter plays no cutscene of its own. Whatever presents a dialogue holds a cutscene itself, if it wants one, so that the game's modes know nothing about dialogue. Dialogue without a cutscene, such as a callout, can be presented the same way (`051 §3.5`).

`IDialoguePresenter` also says whether a file contains a given title (`044 §5.2`). A world calls this when it is built, so a wrong file or title fails then, not when the dialogue is presented.

`DialogueRunner` handles a `Dialogue` control entity. On `Present`, it presents its title from its dialogue file, about the entities it is given. It holds a cutscene while the dialogue is presented, as a script would, and it finishes when the dialogue ends (`043 §2.3`, `050 §1.4`, `051 §3.5`). Each `Present` presents the dialogue again, queued after any dialogue already presented or waiting. Calls are not joined.

## Scripts

A script is a class that implements `IScript` (`043 §5.1`). Its one method, `Run`, is usually `async`. It receives an `IScriptWorld`, and the task it returns finishes when the script does. A new instance of the script is made for each run, and the instance is dropped when its run finishes, so a script keeps nothing between runs.

`ScriptRunner` makes a new script on each `Run`, and it finishes when that script finishes (`043 §1.7`).

### Where scripts come from

The scripts belong to the game and live in the game's project, so that level designers seldom need to touch Core (`051 §2.2, §2.3`, `056 §2.3`).

`IScriptLibrary` makes scripts by the name a `Script` entity gives. The game implements it, as it implements `IDialoguePresenter`, which hands the world what it needs from the game, so a test's dependencies are plain (`051 §2.2, §2.3`, `056 §2.3`).

`IScriptLibrary` also says whether a name belongs to a script that receives entities of given classes. The world checks its scripts this way when it is built, before it has any entities.

A script receives the entities its `Script` entity gives through its one public constructor, as `ScriptArguments` defines (`056 §2.2, §2.3`). A constructor parameter whose type is an entity type receives the next entity, and that entity must be of the parameter's type. A last parameter of type `IReadOnlyList` of an entity type receives all the entities that are left, each of which must be of its element type. That lets a script declare that it is about any number of entities. Every entity given must be received by some parameter, and a script with no constructor parameters receives no entities.

For now, constructor parameters are a script's only arguments. Other kinds of argument can be added beside entities later, without replacing anything (`056 §2.2, §2.3`).

### What a script sees

The simulation implements `IScriptWorld`. It extends `IWorldView`, which is what controllers see. `IScriptWorld` adds the player's controller, the ability to hold a cutscene, the world's `Tick`, and every entity by id (`046 §1.3`, `063 §3.2`).

The script waits on the `Tick` to pass time. An action gets the tick as an argument instead (`046 §1.3`). Entities by id are for code that knows an entity only by its id, such as a dialogue's actions (`063 §3.2`).

Controllers don't see the player's controller, so only scripts can drive the player (`051 §2.4`).

Every script sees the same `IScriptWorld`, whatever it is about, because a narrower view would only get widened, one request at a time (`056 §2.5`).

A script sees none of the world's data. `IScriptWorld` holds no rooms and no level data, so that what a script does depends on the live entities and colliders in the world, like everything else does (`056 §2.6`).

### Scripts about entities

A script sets the scope of its own runs. It makes a scope from its world's `Tick`, such as "while its entities are live", and waits on that scope, so the script ends when its entities go away. It ends through its `using` and `finally` blocks, while the entities are still whole. When a scope ends, it cancels the script and the route that awaits it. The world logs that cancellation as a warning, because a script is usually a beat with an end, so one cut short is worth noticing (`054`, `056 §3.6, §3.8`).

A script with no end of its own, which goes on as long as its entities last, loops with `While` instead. Such a script finishes when its entities go away, and it is usually doing a controller's job (`054`, `056 §3.6, §3.8`).

A script drives a body that has a controller through that controller whenever it can. It may push a body that has no controller. When it must push a body that has a controller, such as lifting the player for a powerup, it holds a cutscene first, so that the script doesn't fight the controller (`056 §2.4, §4.2`).

Holding a cutscene first is guidance, not a rule. A script may push a controlled body outside a cutscene when that is the better trade-off. An example is wind in only one place, which is nicer as a script than as support in Core. Once something like that is needed twice, it belongs in Core (`056 §2.4, §4.2`).

### Driving the player

A script drives the player by setting the player's controller's `ScriptedInput` (`051 §3.3`). `ScriptedInput` is input as the player would press it. The controller acts on it during a cutscene, and applies its own leniency and actions to it, so that a scripted run is one the player could make.

Helpers that wait for what the player does can be written on top of this as plain async code, which keeps scripts short (`051 §3.3`).

### Cutscenes

A script holds a cutscene with `IScriptWorld.HoldCutscene`. The hold is a handle that the script disposes with `using`, so that the cutscene ends when the script ends, even if the script fails. A script that doesn't drive the player, such as one for a robot, needn't hold a cutscene (`051 §3.1`, `§3.2`).

A hold carries a `CutsceneRequest`, which says how the game should present the cutscene, such as with black bars. Black bars tell the player that their control has been taken away. Holds can overlap, as a dialogue's hold does inside a script's hold, so the requests of overlapping holds are joined. `CutsceneRequest.Join` joins each field by that field's own rule. The rule for black bars is "or". The default request asks for nothing beyond the cutscene, and it is the join of no requests. Adding a new kind of presentation means adding a new field and its rule (`051 §3.6`).

### Running scripts

`RunningScripts` holds the scripts a world is running, in the order they started. The world keeps one, and every `ScriptRunner` runs its scripts in it. A script is listed until its run finishes, including when it throws or its scope ends.

## Open questions

Helpers for writing scripts are undecided (`051 §1.5`). One option is a fluent interface like `Promise`. Another is a small async library over what scripts and async actions see. The goal is that a wait such as "until the player is on the ground" takes one line. The helpers are to be shaped by writing scripts.

It is undecided whether anything besides a script's own timeout should stop a cutscene from being held forever (`064 §1.2`, `§3.1`). For now, each script that waits for something it can't tell won't happen guards itself with a timeout. `RunRightOffLedge` is an example. The options are:
  - nothing more;
  - a timeout on every hold, given when the hold is taken, such as in `CutsceneRequest`;
  - a debug key that cancels every script;
  - a warning in debug builds when a hold lasts too long.
