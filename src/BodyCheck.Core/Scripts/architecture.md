# Scripts

Relevant decision records:
- [043: Triggers, actions and scripts](../../../docs/decision-records/043-triggers-and-scripts.md)
- [044: Dialogue](../../../docs/decision-records/044-dialogue.md)
- [046: Actions as async methods](../../../docs/decision-records/046-async-actions.md)
- [047: `Once`, and routing actions](../../../docs/decision-records/047-once-and-routing.md)

## What it is

What runs the control entities, the small language in which a level wires its story beats: each kind of control entity that runs others has a runner here, which runs actions on its targets and finishes as its kind says. Later, the C# scripts that a `Script` entity names (`043 §3.1`).

## What it is not for

What a body does when an action reaches it: that is its controller's. Reading or checking routes, which are the world's data, or building the runners from them and deciding when touches are polled, which is the simulation's. Tasks and their scheduler in general.

## Runners

A runner is given its steps already resolved, as `Call`s, each an action and the `IActionTarget` that runs it, so that running a route is a direct call with nothing looked up, and a runner knows no ids or configurations (`045 §3.3`, `047 §2.3`). `Call` is the one place that turns a `TriggerAction` into the target's method for it. Each runs its steps on the `ITick` it is given, and returns a `TickTask` that finishes as its kind says (`043 §2.3`):

- **`TriggerRunner`**: when touched, runs its step, and finishes when the step does. A touch while the step runs starts nothing (`043 §1.7`). It responds to no action, so only a touch sets it off (`047 §1.1`).
- **`SequenceRunner`**: on `Default`, runs each step in turn, each when the one before has finished, and finishes when the last does.
- **`FanoutRunner`**: on `Default`, starts every step, in order, and finishes when all have.
- **`OnceRunner`**: on the first `Default`, runs its step, and every `Default` gets that first run, so one asked while it runs finishes when it does, and one asked after finishes at once (`047 §1.2`). It remembers this in memory, for as long as its world lasts, until the game state saves it.
- **`DialogueRunner`**: on `Default`, presents its title of its dialogue file, and finishes when the dialogue ends (`043 §2.3`). Every `Default` presents it again, after any dialogue already waiting.

A `Sequence` or a `Fanout` runs every `Default`, alongside any run in progress, since what it runs comes down to controllers, which decide for themselves what a second request does (`043 §1.7`). A runner that does something else when asked again says so with a `ReentrantTask`, in one line: a `Trigger` joins, and a `Once` runs once. The runners that respond to `Default` implement only `IActionTarget.Default`.

What a runner holds is only what its kind needs to decide whether to run: what an action changes belongs to the controller that responds to it (`043 §1.9`).

## Dialogue

`IDialoguePresenter` is what presents dialogue, which the game implements with its dialogue backend, so that Core knows nothing of either (`043 §3.3`): given a tick, a dialogue file by name and a title in it, it returns a task that finishes when the dialogue ends. The game pauses for it at its own point in the tick. It also says whether a file exists and holds a title, so that a world can check its dialogues when it is built, not when they are presented (`044 §5.2`).

## Open questions

- When a `Once` counts as having run, for saving: at the start of its run, so that a save in the middle of it never finishes it after a load, or at its end, so that the same save runs it again, with a third state, running, apart from having run (`047 §1.4`).
