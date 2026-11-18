# 063: World state, and talking to entities

Status: closed.

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
- [x] 1.4 Where state is filled in. Claude: the game, from the world's data, before it hands the state to the world, so that Core only looks state up by id and never makes it. Decision (Neil): so. `IWorldState` shrinks to `Player` and `GetDoor(id)`; finding by name is the game's, for writers. Later `GetEntity<T>(id)` (§1.9).
- [x] 1.5 Names. Entities are machines, with names like serial numbers: an entity's kind and the first four characters of its id, such as `Door a6d1` (Neil). The player is `B-CON`, as the dialogue files name it. Found (Claude) that the text rooms' ids, such as `Test 7,2`, share their first four characters; built: the start of the id is at least four characters, and as many more as tell it from every other id of its kind, as git shortens a commit's hash. Every entity with a body has one, in the game's state, not Core's. Two the same is an error when the state is filled in. A name given in LDtk, so that a door can be Reginald, is for later; Neil may change his mind if derived names make the link between dialogue and entities fragile.
- [x] 1.6 Ownership. Each piece of Core's state has one writer, the controller or runner of its entity, which sets it through Core's view; the game's view of the same piece lets it be read only, and it is changed by asking the owner for an action (§3.3). Further conventions, such as for state Core never sees, later (Neil).
- [x] 1.7 Saving and loading, not built here (Neil). What is known:
  - Doors can be reconstituted by replaying `LastAction`, not by running `Open` or `Close`, but by moving to the stop directly, such as with `MoveAndCollide`. Controllers would take part in reconstituting state, perhaps through a new step in their life cycle, run on the world's second tick, as `Ready` runs once it is built.
  - The world is rebuilt on load.
  - A single class as the source of truth is to be tried first, with a separate save file only if it fails.
  - The state that lives in memory has to be inventoried, and what a beat touches moved into the world's state, so that a beat saved halfway is whole: `Once` and a trigger's being on, yes; a coyote timer, no. Level designers need ways to hook into the world's life cycle.
  - Ids change as levels are edited, and the state's class will change often during development, which will bite the devs hardest.
- [x] 1.8 A player's ability turned off while in use. Found (Claude) that phase turned off while aiming left the game paused for good: the controller skipped the phase, so nothing released it. Decision (Neil): a bug. Built: without the ability, phase counts as let go of with no direction, which cancels an aim in progress.
- [x] 1.9 Every entity's state, found one way. Built (Neil), while making the second door's dialogue conditional: the game's state holds an `EntityState` for every entity with a body, with its id, its kind's name and its name, or a subclass for a kind with state of its own, such as `DoorState`; Core's view has an `IEntityState` that each kind's extends, such as `IDoorState`, and both are found by `GetEntity<T>(id)`. A dialogue sees these states themselves, with no wrapper (§3.2).
- [x] 1.10 Conventions for the state, a start (Neil):
  - What the game's own view holds has types that GDScript and dialogue files can use, so no enums: a door's `LastAction` is a string there.
  - Each class's explicit implementations of Core's view go at the bottom of the class.
  - C#'s visibility says who owns a piece. The player's `CanPhase` is the game's, so the game's class can set it and Core's view only reads it. A door's `LastAction` is Core's, so only the explicit implementation of Core's view can set it, which also hides it from GDScript, which sees only public members. In C#, the game could still cast to Core's view and set it: the convention marks the owner, and doesn't enforce it.
- [x] 1.11 The state's name. Decision (Neil): `GameState`, and Core's view `IGameState`, renamed from `WorldState` and `IWorldState`, which this record names above: so many things are named for the world already, and the game's state is what is saved and outlives any one world, a connotation of persistence that is welcome in Core too.

## 2. Triggers that move with entities

- [x] 2.1 How an entity is talked to. Options (`052 §2.1`): a trigger set off by interacting, which `052` built; an area that moves with any entity's body, through a field on every kind. Neil first: `Target` and `Action` fields on entities, so interacting can do anything, but then whether it starts on, and touch rather than interact, are a trigger's fields again; then a `Trigger` field on every entity, pointing at a trigger that moves with it. Claude: the reference the other way, `MovesWith` on `Trigger`, one field on one definition, which lets an entity have several triggers, such as one to talk to and one for coming near, and leaves a trigger that doesn't move a plain trigger. Decision (Neil): `MovesWith`. The trigger's box in LDtk is where it is when the entity is where it starts, so its size and its place relative to the entity are drawn in the editor.
- [x] 2.2 What it may move with. Built (Claude): any entity with a body but the player, which would always be in it. The world checks it when it is read.
- [x] 2.3 Choosing one when several could be set off by interacting (Neil): one with `MovesWith` before any without; among those, the one whose entity's box is nearest the player's; among those without, the first in the order of the world's controls, as before. Only the chosen one is set off by interacting. The prompt over it is the player's feedback for the choice. A starter idea, which may be bettered later.
- [x] 2.4 The prompt is drawn over the top of the chosen trigger's area where it is now, so it follows a trigger that moves.
- [x] 2.5 A trigger whose entity is removed is never set off again: its area goes with the entity.
- [x] 2.6 A door that hides in the ceiling when it opens takes a trigger that moves with it out of reach, so that it talks only while it is closed. One that should talk once it is open doesn't move with it: such as a two-piece door whose face is on the tracks it runs on (Neil).

