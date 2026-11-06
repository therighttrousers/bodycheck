# Scripts

Relevant decision records:
- [043: Triggers, actions and scripts](../../../docs/decision-records/043-triggers-and-scripts.md)
- [044: Dialogue](../../../docs/decision-records/044-dialogue.md)
- [046: Actions as async methods](../../../docs/decision-records/046-async-actions.md)
- [047: `Once`, and routing actions](../../../docs/decision-records/047-once-and-routing.md)
- [050: The cutscene mode](../../../docs/decision-records/050-cutscene-mode.md)
- [051: The scripted run at the spawn](../../../docs/decision-records/051-scripted-run-at-the-spawn.md)

## What it is

What runs the control entities, the small language in which a level wires its story beats: each kind of control entity that runs others has a runner here, which runs actions on its targets and finishes as its kind says. Also what a script is, the C# that a `Script` entity names, for what wiring can't say, and what a script can see and do in the world (`043 §3.1`, `051 §1.2`).

## What it is not for

What a body does when an action reaches it: that is its controller's. The scripts themselves, which are the game's content. Reading or checking routes, which are the world's data, or building the runners from them and deciding when touches are polled, which is the simulation's. Tasks and their scheduler in general.

## Runners

A runner is given its steps already resolved, as `Call`s, each an action and the `IActionTarget` that runs it, so that running a route is a direct call with nothing looked up, and a runner knows no ids or configurations (`045 §3.3`, `047 §2.3`). `Call` is the one place that turns a `TriggerAction` into the target's method for it. Each runs its steps on the `ITick` it is given, and returns a `TickTask` that finishes as its kind says (`043 §2.3`):

- **`TriggerRunner`**: when touched, runs its step, and finishes when the step does. A touch while the step runs starts nothing (`043 §1.7`). It responds to no action, so only a touch sets it off (`047 §1.1`).
- **`SequenceRunner`**: on `Default`, runs each step in turn, each when the one before has finished, and finishes when the last does.
- **`FanoutRunner`**: on `Default`, starts every step, in order, and finishes when all have.
- **`OnceRunner`**: on the first `Default`, runs its step, and every `Default` gets that first run, so one asked while it runs finishes when it does, and one asked after finishes at once (`047 §1.2`). It remembers this in memory, for as long as its world lasts, until the game state saves it.
- **`DialogueRunner`**: on `Default`, presents its title of its dialogue file, and finishes when the dialogue ends (`043 §2.3`, `050 §1.4`). Every `Default` presents it again, after any dialogue already waiting.
- **`ScriptRunner`**: on `Default`, runs a new instance of its script, and finishes when the script does. Every `Default` runs one, alongside any run in progress (`043 §1.7`).

A `Sequence`, a `Fanout` or a `Script` runs every `Default`, alongside any run in progress, since what it runs comes down to controllers, which decide for themselves what a second request does (`043 §1.7`). A runner that does something else when asked again says so with a `ReentrantTask`, in one line: a `Trigger` joins, and a `Once` runs once. The runners that respond to `Default` implement only `IActionTarget.Default`.

What a runner holds is only what its kind needs to decide whether to run: what an action changes belongs to the controller that responds to it (`043 §1.9`).

## Dialogue

`IDialoguePresenter` is what presents dialogue, which the game implements with its dialogue backend, so that Core knows nothing of either (`043 §3.3`): given a tick, a dialogue file by name and a title in it, it returns a task that finishes when the dialogue ends. The game plays a cutscene for it, starting at its own point in the tick. It also says whether a file exists and holds a title, so that a world can check its dialogues when it is built, not when they are presented (`044 §5.2`).

## Scripts

A script is a class that implements `IScript`, with a constructor of no arguments: a new one is made for each run and dropped when it finishes, so it keeps nothing between runs (`043 §5.1`). Its one method, `Run`, usually `async`, is given what it can see, an `IScriptWorld`, and the world's `ITick` to wait on, as an action is (`046 §1.3`), and its task finishes when the script does.

- **Where scripts come from.** The scripts are the game's, in its project, so that level designers seldom need Core. `IScriptLibrary` is what makes them, by the name a `Script` entity gives, which the game implements, as it implements `IDialoguePresenter`, so that what a world needs from the game is given to it, and a test's dependencies are plain. It also says whether a name is a script's, so that a world can check its scripts when it is built (`051 §2.2`, `§2.3`).
- **What a script sees.** `IScriptWorld`, which the simulation implements, extends `IWorldView`, what controllers see, with the player's controller and holding a cutscene. Controllers don't see the player's controller, so only scripts can drive the player (`051 §2.4`).
- **Driving the player.** A script sets the player's controller's `ScriptedInput`, input as the player would press it, which the controller acts on in a cutscene, through its own leniency and actions, so that a scripted run is one the player could make. Helpers that wait for what the player does, written on that as plain async code, keep scripts short (`051 §3.3`).
- **Cutscenes.** A script holds a cutscene with `IScriptWorld.HoldCutscene`, through a handle disposed with `using`, so that the cutscene ends when the script does, even if it fails. A script that drives no player, such as one for a robot, needn't hold one (`051 §3.1`, `§3.2`).
- **Running scripts.** `RunningScripts` holds the scripts a world is running, in the order they started; the world keeps one, which every `ScriptRunner` runs its scripts in.

## Open questions

- Helpers for writing scripts: a `Promise`-like fluent interface, or a small async library over what scripts and async actions see, so that a wait such as "until the player is on the ground" is one line. To be shaped by writing scripts (`051 §1.5`).

- When a `Once` counts as having run, for saving: at the start of its run, so that a save in the middle of it never finishes it after a load, or at its end, so that the same save runs it again, with a third state, running, apart from having run (`047 §1.4`).
