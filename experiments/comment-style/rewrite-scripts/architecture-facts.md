# Scripts

Relevant decision records:
- [043: Triggers, actions and scripts](../../../docs/decision-records/043-triggers-and-scripts.md)
- [044: Dialogue](../../../docs/decision-records/044-dialogue.md)
- [046: Actions as async methods](../../../docs/decision-records/046-async-actions.md)
- [047: `Once`, and routing actions](../../../docs/decision-records/047-once-and-routing.md)
- [050: The cutscene mode](../../../docs/decision-records/050-cutscene-mode.md)
- [051: The scripted run at the spawn](../../../docs/decision-records/051-scripted-run-at-the-spawn.md)
- [052: The talking door](../../../docs/decision-records/052-talking-door.md)
- [056: Scripts for entities](../../../docs/decision-records/056-scripts-for-entities.md)
- [063: World state, and talking to entities](../../../docs/decision-records/063-world-state-and-talking-to-entities.md)
- [064: Starting again at a respawn](../../../docs/decision-records/064-starting-again-at-a-respawn.md)
- [065: Persisted game state](../../../docs/decision-records/065-persisted-game-state.md)
- [066: Actions that name what they do](../../../docs/decision-records/066-actions-that-name-what-they-do.md)

## What it is

- This subsystem runs the control entities. (`043 §3.1`, `051 §1.2`)
- Control entities are a small language that a level uses to wire its story beats together.
- Each kind of control entity that runs other entities has a runner here.
- A runner runs actions on its targets.
- A runner finishes in the way its kind defines.
- This subsystem also defines what a script is: C# code that a `Script` entity names.
- Scripts are for what wiring control entities can't express.
- This subsystem also defines what a script can see in the world and what it can do there.

## What it is not for

- What a body does when an action reaches it doesn't belong here. It belongs to the body's controller.
- The scripts themselves don't belong here. They are the game's content.
- Reading routes or checking them doesn't belong here. Routes are the world's data.
- Building the runners from routes doesn't belong here. That is the simulation's job.
- Deciding when touches are polled doesn't belong here. That is the simulation's job.
- Tasks and their scheduler, in general, don't belong here.

## Runners