## 3. Dialogue about entities

- [x] 3.1 Telling a dialogue which entities it is about. Routes pass nothing along, and Neil doesn't want them to. Decision (Neil): `Dialogue` gets `Entities`, as `Script` has, an array of references to entities with bodies.
- [x] 3.2 What a dialogue file sees. Built (Claude): a state object for each presentation, which Dialogue Manager is given, with `Entities`, each with its `Name`, and `Speaker`, the first of them. So a line can be spoken by `{{Speaker.Name}}`. Changed (Neil): the entities are their states in the world's state (§1.9), and Dialogue Manager is given the world's state too, so that a line can branch on it, as on `Speaker.LastAction`, and name the player, as `{{Player.Name}}`. To act, the presenter finds the entity by its state's id in the world, which scripts see by id.
- [x] 3.3 Acting from dialogue. Decision (Neil): `do Act(Speaker, "Open")`, which asks the entity for the action, by the name of a `TriggerAction`. Dialogue Manager runs mutations off the world's tick, on Godot's frames, so the request is queued, and the dialogue's own run on the tick starts it, so that the action runs on the tick, as every action does (`043 §2.4`). `do` waits for the action to finish before the dialogue goes on, and `do!` doesn't. An action the entity doesn't respond to fails on the tick, as a failure in the world does.
- [x] 3.4 Avatars by kind (Neil): a line's avatar is the one named for its speaker, as before, or, if the speaker is one of the dialogue's entities, the one named for that entity's kind, so that every door's lines show `Door.png`.
- [x] 3.5 Whether a door says anything once it is open. Neil: a trigger that starts off is a little nicer than conditional dialogue, though either works. Built (Neil) for the second door: conditional dialogue, on its `LastAction`.
- [x] 3.6 Translating names. Found (Claude): Dialogue Manager extracts a line's character as written, with the context `dialogue`, so `{{Speaker.Name}}` is extracted as itself, and translates no character at run time; the box's label translates what it shows, without that context, so it wouldn't find a static name's translation either. Names in a line's text are filled in after the line is translated, and kind names are in C#, which Godot's POT generation doesn't read. Discussed (Claude, Neil):
  - Every speaker is a state with a `Name` that is translated when it is read, and a `KindName`, its key before translation, which its avatar is named for: entities, and characters without a body, such as the Voice, which the game's state holds too (Claude; Neil first, `GameState.VoiceName`, since the Voice's name will turn up outside dialogue). Dialogue files name every speaker through its state, as `{{Voice.Name}}:`, and the box finds a line's avatar by the state whose `Name` is the line's character. Dialogue Manager's extraction of characters is then turned off.
  - `B-CON` is translated (Neil): in English it sounds like "Beacon", and a translator may make a pun of their own.
  - Word order (Neil: learn to do it properly now): a name is made from a translatable pattern with named placeholders, `{kind} {serial}`, with a comment for translators, so that a translator can reorder them or add words. A string's context, the second argument to `Tr`, tells apart the same English word in different roles.
  - Grammar around a name, such as case or gender, which Godot can't vary: writers put a name where it doesn't inflect, such as as the speaker, a subject or an address, and translators rephrase whole lines around it.
  - A name given in LDtk, such as Reginald, is translated too, where a translator sees fit (Neil). It is data, so it reaches the POT another way, such as by the reader or a test.
  - Strings in C# are marked with a no-op, as gettext's `N_()`, which returns its argument, and found by a small translation parser plugin for `.cs` files, so that nothing is kept by hand.
  - The Interact prompt is the up arrow's glyph, an image that will one day come from the input's binding, and the word, a label in the game's font translated in code, not an image a translator would have to redraw (Neil, Claude). Godot's per-locale remaps are for art that is text, such as a logo.
  - Built in this PR, so that what is written from now on needs no rework (Neil: something minimal): every speaker a character state, with the Voice in the game's state and every dialogue file naming speakers through states; names translated when read, from the pattern; `Translatable.Mark` and `Translatable.Translate` for text in C#; Dialogue Manager's extraction of characters turned off; avatars found by the speaker's state. Left for later, since it costs no more then: the parser plugin, given names in the POT, and the prompt.

## 4. The beat

- [x] 4.1 The second door of `Entrance`, closed, has a trigger that moves with it, set off by interacting, which runs its dialogue; the dialogue opens the door, by `Act`, when the player asks. Built (Claude) with a first draft of the writing; rewritten (Neil), with lines for once it is open.
