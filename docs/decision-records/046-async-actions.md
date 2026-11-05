# 046: Actions as async methods

## Context

Inciting event (2026-11-04): issue #160, `ActionTask` and its queue. Neil, thinking ahead to the door (issue #163), asked how a `DoorController` would wait for its door to finish closing. Under `043 §2.2`, a controller records an action and acts on it in its next `Apply`, so the door would keep the `ActionTask` of the close in progress in a field and finish it by hand. Neil found that unsatisfying: with `async` and `await` for actions, he wants to write `public async ActionTask Close()`, with all of its logic in it, the ray cast, the impulse and the wait while watching the door settle, and with no `new ActionTask` in it.

Goals:
- An action that takes several ticks reads as one method, as a script does (`043 §5.1`).
- No hidden state says which world a task belongs to.
- What awaits still resumes inside the world's tick (`043 §2.4`).

Closed on 2026-11-04. Outcomes are in the architecture docs: the root's conventions, Core's, and `Simulation`'s.

## 1. Async actions

- [x] 1.1 A controller's response to an action may be an `async` method that returns an `ActionTask`, and runs where the world runs its queue rather than in the controller's next `Apply` (Neil). This supersedes `043 §2.2` for controllers; the `Dialogue` runner and `GameModeManager` are unchanged. What a controller does every tick stays in `Apply`, and a controller whose `Apply` and async actions drive the same body keeps them out of each other's way. Neil asked whether this is reasonable. Claude: yes, it is what the awaitable is for, and controllers respond to actions as scripts do (`043 §2.1`); the costs are the two places a controller acts, the order of resumptions following the order in which tasks finish, and a method in progress not being saved (`043 §5.4`), so an action sets the state that persists itself (`043 §1.9`).
- [x] 1.2 Where the queue runs (`043 §2.5`): after the entity steps and their deferred actions, before the engine's step, on played ticks, and only while controllers are enabled (Claude, agreed by Neil). Every controller has run by then, so an async action changes a body at once, such as freezing the door, with no `IDefer`, and its impulses act in this tick's step. It sees what this tick's deferred actions changed, which a controller doesn't.
- [x] 1.3 Which queue a task belongs to, without the action naming the queue. Options:
  - (a) A current queue, set by the world around its tick. Ambient state, but making a task outside the tick becomes an error.
  - (b) One static queue for the game, as `Features.Current` is. Rebuilding a world has to clear it, and two worlds at once break it.
  - (c) Tasks with no queue, which take one when awaited, down a chain whose top the world hands its queue. Hidden machinery, and a chain nothing hands to a queue never resumes, silently.
  - (d) The world's tick passed as a parameter, an `ITick`, as `IDefer` is to `Apply`: `Close(ITick tick)`, with `await tick.NextTick()` or `await tick.Wait(seconds)`.
  - Decision (Neil): (d). Which queue is always the one passed, and the compiler checks it.
- [x] 1.4 Only Core makes and finishes `ActionTask`s: the queue's waits and async methods' tasks. Nothing else writes `new ActionTask` (Neil). How the game finishes one from outside the tick, such as when Dialogue Manager's dialogue ends (`043 §2.4`), is for the dialogue interface (issue #164).
- [x] 1.5 An `ActionTask` has no result for now (Claude, accepted by Neil): nothing that awaits one needs a value yet. A generic one comes when something returns a value, such as a dialogue's choice.
- [x] 1.6 Failing a task fails the tick (`043 §2.7`): the queue throws the exception when it next runs, whether or not anything awaits the task, and forgets everything queued (Claude, accepted by Neil). A method's task fails if the method awaits anything but an `ActionTask`, which would resume it outside the tick, so that rule of `043 §2.4` is checked as it runs.
- [ ] 1.7 Whether `IDefer` becomes awaitable points of `ITick` (Neil asked). Async code needs no `RunAfterEntitySteps`, since it runs after the entity steps; `Apply` still does, for one-tick work. `RunAfterPhysicsStep` has no counterpart: `NextTick` resumes after the next tick's entity steps, too late for a change such as gravity that controllers reckon with. They would be one mechanism if the queue also ran at those points and `ITick` offered them to await. Claude: not until a second use needs it.