- A runner receives its steps already resolved, as `Call`s. (`045 §3.3`, `047 §2.3`)
- Each `Call` is an action plus the `IActionTarget` that runs it.
- Resolving steps in advance means running a route is a direct call, with no lookup.
- Resolving steps in advance means a runner knows nothing of ids or configurations.
- `Call` is the only place that maps a `TriggerAction` to the target's method for that action.
- Each runner runs its steps on the `ITick` it is given. (`043 §2.3`)
- Each runner returns a `TickTask` that finishes as its kind defines. (`043 §2.3`)
- `TriggerRunner`: when it is set off and it is on, it runs its step.
- `TriggerRunner`: it finishes when its step finishes.
- `TriggerRunner`: setting it off while its step is running starts nothing new. (`043 §1.7`)
- `TriggerRunner`: it responds to `TurnOn`, `TurnOff` and `ToggleOnOff`.
- `TriggerRunner`: those three actions finish immediately.
- `TriggerRunner`: turning it off doesn't stop a run that is in progress.
- `TriggerRunner`: it doesn't respond to `Run`. (`047 §1.1`, `052 §1.1`)
- `TriggerRunner`: so a route can switch a trigger on or off, but can't set it off. (`047 §1.1`, `052 §1.1`)
- `TriggerRunner`: only the player sets a trigger off. (`047 §1.1`, `052 §1.1`)
- `TriggerRunner`: whether it is on is stored as its `ISwitchState` in the game's state. (`065 §1.6`)
- `TriggerRunner`: it is the only thing that writes its `ISwitchState`. (`065 §1.6`)
- `TriggerRunner`: its `ISwitchState` outlives its world. (`065 §1.6`)
- `SequenceRunner`: on `Run`, it runs its steps one at a time, starting each after the previous one finishes.
- `SequenceRunner`: it finishes when its last step finishes.
- `FanoutRunner`: on `Run`, it starts all its steps, in their order.
- `FanoutRunner`: it finishes when all its steps have finished.
- `OnceRunner`: on `Run`, when it is on, it runs its step. (`047 §1.2`)
- `OnceRunner`: it turns itself off when its step finishes. (`047 §1.2`)
- `OnceRunner`: a `Run` that arrives while its step is running finishes when that run finishes. (`047 §1.2`)
- `OnceRunner`: a `Run` that arrives after it has turned off finishes immediately. (`047 §1.2`)
- `OnceRunner`: it turns off at the end of its step, not at the start.
- `OnceRunner`: the reason is that if the world is rebuilt while the step is running, the step runs again from the start, instead of a beat being left half done.
- `OnceRunner`: like `TriggerRunner`, it responds to `TurnOn`, `TurnOff` and `ToggleOnOff`.
- `OnceRunner`: that lets a route arm it again.
- `OnceRunner`: whether it is on is stored as its `ISwitchState` in the game's state. (`065 §1.6`)
- `DialogueRunner`: on `Present`, it presents its title from its dialogue file. (`043 §2.3`, `050 §1.4`, `051 §3.5`)
- `DialogueRunner`: the dialogue is about the entities the runner is given. (`043 §2.3`, `050 §1.4`, `051 §3.5`)
- `DialogueRunner`: it holds a cutscene while the dialogue is presented, as a script would. (`043 §2.3`, `050 §1.4`, `051 §3.5`)
- `DialogueRunner`: it finishes when the dialogue ends. (`043 §2.3`, `050 §1.4`, `051 §3.5`)
- `DialogueRunner`: each `Present` presents the dialogue again.
- `DialogueRunner`: a repeated presentation comes after any dialogue that is already waiting.
- `ScriptRunner`: on `Run`, it runs a new instance of its script. (`043 §1.7`)
- `ScriptRunner`: it finishes when that script finishes. (`043 §1.7`)
- `ScriptRunner`: each `Run` starts a new script run, alongside any run already in progress. (`043 §1.7`)
- A `Sequence`, a `Fanout` and a `Script` start a new run on every `Run`, alongside any run in progress. (`043 §1.7`)
- The reason is that what they run ends up at controllers, and controllers decide for themselves what a second request does. (`043 §1.7`)
- A runner that behaves differently when asked again declares that with a `ReentrantTask`, in one line.
- A `Trigger` and a `Once` join the run in progress when asked again.
- Each runner implements only the actions it responds to: `IActionTarget.Run`, `IActionTarget.Present`, or the switching actions.
- A runner holds only what its kind needs to decide whether to run. (`043 §1.9`)
- What an action changes belongs to the controller that responds to the action, not to the runner. (`043 §1.9`)

## Dialogue

- `IDialoguePresenter` is what presents dialogue. (`043 §3.3`)
- The game implements `IDialoguePresenter` with its dialogue backend. (`043 §3.3`)
- The reason is that Core then knows nothing about the dialogue backend or the game's presenter. (`043 §3.3`)
- To present, it takes a tick, a dialogue file's name, a title in that file, and the entities the dialogue is about.
- Presenting returns a task that finishes when the dialogue ends.
- The dialogue may ask its entities for actions through their controllers. (`063 §3.3`)
- Those actions run on the tick the presenter was given. (`063 §3.3`)
- The presenter plays no cutscene of its own. (`051 §3.5`)
- Whatever presents a dialogue holds a cutscene itself, if it wants one. (`051 §3.5`)
- One reason is that the game's modes then know nothing about dialogue. (`051 §3.5`)
- Another reason is that dialogue without a cutscene, such as a callout, could be presented the same way. (`051 §3.5`)
- `IDialoguePresenter` also says whether a file exists and contains a given title. (`044 §5.2`)
- The reason is that a world can check its dialogues when it is built, rather than when they are presented. (`044 §5.2`)

## Scripts

