# Tasks

Relevant decision records:
- [043: Triggers, actions and scripts](../../../docs/decision-records/043-triggers-and-scripts.md)
- [046: Actions as async methods](../../../docs/decision-records/046-async-actions.md)
- [047: `Once`, and routing actions](../../../docs/decision-records/047-once-and-routing.md)
- [044: Dialogue](../../../docs/decision-records/044-dialogue.md)
- [049: Doors and their stops](../../../docs/decision-records/049-doors-and-stops.md)

## What it is

Tasks that resume only on a tick: work in progress that can be awaited, and `async` methods that return it, run by a scheduler that its owner runs once a tick. It knows nothing of games, worlds or engines: what a tick is, when it runs, and what clock its waits count are its owner's.

## What it is not for

What runs on it, such as actions, routing or scripts, and when in a tick it runs. Work on other threads, or that resumes whenever something completes: that is .NET's `Task`.

## How it works

- **`TickTask`** is work in progress, which finishes once, or fails with an exception. It has no result yet (`046 §1.5`). An `async` method can return one, through `TickTaskMethodBuilder`. `TickTask.Finished` is one that has already finished, for work that finishes at once.
- **`TickScheduler`** runs them. Its owner calls `Run` once a tick, with the tick's length on whatever clock the waits are to count. Dropping it drops everything waiting on it.
- **`ITick`** is what a task sees of its scheduler: what to await to let ticks pass. Whatever starts a task passes it the `ITick` to run on, so which scheduler a task belongs to is never hidden state (`046 §1.3`). Its `Dt` is the length of the tick being run, so that a task can count time on the scheduler's clock (`049 §2.4`).
- **Nothing that awaits a task runs when it finishes.** What comes after each `await` goes to the task's scheduler, and runs when the scheduler next runs, until nothing is left, so that a chain of tasks that finish at once runs to its end in one tick. Finishing a task between ticks only queues what waits for it.
- **An `async` method that returns a `TickTask` awaits only `TickTask`s.** Awaiting anything else would resume it off the tick, so it fails the method's task. The method runs at once up to its first `await` of a task that hasn't finished, and takes that task's scheduler; one scheduler runs all of a chain.
- **Only the scheduler and the method builder make tasks:** the scheduler's waits and completions, and async methods' tasks. Only they finish them, but for a completion, which its holder finishes (`046 §1.4`).
- **Completions.** `ITick.NewCompletion` makes a `TickCompletion`, a task that something off the tick finishes or fails, such as the game when a dialogue ends, as with .NET's `TaskCompletionSource`. The scheduler still makes its task, and finishing it only queues what waits for it (`044 §2.4`).
- **Waiting.** `ITick.Wait` finishes once that much time has passed on the scheduler's clock, which counts the ticks it runs from the end of the last one: in the run whose tick ends nearest, so that rounding never adds a tick, but never in the run it is made in, so that a loop that waits can't spin within a tick. `NextTick` waits for no time, so it finishes in the scheduler's next run: for a task the scheduler resumed, the next tick's; for a wait made before this tick's run, this tick's.
- **Failing a task fails the tick.** The scheduler throws the exception when it next runs, whether or not anything awaits the task, so that it is never lost, and forgets everything queued (`046 §1.6`).
- **Re-entry.** A `ReentrantTask` starts runs of work that may be asked for while one is in progress, as its `Reentry` says, so that what responds to requests says in one line what a second one does. Each request passes what starts a run, which the policy calls or not: `Join` gives a request made during a run that run, and starts nothing; `Once` starts only the first run, and gives that run to every request, so that one made after it finishes at once; and `Queue` starts a run once the last one asked for has finished, so that runs follow one another in the order asked. Work that runs every request alongside any in progress needs none: it just starts a run. One `ReentrantTask` may serve several kinds of request, such as two actions, which then count as runs of the same work, so that with `Join` one gets the other's run (`043 §1.10`, `047 §2.1`). A request made while a run is being started, up to its first wait, throws, under every policy and whether or not another run is in progress: no policy can serve it, and with `Queue` it would deadlock, waiting for the run that waits for it, but only when another run was in progress. So work that shares a `ReentrantTask` calls the work itself, not the requests that go through it: a door's toggle starts the work of opening, not a request to `Open` (`049 §3.1`).
- Tasks and their scheduler are for one thread.

## Open questions

- Ignore, a re-entry policy which finishes at once while a run is in progress, when something needs it (`043 §1.10`).
- Cancelling a run, such as a door's `Close` when it is asked to `Open`, as a re-entry policy that replaces the run in progress (`047 §2.5`). To be designed: how the run stops (a flag or token it checks after each `await`, or its `await` throwing, so that `finally` can clean up), what awaits it then sees, without failing the tick or stopping a `Sequence` that waits for it, and who puts right what it left half done.

- What to do with a .NET `Task`, if something hands us one. Leaning: keep the rule that an async method awaits only `TickTask`s, and adapt at the edge, such as with an `ITick` method that polls the `Task` each tick, so that no other thread touches the scheduler. Accepting any awaiter in the method builder would need the method's scheduler before its first `TickTask` await, and a scheduler safe to add to from other threads. `Task.Delay` is no substitute for `Wait`: it counts real time on a timer, so it runs on through pauses, ignores the game's speed, resumes in whichever tick its thread's timing gives, and outlives a dropped world.
