# `game/bodycheck/state/`

Relevant decision records:
- [063: World state, and talking to entities](../../../docs/decision-records/063-world-state-and-talking-to-entities.md)
- [065: Persisted game state](../../../docs/decision-records/065-persisted-game-state.md)

## What it is

`GameState`, the game's state, which the game owns: everything the story, the dialogue and the rules read or change. It implements Core's `IGameState`, and keeps more beside it that Core never sees, such as each entity's name (`063 §1.1`). It holds an `EntityState` for every entity, with a body or without, with its id, its kind's name and its name, or, for a kind with state of its own, a subclass, such as `DoorState`, or `SwitchState`, whether a trigger or a once is on, or `PlayerState`, all found by id in one way, by the game and by Core (`063 §1.9`, `065 §1.8`). It also holds which world is being played, each world's current respawn, and the story, a `StoryState`, with a state for each beat, such as `TheCallBeat`, whose properties the dialogue reads and sets, as `Story.TheCall.InnerDoorWontCloseReason`.

It outlives worlds. A new game makes one, and every world built is given it, so that a restart, or a switch to another world and back, keeps what has happened, and a world is built from its data and the state (`065 §1.1`). It is filled in lazily: before a world is built, `Fill` adds the states its entities lack, as its data says they start, and keeps those it has, which serves a new game, a world entered for the first time, a level edited and read again, and a save loaded alike (`065 §1.2`).

## What it is not for

State that only presents something, such as what a dialogue shows now, or state of Core's rules that the game doesn't read.

## Names

Every character is a `CharacterState`, with a name: the Voice, which has no body, and every entity, an `EntityState`, so that any can be made to speak. An entity's name is the one its data gives it, such as `Bridge trigger`, which no other entity in the game has, or else its kind's name, such as `Door`; the player's is `B-CON`, its kind's, and its id is `player` in every world, so that its state is the game's, not a world's (`065 §1.3`). An entity is found by its name only if its data gives it one, or if it is the player, in any world filled in so far; names are taken from a world's data again each time it is filled in, since a level may have been edited (`065 §1.5`, `§1.8`). A dialogue's speakers are found among the Voice and every entity filled in so far. A kind's name, untranslated, is also what its avatar is named for (`063 §1.5`, `§3.4`). A name is translated when it is read, through `Translatable`, so that the game's state holds no translated text and follows the locale ([dialogue.md](../../../docs/designs/dialogue.md#translation), `063 §3.6`).

## Saves

A save is a `SaveData`, a plain record that holds only what the levels' data can't make again: which world is being played, each world's current respawn, the story, its `StoryState` as it is, and an `EntityData` for each entity with state of its own, the player's included, by id; names come from the data. It also holds its format's version, 1, and when the game was started and last saved, which a menu of saves would show. It is JSON, by `System.Text.Json`'s source generator, with the options of every file the game reads, Core's `JsonFiles`, so that a save edited by hand may have comments, and each entity's record says its kind, such as `"Kind": "Door"`, so that one map holds every kind (`065 §3.2`).

- **A state is its record.** Each kind with state of its own has a state class, such as `DoorState`, and a record of what is saved of it, such as `DoorData`, in one file, named for the state. The state holds the record as all it has besides its id and its name, and its properties read and write the record's, so that what is saved and what the game and Core see can't drift apart. An entity's state is made from its configuration as well as its record, so a save can't hold the state itself.
- **The story is saved as it is.** `StoryState` and its beats, such as `TheCallBeat`, are Godot objects, so that the dialogue can read and set them, with nothing in them but what is saved. The save is read and written through `SaveJson`, which leaves out of a Godot object every member the engine declares, such as its `NativeInstance`, and keeps the game's own.
- **A saved type's properties are `{ get; set; }`, with how a new game starts as the initializer,** so that a save written before a property was added reads, with no new version; or `required`, if the start comes from the levels' data, as a switch's does. Never `init` without `required`: the source generator gives such a property that a file lacks its type's default, such as null, not its initializer, and the save then fails to write. `SaveTest` checks every type a save holds for this, and that it holds nothing of the engine's.

- **Loading is lazy.** A `GameState` made from a save makes no entity's state itself: `Fill` makes each from the save's value for its id, if there is one, or else from the data. A saved value no world has filled in yet is kept, and written again as it is, so a save keeps the worlds not entered this session; one whose id is in no level is never used, and stays (`065 §3.3`).
- **`SaveStore`** reads and writes saves, a file for each slot, `{slot}.json`, in one directory, the game's being `user://saves`, with one slot, `default`, for now (`065 §3.4`, `§3.5`). It writes to a temporary file and renames it over the old one, so that a crash while writing leaves the last save whole. A file that can't be read, doesn't parse or is of another version is renamed aside, to `{slot}.bad.json`, and its loading throws, so that a new game doesn't write over it.

## Keeping track of something new

- **A field of a kind that has state:** add it to the kind's record, such as `DoorData`, with how the kind starts as its initializer, and a property on its state that reads and writes it, such as `DoorState.LastAction`: public and settable if the game owns it, or read-only, with an explicit implementation of Core's view to set it, if Core's rules own it. Nothing else changes: saving, loading and old saves follow from the record.
- **A flag of the story:** a property on its beat, such as `TheCallBeat.InnerDoorWontCloseReason`, `{ get; set; }`, with how a new game starts as its initializer. Nothing else changes.
- **A beat of the story:** a file named for it, such as `TheCallBeat.cs`, holding a `RefCounted` with its flags, and a property for it on `StoryState`, `{ get; set; } = new();`.
- **A kind with state of its own:** a file named for its state, holding the state, which extends `EntityState` and overrides `Saved` to give its record, and the record, which extends `EntityData`; a `[JsonDerivedType]` line for the record in `EntityData.cs`, with the name its save gives its kind, which must never change once saves exist; and a case in `GameState.Fill`, which makes the state from its saved record, or else from its configuration.

## Two views of one state

Where Core's rules own a piece, such as a door's `LastAction`, Core's view sets it and the game's reads it only, through an explicit implementation of Core's interface, so that the game changes it only by asking for an action, and GDScript, which sees only public members, can't set it at all (`063 §1.6`). The player's abilities are the game's: they are public and settable, and Core's view reads them only. Visibility says who owns a piece.

## Conventions

What the game's own view holds has types that GDScript and dialogue files can use, so no enums: a door's `LastAction` is `"None"`, `"Open"` or `"Close"` there. Each class's explicit implementation of Core's view goes at the bottom of the class (`063 §1.10`). A kind's state class and its record go in one file, the record first.
