# 053: Cancelling tasks

## Context

Inciting event (2026-11-06): pressing F5 while a dialogue is presented leaves it up. `Main` owns the `DialogueBox` across worlds, and nothing tells it that the world whose dialogue it presents is gone. Looking into it, Neil asked what happens to a script, such as `RunRightOffLedge`, that is running when the world is dropped. It doesn't run on: what comes after its `await` waits on the dropped world's `TickScheduler`, which nothing runs again, so it is collected unfinished. Its `using` and `finally` blocks never run, so its cutscene hold is never released, and `RunningScripts` never forgets it. That is harmless only while all of that lives in the dropped world.

`047 §2.5` left cancelling a run open until an action needed it, with three questions: how the run stops, what awaits it sees, and what puts right what it left half done. Dropping a world needs the first answered now, and the second for what awaits from off the tick, such as the dialogue box with its completion.

Goals:
- A dropped world's tasks stop, and their `using` and `finally` blocks run, so that cleanup written for an ordinary end also runs for this one.
- The dialogue box drops a dropped world's dialogue.
- One mechanism for anything to stop.

Anti-goals:
- .NET's `CancellationToken` everywhere: a parameter on every async method, which each must remember to check and pass on.

Closed on 2026-11-06, with item 3.2 settled by Claude while implementing it, for Neil to review. Outcomes are in the architecture docs: the root's conventions, and those of `Tasks`, `Simulation` and `game/main/`.

## 1. How a run stops

- [x] 1.1 How a suspended `async` method is stopped. C# gives one way to leave a method that is suspended at an `await` and still run its `finally` blocks: resume it, and have the `await` throw. Options:
  - (a) A flag or token the method checks after each `await` (`047 §2.5`). The method decides where it stops, but every method must check, and one that forgets runs on.
  - (b) The `await` throws a cancellation exception, which unwinds the method through its `using` and `finally` blocks, and fails its own task in turn, up the chain.
  - (c) Drop it without resuming it, as now. Nothing runs, which is the bug.
  - Decision (Claude, agreed by Neil): (b). It needs nothing of the methods, and cleanup runs where it is already written. The exception is .NET's `OperationCanceledException`, so that code that must tell it from a failure can, with .NET's usual idiom.
- [x] 1.2 What cancels a task, so that its awaiters' `await` throws. Every suspended method waits, down its chain, on a scheduler's wait or a completion: an async method's task finishes only when its method does. So cancelling those leaves unwinds everything above them. A task gets a third way to settle besides finishing and failing: cancelled. Decision (Claude, agreed by Neil): only the scheduler cancels tasks, as only it and the method builder make them (`046 §1.4`).
- [x] 1.3 Cancelled is not failed. A failed task fails the tick (`046 §1.6`); a cancelled one doesn't, since cancelling is asked for. An async method that a cancellation unwinds is cancelled too, not failed, so that cancelling a chain doesn't fail the tick on the way up. One whose `finally` throws something else while unwinding fails as usual (Claude, agreed by Neil).
- [x] 1.4 A method that catches every exception, such as `catch (Exception)`, catches the cancellation too, and runs on. Options: (a) a convention against catching everything, catching only what is expected; (b) the scheduler, once stopped, cancels every new wait at once, so such a method throws again at its next `await`, and a loop that catches and waits ends only when it stops catching. Decision: both. The convention is good to have in general, not only for cancelling (Neil); (b) comes free with item 2.2 (Claude).

## 2. Dropping a scheduler

- [x] 2.1 `TickScheduler` gets `Stop()`, which cancels every wait and every completion it made that hasn't settled, then runs what that queues, until nothing is left, so that every method unwinds in that call. The scheduler then keeps track of its completions, which today it only makes. After `Stop`, it runs no more ticks (Claude, agreed by Neil).
- [x] 2.2 What a method's `finally` sees while unwinding: a wait it makes is cancelled at once, so its `await` throws; it can still change the world, which is still whole. Decision (Claude, agreed by Neil): allowed, with no special case; cleanup that waits is a smell, but stopping it outright would need a rule the compiler can't check.
- [x] 2.3 Who stops a world's scheduler, and when. Options:
  - (a) `WorldNode` in `_ExitTree`. Automatic, and every way a world goes, now and later, stops it. But a world taken out of the tree to be put back, which nothing does yet, would stop too, and the nodes being freed are mid-exit while cleanup runs.
  - (b) `Main`, through a `WorldNode` method such as `Drop()`, before it frees the world. Cleanup runs while the world is still whole and in the tree, before the next is built. But each way of dropping a world has to call it, and tests and probes that drop worlds too.
  - (c) `WorldNode` on its `Predelete` notification: only when freed, but later still than (a).
  - Decision (Neil): (b). It is safer to stop the scheduler before the nodes are torn down. The world's `QueueFree` goes inside `Drop()`, so that there is one call to make, not two to remember (Claude).