- A script is a class that implements `IScript`. (`043 §5.1`)
- A new script instance is made for each run. (`043 §5.1`)
- The instance is dropped when its run finishes. (`043 §5.1`)
- So a script keeps nothing between runs. (`043 §5.1`)
- A script receives the entities its `Script` entity gives it through its one public constructor, as `ScriptArguments` defines. (`056 §2.2`, `§2.3`)
- A constructor parameter whose type is a type of `Entity` takes the next entity. (`056 §2.2`, `§2.3`)
- That entity must be of the parameter's type. (`056 §2.2`, `§2.3`)
- A last parameter of type `IReadOnlyList` of a type of `Entity` takes all remaining entities. (`056 §2.2`, `§2.3`)
- That lets a script declare that it is about any number of entities. (`056 §2.2`, `§2.3`)
- Every entity given must be taken by some parameter. (`056 §2.2`, `§2.3`)
- A script with no constructor parameters takes no entities. (`056 §2.2`, `§2.3`)
- For now, constructor parameters are a script's only arguments. (`056 §2.2`, `§2.3`)
- Other kinds of argument can be added beside entities later, without replacing anything. (`056 §2.2`, `§2.3`)
- `IScript` has one method, `Run`.
- `Run` is usually `async`.
- `Run` is given an `IScriptWorld`: what the script can see and do.
- The `IScriptWorld` holds the world's `ITick`, for the script to wait on.
- The task `Run` returns finishes when the script finishes.

### Where scripts come from

- The scripts belong to the game and live in the game's project. (`051 §2.2`, `§2.3`, `056 §2.3`)
- The reason is that level designers then seldom need to touch Core. (`051 §2.2`, `§2.3`, `056 §2.3`)
- `IScriptLibrary` makes scripts, by the name a `Script` entity gives. (`051 §2.2`, `§2.3`, `056 §2.3`)
- The game implements `IScriptLibrary`, as it implements `IDialoguePresenter`. (`051 §2.2`, `§2.3`, `056 §2.3`)
- The reason is that what a world needs from the game is handed to it, so a test's dependencies are plain. (`051 §2.2`, `§2.3`, `056 §2.3`)
- `IScriptLibrary` also says whether a name belongs to a script that takes entities of given classes. (`051 §2.2`, `§2.3`, `056 §2.3`)
- The reason is that a world can check its scripts, and what they are given, when it is built, before it has any entities. (`051 §2.2`, `§2.3`, `056 §2.3`)

### What a script sees

- The simulation implements `IScriptWorld`.
- `IScriptWorld` extends `IWorldView`, which is what controllers see.
- `IScriptWorld` adds the player's controller.
- `IScriptWorld` adds holding a cutscene.
- `IScriptWorld` adds the world's tick. (`046 §1.3`)
- An action gets the tick as an argument instead. (`046 §1.3`)
- `IScriptWorld` adds every entity, by id. (`063 §3.2`)
- Entities by id are for code that knows an entity only by its id, such as a dialogue's actions. (`063 §3.2`)
- Controllers don't see the player's controller. (`051 §2.4`)
- So only scripts can drive the player. (`051 §2.4`)
- Every script sees the same `IScriptWorld`, whatever it is about. (`056 §2.5`)
- The reason is that a narrower view would only get widened, one request at a time. (`056 §2.5`)

### Scripts about entities

- A script sets the scope of its own runs. (`054`, `056 §3.6`, `§3.8`)
- It makes a scope from its world's `Tick`, such as "while its entities are live", and waits on that scope. (`054`, `056 §3.6`, `§3.8`)
- So the script ends when its entities go away. (`054`, `056 §3.6`, `§3.8`)
- It ends through its `using` and `finally` blocks, while the entities are still whole. (`054`, `056 §3.6`, `§3.8`)
- When a scope ends, it cancels the script and the route that awaits it. (`054`, `056 §3.6`, `§3.8`)
- The world logs that cancellation as a warning. (`054`, `056 §3.6`, `§3.8`)
- The reason is that a script is usually a beat with an end, and here it was cut short. (`054`, `056 §3.6`, `§3.8`)
- A script with no end of its own, which goes on as long as its entities last, loops with `While` instead. (`054`, `056 §3.6`, `§3.8`)
- Such a script finishes when its entities go away. (`054`, `056 §3.6`, `§3.8`)
- Such a script is usually doing a controller's job. (`054`, `056 §3.6`, `§3.8`)
- A script drives a body that has a controller through that controller whenever it can. (`056 §2.4`, `§4.2`)
- A script may push a body that has no controller. (`056 §2.4`, `§4.2`)
- When a script must push a body that has a controller, such as lifting the player for a powerup, it holds a cutscene first. (`056 §2.4`, `§4.2`)
- The reason is that the script then doesn't fight the controller. (`056 §2.4`, `§4.2`)
- Holding a cutscene first is guidance, not a rule. (`056 §2.4`, `§4.2`)
- A script may push a controlled body outside a cutscene when that is the better trade-off. (`056 §2.4`, `§4.2`)
- An example is wind in only one place, which is nicer as a script than as support in Core. (`056 §2.4`, `§4.2`)
- Once something like that is needed twice, it belongs in Core. (`056 §2.4`, `§4.2`)

