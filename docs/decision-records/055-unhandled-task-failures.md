# 055: Unhandled task failures

## Context

Inciting event (2026-11-07): deciding what a scope's condition that throws should do (`054 §1.4`) led Neil to reopen how a failed task is reported. Today failing a task fails the tick (`046 §1.6`): the task queues its exception, `TickScheduler.Run` throws it when it reaches it, and then forgets everything queued. Neil wasn't clear on this point when the scheduler was first designed.

What that does now:
- The exception surfaces from `Run`, and through it from `WorldNode`'s physics tick, which skips the rest of the tick, such as updating the current respawn. Its stack trace still shows the frames of the method that threw, since `ExceptionDispatchInfo` keeps them.
- Forgetting the queue drops the continuations of every other task queued in that run, so unrelated scripts never resume: one broken script hangs the rest, for good.
- A cleanup that throws while `Stop` unwinds is thrown by `Stop`, once everything has unwound (`053 §2.4`).

Continuations themselves seldom throw: each is an async method's `MoveNext`, whose builder catches what the method throws and fails its task. What throws in `Run` is the entry queued for each failure.

Closed on 2026-11-07. Outcomes are in the architecture docs: the root's conventions, Core's, and those of `Tasks` and `Simulation`.

Goals:
- Blame falls on the code that failed, such as a script, not on the scheduler. We are extending `async`/`await`, so failures should behave as the language's do: they go to what awaits them.
- Other tasks carry on, in the same run, when one fails.
- No failure is lost.
- Tests can see that a failure happened, and which.

## 1. Where a failure shows up

- [x] 1.1 In what awaits the failed task, as now: its `await` throws the exception, so that a `Sequence` awaiting a broken script fails too, through its `finally` blocks. Decision (Neil, Claude): unchanged.
- [x] 1.2 A failure that reaches a task nothing awaits is unhandled, as a rejected promise that nothing handles is in JavaScript. It is reported to a handler, a sink that the scheduler's owner gives it, and `Run` never throws a task's failure. Decision (Neil): a sink, proposed by Claude.
- [x] 1.3 When a failed task counts as unawaited. Something may await it later in the same run, so it is checked at the end of the run, as JavaScript checks at the end of its microtask queue: a task that failed during the run and that nothing has awaited by its end is reported. Each chain then reports once, at its top, since what is below it was awaited. A `Fanout` whose two steps fail reports both: `All` stops awaiting at the first, so nothing awaits the second (Claude).
- [x] 1.4 A failed task awaited after it was reported, such as the first run that `OnceRunner` gives later requests, fails what awaits it, which may be reported again. That is a second chain failing, so both are reported (Claude).
- [x] 1.5 An async method that fails before its first `await` has no scheduler, and still throws to its caller, as a method that isn't async would (Claude).
- [x] 1.6 `Stop` reports a cleanup that throws while unwinding to the same sink, and doesn't throw, which replaces `053 §2.4` (Claude).

## 2. The sink

- [x] 2.1 How the sink is given. Options: (a) a required constructor argument, `TickScheduler(Action<Exception> unhandled)`, so that every owner decides what a failure means; (b) optional, and without one `Run` finishes the run and then throws the first unhandled failure, as `Stop` does now, which gives the scheduler two behaviours. Claude: (a).
- [x] 2.2 What the world does with one. Decision (Neil): `GD.PushError`, as Claude proposed; pausing is left until it is missed. Claude: `GD.PushError` with the exception, so that Godot's debugger shows it with the failed code's stack trace, and the game carries on. Perhaps also pausing the game in debug builds, as a breakpoint at the failure would.
- [x] 2.3 How tests see failures: a sink that collects them, such as `new TickScheduler(failures.Add)`, then asserting on the list. A test that expects none passes a sink that collects and checks the list is empty, or one that fails the test.

## 3. Scope conditions that throw

- [x] 3.1 Where the exception goes. Options: (a) it fails the scope's waits, so that it reaches the script through its `await`; (b) it goes to the sink. Decision (Neil): (b). With a sink for what nothing else handles, it is wrong to blame the scope's waits.
- [x] 3.2 What then becomes of the scope's waits. If they are kept, the condition throws again in every run, and is reported each time. Options: (a) kept, and reported each run; (b) the scope counts as ended, so its waits are cancelled and unwind as for any scope that ends, with their cleanup, after the exception is reported once; (c) the waits are dropped without being cancelled, so what awaits them never resumes. Decision (Neil): (b), as Claude proposed: a condition that can't say the scope holds can't keep it open, and the cancellation blames nothing.
- [x] 3.3 A condition shared by several waits, as every wait of one `ScopedTick` shares it, is called once for each, so it may throw for each in one run. Decision (Neil): report it once a run for each condition, as Claude proposed, which with §3.2 (b) means once in all.

## 4. Implementing it

- [x] 4.1 Claude's sketch, built so (Claude), for Neil to review. A scope's condition that throws is reported at the end of the run too, with the tasks, in the order failed, so that a sink that throws can't leave the scheduler's waits half checked. The world's sink is `GD.PushError`; pausing in debug builds (§2.2) is left open. Tests give schedulers that shouldn't fail a sink that rethrows. `TickTask.Settle` no longer queues the failure, but tells the scheduler the task has failed; the task keeps a flag for whether it has been awaited, set when an awaiter attaches, and when the exception is read from a task already finished, which never attaches one. The scheduler keeps the tasks that failed in the run, and at its end, and at the end of `Stop`, reports those not awaited. Forgetting the queue stays only as a guard against a bug in the scheduler itself. Every `new TickScheduler()` gets a sink, and the tests that expect `Run` or `Stop` to throw assert on what the sink got instead.
