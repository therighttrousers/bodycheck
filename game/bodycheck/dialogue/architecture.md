# `game/bodycheck/dialogue/`

Relevant decision records:
- [044: Dialogue](../../../docs/decision-records/044-dialogue.md)
- [050: The cutscene mode](../../../docs/decision-records/050-cutscene-mode.md)
- [051: The scripted run at the spawn](../../../docs/decision-records/051-scripted-run-at-the-spawn.md)
- [053: Cancelling tasks](../../../docs/decision-records/053-cancelling-tasks.md)
- [063: World state, and talking to entities](../../../docs/decision-records/063-world-state-and-talking-to-entities.md)

## What it is

`DialogueBox`, the game's dialogue presenter, which Core's `IDialoguePresenter` describes, and the text box it shows; `DialogueState`, what a dialogue file sees of the entities it is about; and `GarbledDialogueLabel` and `Garble`, which show a line garbled. See [dialogue.md](../../../docs/designs/dialogue.md) for how dialogue is written, shown and translated.

## What it is not for

The dialogue files, which are content, in `game/assets/dialogue/`; running a dialogue from a level, which is Core's `DialogueRunner`; or the cutscene a dialogue plays in, which is a mode.

## Presenting a dialogue

The dialogue box is given the world, where it finds by id the entities a dialogue asks for actions, and the world's `GameState`. A dialogue file sees that `GameState` and a `DialogueState` as Dialogue Manager's extra game states: the first so that a line can branch on the game's state, and the second with the states of the entities the dialogue is about, as the `GameState` holds them, and `Act`, which queues an action, off the tick, for the presenting method to start on the tick at its next check, and returns a .NET task that completes when the action ends, so that `do` waits for it. Each is a script of its own, since Dialogue Manager finds a C# method only on an object whose script is a C# file (`063 §3.2`, `§3.3`).

- A dialogue names its file without the directory or the extension.
- Presenting a dialogue starts it at once if none is presented, and otherwise queues it, to start as the one before ends, so that two asked for in one tick, such as by a `Fanout`, play one after the other, with a `ReentrantTask` that queues; each is presented by an async method on the world's tick, which ends when the dialogue does (`044 §2.5`, `051 §3.5`, `053 §3.1`).
- Whether a file holds a title, which the world checks when it is built, is whether Dialogue Manager's resource for the file lists it among its cues.
- When a dialogue ends, its task finishes in that tick, so what waits for it resumes then. When the world it presents for is dropped, its method unwinds, and the box drops the dialogue.

## The text box

`DialogueBox` is also the text box, in the game's viewport, on a layer above the colour pass, along its bottom edge, shown in the cutscene mode only, which shows itself only while a dialogue is presented or waits, so that a cutscene with no dialogue, such as a script's, has no box: the speaker's avatar, in a border, if `assets/avatars/` has one by the speaker's name, the speaker, the line, wrapped, and once the line has typed out, the responses, with the chosen one marked. The box itself, its panel and its margins, is a placeholder. Its font and size are the default of the theme `assets/fonts/dialogue_theme.tres`, so the font sets every line's height.

- The line is a `GarbledDialogueLabel`, Dialogue Manager's `DialogueLabel` with its scene's settings set in code, which types the line out, handles its inline tags and runs its inline mutations, and shows it garbled by `Garble`, by the fraction in the line's garble tag, from the line's ID and a step it counts up every half second, which shifts the garble.
- Jump skips the typing, or once the line has typed out, goes on, and up and down choose a response. Whether the line has typed out comes from the label's `FinishedTyping`, not its `IsTyping`, which is false while an inline wait is awaited partway through the line.
- It gets each line from Dialogue Manager, which answers on Godot's frames with a .NET `Task`, a frame or more after it is asked, and waits for it with `ITick.UntilCompleted`, so that the line shows in the first tick after it comes; the player's going on from a line, which comes off the tick, in `_UnhandledInput`, is waited for with `ITick.While` (`044 §1.4`, `053 §3.2`).
- A dialogue file that doesn't exist, a garble tag that isn't valid, or an error in Dialogue Manager, fails the dialogue's task, which fails the world's tick.
- It pauses with the game's viewport, so it gets no input while the game is paused (`050 §4.2`).
