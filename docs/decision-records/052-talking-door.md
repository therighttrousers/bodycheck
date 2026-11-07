# 052: The talking door

## Context

Inciting event (2026-11-06): issue #180, the stretch goal of the first slice: the door of `Entrance`, once it has shut the player in, gives thin but plausible excuses for not opening. `043 §6.5` planned it as a `Dialogue` from a touch trigger in front of the door, with the door as the dialogue's speaker through a `Target` that `Dialogue` would get.

A touch trigger in front of the door would go off as the player passes it on the way in, while the door is still open, and again if the player goes back before the trigger further on shuts the door. Something has to keep it quiet until the door has shut.

Goals:
- The least new machinery that does it well, built so that later uses, such as robots that talk, build on it instead of replacing it.
- Something on screen that says the door can be talked to.

Closed on 2026-11-06. Outcomes are in [dialogue.md](../designs/dialogue.md), [rendering.md](../designs/rendering.md) and the architecture docs: the root's conventions, and those of Core, `WorldData`, `Levels`, `Scripts`, `Simulation` and `game/main/`.

## 1. Turning triggers on and off

- [x] 1.1 A `Trigger` responds to `TurnOn`, `TurnOff` and `ToggleOnOff` (Claude, agreed by Neil). A trigger that is off isn't set off. Switching it finishes at once, and turning it off doesn't stop a run in progress. It still doesn't respond to `Default`, so a route can switch a trigger but never set it off: only the player does. This supersedes the part of `047 §1.1` that says a trigger responds to no action. Whether it is on is the runner's state, since it decides whether the trigger runs, as `043 §1.9` allows. It lives in memory, as `Once`'s does, until the game state saves it.
- [x] 1.2 A required field, `StartsOn`, says whether a trigger starts on; LDtk defaults it to true (Neil).
- [x] 1.3 `Entrance`'s talking trigger starts off, and the `Sequence` that shuts the door ends by turning it on (Neil).
- [x] 1.4 A trigger tracks whether it holds the player whether it is on or off, so a touch trigger turned on with the player already in it is set off only when the player comes into it again.
- [ ] 1.5 The check for cycles still counts a route that switches a trigger as leading into the trigger's own step, though switching never runs it. So a trigger whose route turns that same trigger off is refused. Allowing it would need each runner built before the steps it runs are resolved, since the world builds each runner from steps that are already resolved. Nothing needs it yet; `Once` covers firing once.

## 2. Setting a trigger off by interacting

- [x] 2.1 Options for starting a conversation (Claude): (a) a trigger that the player sets off by interacting instead of by touch; (b) letting the player talk to any entity, through an area that moves with its body and a field on every kind of entity. (b) needs more: areas built with bodies, the field on each kind in LDtk and checked by the reader, a way to say when an entity can talk, which on the door would clash with its `Open` and `Close` if done with on and off, a prompt that follows a moving body, and decisions about facing the player or stopping during a conversation that belong to the slice where robots talk. The door is frozen while it is shut, so a trigger's fixed box in front of it fits. Decision (Neil): (a). (b), when robots talk, can probably be built as an interact trigger whose area moves with a body.
- [x] 2.2 A trigger's field `SetOffBy`, of LDtk's enum `TriggerSetOffBy`, is `Touch` or `Interact` (Neil). An enum rather than a bool leaves room for other ways, such as a block on a pressure plate. It is the same kind of entity, with the same area, runner and re-entry; only what sets it off differs.
- [x] 2.3 Interact is up (Claude, agreed by Neil). Looking up needs up held for a while, so a press is free; it is the convention of Hollow Knight. A trigger set off by interacting is set off when it holds the player and up has been pressed since the last tick, not held from before. Pressing up starts the dialogue at once, so the player doesn't look up there.
- [x] 2.4 Only on the ground (Neil), which is conventional. Only while playing: in a cutscene the player acts on no input of its own, and a phase being aimed or slowed for takes up as a direction.
- [x] 2.5 The speaker is a name in the dialogue file, `Door`, with an avatar if `assets/avatars/` has one by that name, so `Dialogue` needs no `Target` for one (Claude, agreed by Neil). This supersedes `043 §6.5`'s plan. Its excuses vary with Dialogue Manager's random lines: one of a group of lines that start with `%` is chosen each time.

## 3. The prompt

- [x] 3.1 While the player could set off an interact trigger, the game draws a speech bubble centred over the top of its box, only while it is in range, not always (Claude, agreed by Neil). The world says which trigger that is, as of the last tick of play: on, set off by interacting, holding the player, which is on the ground, while playing. If several are, the first in the order of the world's controls.
- [x] 3.2 The bubble is an image, `assets/prompts/interact.png`, 11 by 7 pixels, a placeholder that can be redrawn in any pixel editor. It is drawn in the game's viewport, under the colour pass, as `PhaseAimMarker` is, at whole pixels, and only while playing.
- [ ] 3.3 The prompt shows while a trigger's step runs outside a cutscene, though a press then starts nothing, by `043 §1.7`. No interact trigger yet runs a step that isn't a cutscene.
