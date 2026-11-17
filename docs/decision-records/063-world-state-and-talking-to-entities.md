# 063: World state, and talking to entities

Status: open: the speakers' names in translation (§3.6), and whether to close with the beat as built (§4).

## Context

Inciting event (2026-11-16): issue #214, a beat: a second door in `Entrance`, closed, which the player talks into opening. Having to talk to a door to go on is how the player starts to see that everything that moves is intelligent. It needs some of #2, so that a door's dialogue can read and change the world's state, and some of #139, so that the player talks to entities rather than to triggers.

Goals:
- Artists, level designers and writers, or devs in those roles, extend the world's state without digging into `BodyCheck.Core`. The game owns the state, and Core sees a view of it with only what the game's rules need.
- Core's view can differ from what is stored: a player's ability can be off for a story beat, `CanPhase => HasPhase && !DisabledForStoryBeat`, with nothing saved for it.
- What dialogue does to the world is a request, like a signal, not a change to state: state has an owner, and others ask the owner to change it.

Anti-goals:
- Saving and loading, in this record. How it may work shapes the state's design, so it is discussed, but nothing is built for it (§1.7).

## 1. World state

- [x] 1.1 Who owns the world's state. Decision (Neil): the game, as a class `WorldState`, which implements Core's `IWorldState`, a view of only what Core's rules read or change. The game can grow state Core never sees, such as an inventory or what the story has reached.
- [x] 1.2 A door's state. Decision (Neil): its `LastAction`, `Open` or `Close`, which its controller sets when it starts the action, not when it finishes. Toggling reverses it. Asking for the same action twice runs it again, which is valid: a door can be held open by something under it, or kept from opening from above, so the second ask may be the one that gets there. Starting from the last action rather than where the door is lets a load replay it (§1.7).
- [x] 1.3 Every `Entity` has an `Id`, its configuration's, so that its controller can find its state (Neil). Nothing else of the configuration is useful to a controller. The player's block, which no configuration builds, is `player-block`.
- [x] 1.4 Where state is filled in. Claude: the game, from the world's data, before it hands the state to the world, so that Core only looks state up by id and never makes it. Decision (Neil): so. `IWorldState` shrinks to `Player` and `GetDoor(id)`; finding by name is the game's, for writers.
- [x] 1.5 Names. Entities are machines, with names like serial numbers: an entity's kind and the first four characters of its id, such as `Door a6d1` (Neil). The player is `B-CON`, as the dialogue files name it. Found (Claude) that the text rooms' ids, such as `Test 7,2`, share their first four characters; built: the start of the id is at least four characters, and as many more as tell it from every other id of its kind, as git shortens a commit's hash. Every entity with a body has one, in the game's state, not Core's. Two the same is an error when the state is filled in. A name given in LDtk, so that a door can be Reginald, is for later; Neil may change his mind if derived names make the link between dialogue and entities fragile.
- [x] 1.6 Ownership. Each piece of Core's state has one writer, the controller or runner of its entity, which sets it through Core's view; the game's view of the same piece lets it be read only, and it is changed by asking the owner for an action (§3.3). Further conventions, such as for state Core never sees, later (Neil).
- [x] 1.7 Saving and loading, not built here (Neil). What is known:
  - Doors can be reconstituted by replaying `LastAction`, not by running `Open` or `Close`, but by moving to the stop directly, such as with `MoveAndCollide`. Controllers would take part in reconstituting state, perhaps through a new step in their life cycle, run on the world's second tick, as `Ready` runs once it is built.
  - The world is rebuilt on load.
  - A single class as the source of truth is to be tried first, with a separate save file only if it fails.
  - The state that lives in memory has to be inventoried, and what a beat touches moved into the world's state, so that a beat saved halfway is whole: `Once` and a trigger's being on, yes; a coyote timer, no. Level designers need ways to hook into the world's life cycle.
  - Ids change as levels are edited, and the state's class will change often during development, which will bite the devs hardest.
- [x] 1.8 A player's ability turned off while in use. Found (Claude) that phase turned off while aiming left the game paused for good: the controller skipped the phase, so nothing released it. Decision (Neil): a bug. Built: without the ability, phase counts as let go of with no direction, which cancels an aim in progress.

## 2. Triggers that move with entities

- [x] 2.1 How an entity is talked to. Options (`052 §2.1`): a trigger set off by interacting, which `052` built; an area that moves with any entity's body, through a field on every kind. Neil first: `Target` and `Action` fields on entities, so interacting can do anything, but then whether it starts on, and touch rather than interact, are a trigger's fields again; then a `Trigger` field on every entity, pointing at a trigger that moves with it. Claude: the reference the other way, `MovesWith` on `Trigger`, one field on one definition, which lets an entity have several triggers, such as one to talk to and one for coming near, and leaves a trigger that doesn't move a plain trigger. Decision (Neil): `MovesWith`. The trigger's box in LDtk is where it is when the entity is where it starts, so its size and its place relative to the entity are drawn in the editor.
- [x] 2.2 What it may move with. Built (Claude): any entity with a body but the player, which would always be in it. The world checks it when it is read.
- [x] 2.3 Choosing one when several could be set off by interacting (Neil): one with `MovesWith` before any without; among those, the one whose entity's box is nearest the player's; among those without, the first in the order of the world's controls, as before. Only the chosen one is set off by interacting. The prompt over it is the player's feedback for the choice. A starter idea, which may be bettered later.
- [x] 2.4 The prompt is drawn over the top of the chosen trigger's area where it is now, so it follows a trigger that moves.
- [x] 2.5 A trigger whose entity is removed is never set off again: its area goes with the entity.
- [x] 2.6 A door that hides in the ceiling when it opens takes a trigger that moves with it out of reach, so that it talks only while it is closed. One that should talk once it is open doesn't move with it: such as a two-piece door whose face is on the tracks it runs on (Neil).

## 3. Dialogue about entities

- [x] 3.1 Telling a dialogue which entities it is about. Routes pass nothing along, and Neil doesn't want them to. Decision (Neil): `Dialogue` gets `Entities`, as `Script` has, an array of references to entities with bodies.
- [x] 3.2 What a dialogue file sees. Built (Claude): a state object for each presentation, which Dialogue Manager is given, with `Entities`, each with its `Name`, and `Speaker`, the first of them. So a line can be spoken by `{{Speaker.Name}}`.
- [x] 3.3 Acting from dialogue. Decision (Neil): `do Act(Speaker, "Open")`, which asks the entity for the action, by the name of a `TriggerAction`. Dialogue Manager runs mutations off the world's tick, on Godot's frames, so the request is queued, and the dialogue's own run on the tick starts it, so that the action runs on the tick, as every action does (`043 §2.4`). `do` waits for the action to finish before the dialogue goes on, and `do!` doesn't. An action the entity doesn't respond to fails on the tick, as a failure in the world does.
- [x] 3.4 Avatars by kind (Neil): a line's avatar is the one named for its speaker, as before, or, if the speaker is one of the dialogue's entities, the one named for that entity's kind, so that every door's lines show `Door.png`.
- [x] 3.5 Whether a door says anything once it is open. Neil: a trigger that starts off is a little nicer than conditional dialogue, though either works.
- [ ] 3.6 Speaker names are extracted for translation, but a name filled in when the line is shown isn't. To be talked about near the end of the PR (Neil).

## 4. The beat

- [~] 4.1 The second door of `Entrance`, closed, has a trigger that moves with it, set off by interacting, which runs its dialogue; the dialogue opens the door, by `Act`, when the player asks. Built (Claude) as a first draft of the writing, for Neil to rewrite.