- [x] 2.4 If cleanup fails while stopping: a `using`'s `Dispose` or a `finally` in an async method, run because the cancellation is unwinding it, throws an exception of its own. Its method's task then fails, not cancelled (item 1.3), and the failure would fail the tick, but `Stop` isn't a tick, and other methods may still be waiting to unwind. Options: (a) `Stop` throws at once, leaving the rest unwound and their cleanup undone; (b) `Stop` unwinds everything, then throws the first failure. Decision (Neil): (b), so that one bad `finally` doesn't leave the others' cleanup undone (Claude).

## 3. What awaits from off the tick

- [x] 3.1 How the dialogue box learns that the world is gone. The box is the only holder of a `TickCompletion`: it presents a dialogue off the tick, on Godot's frames, and finishes the completion when the dialogue ends. Options:
  - (a) A `Cancelled` event on `TickCompletion`, which the box listens for, and drops the request (Claude).
  - (b) The box presents a dialogue as an `async TickTask` method on the world's tick, which awaits each line and each advance, so that `Stop` unwinds it like any other, and its `finally` ends the dialogue (Neil). The completion was needed only because the box wraps its own presentation in a task.
  - Decision (Neil): (b). One way for anything to stop, with nothing for the box to listen for.
- [x] 3.2 What (b) needs (Claude, while implementing it):
  - Dialogue Manager hands each line over as a .NET `Task`, which a `TickTask` method can't await. The adapter is the one Tasks' open question leaned to: `ITick.Until(Task)`, which polls the `Task` each tick, so that no other thread touches the scheduler. Each line then shows in the first tick after it is fetched, rather than the first frame: a tick at most later.
  - The player's input arrives in `_UnhandledInput`, off the tick. It sets the ID of the line to go on to, which the method waits for with `ITick.Until(Func<bool>)`, a condition checked each tick, so that it needs no completion. `Until(Task)` is built on it.
  - Only one dialogue is presented at a time, and the next waits for it: a `ReentrantTask` with `Reentry.Queue`. A cancelled run has finished, so a new world's dialogue starts at once rather than waiting for, or awaiting across schedulers on, the dropped world's.
  - Nothing then uses `TickCompletion`, so it goes, with `ITick.NewCompletion`: `Until` waits for what happens off the tick, and `Stop` then has only its waits to cancel. A completion can come back if something off the tick needs to finish a task rather than be polled.
  - `Main` drops the old world after the new one is in place, rather than before building it, so that the game is whole if the old world's cleanup throws, or the new one fails to build.
- [x] 3.3 `ITick`, after review (Neil). Waiting on a condition takes an optional body, run each tick while it holds, so that a script says "do this while that" in one call, as `RunRightOffLedge` does, with overloads for no body, a body that runs at once, and one that returns a task. It was `Until`, with the condition negated, which is how gameplay is often thought of, but is `While`, since what people are used to pulls on the design too. So that it means only that, the overload for a .NET `Task` is `UntilCompleted`: it waits for the task to finish, fail or be cancelled. `ITick.Time` gives the scheduler's clock, so that a timeout is a deadline in the condition. `ITick.All` waits for several tasks, as JavaScript's `Promise.all` does, which `FanoutRunner` now uses. An `Any`, as `Promise.race`, was tried and taken out: it cancels nothing, so a race used for a timeout leaves the loser running, acting on the world each tick, and cancelling the losers is the cancelling of single runs that item 4.1 rules out. It waits for a use that isn't a timeout, which might cancel the losers by giving each its own tick to stop (Claude). `WorldNode.Drop` is `StopAndQueueFree`, which says what it does: stops the tick, as `TickScheduler.Stop` does, then Godot's `QueueFree`, with no word of its own that overlaps Godot's, such as `Kill`, which tweens have.

## 4. Cancelling one run

- [x] 4.1 Whether to cancel single runs at all (`047 §2.5`). Neil rarely felt the need for it in TypeScript, whose promises can be cancelled only cooperatively, by convention. The door's case is served by `Reentry.Queue`: `Open` asked during `Close` runs after it. Cancelling one run also has questions of its own: a run's task doesn't know what its method awaits, a run shared by `Reentry.Join` would be cancelled for every request that shares it, and what awaits it, such as a `Sequence`, would be cancelled too. Decision (Neil): a convention against cancelling a single run, and no mechanism for it. A run that should stop early does so cooperatively.
- [ ] 4.2 Cancelling everything without dropping the world, such as to start an important cutscene (Neil imagines it). It would be `Stop` that leaves the scheduler running afterwards, with the questions of what was left half done, such as a door unfrozen mid-close unless its `finally` freezes it, and whether waits made while unwinding are cancelled. Left until something needs it.
