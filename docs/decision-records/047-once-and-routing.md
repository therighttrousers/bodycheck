# 047: `Once`, and routing actions

## Context

Inciting event (2026-11-05): issue #161, routing actions between control entities. Under `043 §1.1` and `§1.8`, a `Trigger`, a `Sequence` and a `Fanout` each have a field `Once`, and a `Trigger` runs on being touched or on `Default`. Asked what a `Once` that has started should do when asked again, Neil found that a `Once` trigger behaved differently depending on how it was reached, by touch or by `Default`, and proposed taking `Once` out of all three, as `043 §1.5` took steps out of `Trigger`.

Goals:
- Each kind of control entity does one thing (`043`'s goals).
- One rule for what an entity does when asked again, whatever asks.
- Persisted state in as few kinds as possible (`043 §1.9`).

Closed on 2026-11-05. Outcomes are in the architecture docs of `Entities`, `WorldData`, `Tasks`, `Scripts` and `Simulation`.

## 1. `Once`

- [x] 1.1 A `Trigger` responds to no action: only a touch sets it off (Neil). Nothing needed it to respond to `Default`, and with no second way in, what it does when touched again while running is the only question: it runs nothing, as `043 §1.7` says. This supersedes the `Trigger`'s `Default` in `043 §1.1` and `§1.7`.
- [x] 1.2 `Once` is a kind of its own, not a field (Neil). On `Default`, it runs its `Action` on its `Target` the first time, and every `Default` gets that first run: one asked while it runs finishes when it does, and one asked after finishes at once. The field goes from `Trigger`, `Sequence` and `Fanout`, superseding `043 §1.8`. A trigger that should fire once targets a `Once`.
  - Rationale: `Once` was about what asks, not what runs. Only a `Trigger` is asked again by itself, by touches; a `Sequence` or `Fanout` runs only when something asks it. As a kind, it works in any route, such as a step of a `Sequence`, which the field couldn't, and it is the only kind with state that saving must keep.
  - Cost: two entities for every trigger that fires once, which in the first slice is every trigger. Neil expects `Once` to be used less than that suggests, and likes it visible on the map, so it may be placed out of its level's bounds, as the other control entities can.
- [x] 1.3 A policy field on `Once` in LDtk, choosing between joining a run in progress and finishing at once (Neil proposed). Dropped (Claude, agreed by Neil): it shows only when something waits on the `Once` while another route has it running, joining fits "finishes when that does", and finishing at once while running would disagree with finishing at once after. A field to add if something needs it.
- [ ] 1.4 When a `Once` counts as having run, for saving. If at the start, a save in the middle of its run, which restarts from the game state (`043 §5.4`), never finishes it after a load: the door of `Entrance` would never shut. If at the end, the same save runs it again from the start, and a `Once` has a third state, running, apart from having run. In memory it is the start, which item 1.2's rule gives. For the game state (issue #2).

## 2. Routing

- [x] 2.1 Re-entry policies (`043 §1.10`) are in `Tasks`, since nothing in them is about actions (Neil). Join, for a touched `Trigger`, which nothing awaits, so that joining and ignoring are the same; once, for `Once`, which is join for ever; and queue, which nothing uses yet, but will, and is small (Neil). Alongside, for `Sequence` and `Fanout`, is not a policy: it is the same as starting a run, and nothing chooses a policy at run time, now that `Once` has no policy field (item 1.3) (Neil). Ignore waits for something that needs it. A `ReentrantTask` is given what starts a run with each request, not once, so that one can serve several actions (Neil); actions that share one count as runs of the same work, so with join, a door's `Close` asked while its `Open` runs would get the `Open`'s run (Claude).
- [x] 2.2 The interface for running an action is in `Entities`, beside `IController`, which extends it (`043 §3.2`). `Entities` then uses `Tasks`, a new dependency, with no cycle (Claude). It has a method for each action, each a default member that throws, rather than one method that takes the action, so that a target implements an action as a method of its own, such as `async TickTask Close(ITick tick)`, with no dispatch of its own; the one dispatch from `TriggerAction` to method is in `Scripts`, where a step runs (Neil). Since the actions are now method names, `On` and `Off` are renamed `TurnOn` and `TurnOff`, as verbs (Neil).
- [x] 2.3 `Scripts` uses `Entities` and `Tasks`, not `WorldData`: `Simulation` builds each runner from its configuration, with its steps resolved to what runs them, so that a runner holds no ids (Claude, agreed by Neil). Since there are no cycles, it builds each runner after its targets.
- [x] 2.4 Targets that can't run their action yet finish at once, so that the levels load and play (Claude, agreed by Neil): a `Dialogue`, until its runner (issue #166); a `VerticalDoor`, until its controller (issue #163); and a `Respawn`, which becomes current by reach until respawns are made current by triggers (`043 §4.4`).
- [ ] 2.5 Cancelling a run in progress, such as a door's `Close` when asked to `Open` (Neil asked). .NET's pattern is a `CancellationToken` the method checks; ours needs designing first (Claude): whether the run stops by checking a flag after each `await`, or by its `await` throwing, so that `finally` can clean up; what whatever awaits it sees, since a failed task fails the tick (`046 §1.6`) and .NET's cancelled tasks cancel what awaits them, which would stop a `Sequence` waiting on the door; and what puts right what the run left half done. To be designed with the first action that needs it.