### What a script sees of the world's data

- A script sees none of the world's data. (`056 §2.6`)
- `IScriptWorld` holds no rooms and no level data. (`056 §2.6`)
- The reason is that what a script does then depends on the live entities and colliders in the world, like everything else does. (`056 §2.6`)

### Driving the player

- A script drives the player by setting the player's controller's `ScriptedInput`. (`051 §3.3`)
- `ScriptedInput` is input as the player would press it. (`051 §3.3`)
- The controller acts on `ScriptedInput` during a cutscene. (`051 §3.3`)
- The controller applies its own leniency and actions to it. (`051 §3.3`)
- The reason is that a scripted run is then one the player could make. (`051 §3.3`)
- Helpers that wait for what the player does can be written on top of this as plain async code. (`051 §3.3`)
- Such helpers keep scripts short. (`051 §3.3`)

### Cutscenes

- A script holds a cutscene with `IScriptWorld.HoldCutscene`. (`051 §3.1`, `§3.2`)
- The hold is a handle that the script disposes with `using`. (`051 §3.1`, `§3.2`)
- The reason is that the cutscene then ends when the script ends, even if the script fails. (`051 §3.1`, `§3.2`)
- A script that doesn't drive the player, such as one for a robot, needn't hold a cutscene. (`051 §3.1`, `§3.2`)

### How a cutscene is presented

- A hold carries a `CutsceneRequest`. (`051 §3.6`)
- A `CutsceneRequest` says how the game should present the cutscene, such as with black bars. (`051 §3.6`)
- Black bars tell the player that their control has been taken away. (`051 §3.6`)
- Holds can overlap, as a dialogue's hold does inside a script's hold. (`051 §3.6`)
- So the requests of overlapping holds are joined. (`051 §3.6`)
- `CutsceneRequest.Join` joins each field by that field's own rule. (`051 §3.6`)
- The rule for the black bars is "or". (`051 §3.6`)
- The default request asks for nothing beyond the cutscene. (`051 §3.6`)
- The default request is the join of no requests. (`051 §3.6`)
- Adding a new kind of presentation means adding a new field and its rule. (`051 §3.6`)

### Running scripts

- `RunningScripts` holds the scripts a world is running.
- It keeps them in the order they started.
- The world keeps one `RunningScripts`.
- Every `ScriptRunner` runs its scripts in the world's `RunningScripts`.

## Open questions

- Open: what helpers for writing scripts should look like. (`051 §1.5`)
- One option is a fluent interface like `Promise`. (`051 §1.5`)
- Another option is a small async library over what scripts and async actions see. (`051 §1.5`)
- The goal is that a wait such as "until the player is on the ground" takes one line. (`051 §1.5`)
- The helpers are to be shaped by writing scripts. (`051 §1.5`)
- Open: whether anything besides a script's own timeout should stop a cutscene from being held forever. (`064 §1.2`, `§3.1`)
- For now, each script that waits for something it can't tell won't happen guards itself with a timeout. (`064 §1.2`, `§3.1`)
- `RunRightOffLedge` is an example of a script with such a timeout. (`064 §1.2`, `§3.1`)
- Option: nothing more. (`064 §1.2`, `§3.1`)
- Option: a timeout on every hold, given when the hold is taken, such as in `CutsceneRequest`. (`064 §1.2`, `§3.1`)
- Option: a debug key that cancels every script. (`064 §1.2`, `§3.1`)
- Option: a warning in debug builds when a hold lasts too long. (`064 §1.2`, `§3.1`)
