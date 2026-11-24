# `game/bodycheck/state/`

Relevant decision records:
- [063: World state, and talking to entities](../../../docs/decision-records/063-world-state-and-talking-to-entities.md)
- [065: Persisted game state](../../../docs/decision-records/065-persisted-game-state.md)

## What it is

`GameState`, the game's state, which the game owns: everything the story, the dialogue and the rules read or change. It implements Core's `IGameState`, and keeps more beside it that Core never sees, such as each entity's name (`063 §1.1`). It holds an `EntityState` for every entity, with a body or without, with its id, its kind's name and its name, or, for a kind with state of its own, a subclass, such as `DoorState`, or `SwitchState`, whether a trigger or a once is on, all found by id in one way, by the game and by Core (`063 §1.9`, `065 §1.8`). It also holds which world is being played, and each world's current respawn.

It outlives worlds. A new game makes one, and every world built is given it, so that a restart, or a switch to another world and back, keeps what has happened, and a world is built from its data and the state (`065 §1.1`). It is filled in lazily: before a world is built, `Fill` adds the states its entities lack, as its data says they start, and keeps those it has, which serves a new game, a world entered for the first time and a level edited and read again alike (`065 §1.2`). Nothing is saved yet.

## What it is not for

State that only presents something, such as what a dialogue shows now, or state of Core's rules that the game doesn't read.

## Names

Every character is a `CharacterState`, with a name: the Voice, which has no body, and every entity, an `EntityState`, so that any can be made to speak. An entity's name is the one its data gives it, such as `Bridge trigger`, which no other entity in the game has, or else its kind's name, such as `Door`; the player's is `B-CON`, its kind's, and its id is `player` in every world, so that its state is the game's, not a world's (`065 §1.3`). An entity is found by its name only if its data gives it one, or if it is the player, in any world filled in so far; names are taken from a world's data again each time it is filled in, since a level may have been edited (`065 §1.5`, `§1.8`). A dialogue's speakers are found among the Voice and every entity filled in so far. A kind's name, untranslated, is also what its avatar is named for (`063 §1.5`, `§3.4`). A name is translated when it is read, through `Translatable`, so that the game's state holds no translated text and follows the locale ([dialogue.md](../../../docs/designs/dialogue.md#translation), `063 §3.6`).

## Two views of one state

Where Core's rules own a piece, such as a door's `LastAction`, Core's view sets it and the game's reads it only, through an explicit implementation of Core's interface, so that the game changes it only by asking for an action, and GDScript, which sees only public members, can't set it at all (`063 §1.6`). The player's abilities are the game's: they are public and settable, and Core's view reads them only. Visibility says who owns a piece.

## Conventions

What the game's own view holds has types that GDScript and dialogue files can use, so no enums: a door's `LastAction` is `"None"`, `"Open"` or `"Close"` there. Each class's explicit implementation of Core's view goes at the bottom of the class (`063 §1.10`).
