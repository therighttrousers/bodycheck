# Review of the rewritten comments in src/BodyCheck.Core/Scripts

## All runners (DialogueRunner, FanoutRunner, OnceRunner, ScriptRunner, SequenceRunner, TriggerRunner)

### Class summaries
- **2.** Each summary calls the runner "a step". In the code a step is a `Call` (the runners' `step` and `steps` parameters, `EntityConfig`'s `Step`). A runner is the `IActionTarget` that a step's action runs on, and the thing that runs a control entity (a `Trigger`, `Sequence`, `Fanout`, `Once`, `Dialogue` or `Script` entity). The fix must name the runner as what runs its kind of control entity, or as an action target, not as a step. This matters most where the summary also says the runner "runs its step" (TriggerRunner, SequenceRunner), because there the same word means two different things.

## Call.cs

### `Call` (summary)
- **1.** "One step in a script or trigger" is wrong. A script (`IScript`) is C# code and is not made of `Call`s. `Call`s are the steps of a route: a trigger's or a once's single step, and a sequence's or a fanout's steps.
- **3.** The old summary said a `Call` is a step that has already been resolved: the target is looked up when the world is built, so running it is a direct call with nothing to look up. The fix must say that `Target` is the resolved runner or controller, not an id.

## CutsceneRequest.cs

### `CutsceneRequest` (summary)
- **1.** "What a cutscene asks of the game" gives the wrong owner. Each *hold* carries its own request. The world combines the requests of every current hold into one with `Join`, and the game presents the cutscene as that combined request says.
- **3.** The summary no longer says that the default request asks for nothing beyond the cutscene itself, and that it is the join of no requests.

### `BlackBars` (param)
- **3.** The summary no longer says why black bars exist: they tell the player that their control has been taken away.

### `Join`
- **3.** The doc now gives only the black-bars rule. A maintainer needs the general rule: each field is joined by its own rule (for a flag that any hold may turn on, that rule is "or"). Adding a new kind of presentation therefore means adding a field and its rule in `Join`.

## DialogueRunner.cs

### `DialogueRunner` (summary) / `Present`
- **3.** The doc no longer says what happens when `Present` is called again: every call presents the dialogue again, queued after any dialogue that is already being presented or waiting. Calls are not joined.

### `entities` (param)
- **1.** "The first one is the speaker" states a convention of the game's dialogue implementation (the game's `DialogueState.Speaker` is the first entity) as if Core required it. Core passes the list through unchanged. The dialogue file chooses who speaks each line, and it can name any of these entities and ask any of them for actions. The fix must describe the entities as the ones the dialogue is about.

## FanoutRunner.cs

### `FanoutRunner` (summary)
- **3.** The doc no longer says that the steps are started in their listed order.
- **3.** The doc no longer says what a second `Run` does while a run is in progress: it starts the steps again, alongside the run in progress. There is no join or queue.

## IDialoguePresenter.cs

### `IDialoguePresenter` (summary)
- **3.** The summary no longer says why the interface exists: the game implements it, so that Core knows nothing of the dialogue backend or of how the game shows dialogue.
- **1.** "for a DialogueRunner" is too narrow. The world also calls `Holds` on the presenter when it is built, to check every dialogue in the world.

### `Present`
- **3.** The doc no longer says that dialogue is presented after any dialogue that is already being presented or waiting, in the order the calls were made.
- **3.** The doc no longer says that the presenter plays no cutscene of its own. A caller that wants a cutscene must hold one itself, as `DialogueRunner` does.
- **3.** The doc no longer says when awaiting code resumes after the dialogue ends: in the world's next tick.
- **3.** `file` (param): the doc no longer says how the file is named. It is the dialogue file's name, with no directory and no extension. `Holds` takes it in the same form.
- **3.** `entities` (param): the doc no longer says that the dialogue can ask these entities for actions through their controllers, and that those actions run on `tick`. The `tick` param's "the tick that the dialogue waits on" leaves this out.
- **1.** `entities` (param): "The first one is the speaker" is a convention of the game's implementation, not part of this interface's contract. See the note under DialogueRunner.

### `Holds`
- **3.** The doc no longer says when the check is meant to happen: when the world is built, so that a wrong file or title fails then, not when the dialogue is presented.

## IScript.cs

### `IScript` (summary)
- **1.** "such as a cutscene" gives the wrong impression that a script is a cutscene. A script may hold a cutscene while it runs. Many scripts hold none, such as one that drives a robot.
- **3.** The summary no longer says that a `Script` control entity is what runs a script, and that scripts are for logic that wiring control entities together can't express.
- **3.** The summary no longer says that a script is dropped when its run finishes, so it keeps nothing between runs. It also no longer says how a script receives its entities: through its one public constructor, as `ScriptArguments` describes (a `<see cref>`). The old comment's "constructor of no arguments" was out of date, but the fact that the constructor is how a script gets its inputs still matters.

### `Run`
- **3.** The doc no longer says that `Run` is usually written as an `async` method.

## IScriptLibrary.cs

### `IScriptLibrary` (summary)
- **3.** The summary no longer says why the interface exists: the game implements it, so that Core doesn't need to know the game's script types.
- **3.** The summary no longer says that a script takes its entities as `ScriptArguments` describes (a `<see cref>`).

### `Holds`
- **3.** The doc no longer says what the arguments are. `className` is the name that a `Script` entity gives. `entityTypes` are the classes of the entities that the `Script` entity gives, in order.
- **3.** The doc no longer says why `Holds` takes types rather than entities: the world checks its scripts when it is built, before it has any entities.

## IScriptWorld.cs

### `IScriptWorld` (summary)
- **3.** The summary no longer says that callers must read its members each time they need them, never once and kept, so that what a script sees is current. `IWorldView` has the same rule.
- **3.** The summary no longer says what it adds to `IWorldView`: the player's controller, which controllers don't get, so only scripts can drive the player; holding a cutscene; the world's tick; and every entity by id. It also no longer says that the simulation implements it.

### `PlayerController`
- **2.** `<c>ScriptedInput</c>` should be `<see cref="PlayerController.ScriptedInput"/>`, as the old comment had it.
- **3.** The doc no longer says that the player acts on `ScriptedInput` only during a cutscene. Setting it has no effect unless a cutscene is held.

### `HoldCutscene`
- **3.** The doc no longer says that the game plays a cutscene while any hold is held.
- **3.** The doc no longer says the timing: the game's mode changes after the world's tick, so controllers first see the cutscene on the next tick.
- **3.** The doc no longer tells the caller to hold it with `using`, so that the cutscene ends when the caller ends, even if it fails. `IScript.Run` says this, but other callers (such as `DialogueRunner`) hold cutscenes too, and this member is where the requirement belongs.
- **1.** "each option applies if any hold asks for it" states the current black-bars rule as the rule for every field. Requests are joined with `CutsceneRequest.Join`, where each field has its own rule (a `<see cref>` to it would serve).

### `Entities`
- **3 / 5.** "The world's entities, by id" leaves out which entities are included and why the property exists. It includes every entity in the world. An entity that has been removed stays until the end of the tick in which it was removed. The property exists so that code that knows an entity only by its id, such as dialogue, can find it.

## OnceRunner.cs

### `OnceRunner` (summary)
- **1.** "runs while its switch is on" suggests that it runs continuously, or by itself, while it is on. It runs its step only when it is asked to `Run` and the switch is on.
- **3.** The summary no longer says why the switch is turned off when the step finishes rather than when it starts. If the world is built again while the step runs (after a restart, a death or a load), the step runs again from its start rather than being left half done. The inline comment in `RunThenTurnOff` gives only the "if the step throws" case, which misses this main reason.

### `state` (param)
- **3.** The doc no longer says that the switch is the entity's switch state in the game's state, which is saved and outlives the world. That is why it is read through a function each time.

## ScriptArguments.cs

### `ScriptArguments` (summary)
- **2.** `<c>IReadOnlyList</c>` should be `<see cref="IReadOnlyList{T}"/>`, as the old comment had it.
- **3.** The summary no longer says that each entity must be of its parameter's type (for the list, every remaining entity must be of the element type), that every entity must be taken, and that a script with no parameters takes no entities. It also no longer says that any other parameter type makes the match fail.

### `Constructor`
- **3.** The doc no longer says why it takes types rather than entities: so that a world can check its scripts when it is built, before it has entities.

### `Bind` (inline comment)
- **3.** The removed inline comment explained why the list argument is built as an array of the element type: an array of the element type implements `IReadOnlyList` of that type. Without it, `Array.CreateInstance` looks arbitrary.

## ScriptRunner.cs

### `ScriptRunner` (summary)
- **1.** "Each run makes a new script, so runs can overlap" gives the wrong cause. Runs overlap because the runner has no reentry policy: every `Run` starts a run alongside any run in progress. Making a new script for each run is why the runs share no state.
- **3.** The summary no longer says that it is what runs a `Script` control entity, or that each run is listed among the world's running scripts (`running`) until it finishes.

## SequenceRunner.cs

### `SequenceRunner` (summary)
- **3.** The doc no longer says what a second `Run` does while a run is in progress: it starts the steps again from the first, alongside the run in progress.

## TriggerRunner.cs

### `TriggerRunner` (summary)
- **3.** The summary no longer says that a route can switch a trigger on and off but can't set it off, because the runner doesn't respond to `Run`. Only the world sets it off, through `SetOff`.

### `state` (param)
- **3.** Same as for OnceRunner: the switch is the trigger's switch state in the game's state, which is saved and outlives the world.

### `IsOn`
- **3.** The doc no longer says that turning the switch off doesn't stop a run that is in progress.
