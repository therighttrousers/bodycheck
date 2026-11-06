# Dialogue

Relevant decision records:
- [043: Triggers, actions and scripts](../decision-records/043-triggers-and-scripts.md)
- [044: Dialogue](../decision-records/044-dialogue.md)
- [050: The cutscene mode](../decision-records/050-cutscene-mode.md)

How dialogue is written, shown and translated. What the characters say is in [story.md](story.md#dialogue); how the code is put together is in [`game/main/`](../../game/main/architecture.md#the-games-modes).

## The backend

Dialogue is written in Dialogue Manager's files, in `game/dialogue/`, and fetched a line at a time through its C# API. Presentation is ours: we use none of its balloons (`044 §1.3`, `§1.4`).

Files are organized by story beat, not by level. Lines shared everywhere, such as "Sleep", go in a common file that others import (`044 §5.1`).

A level presents dialogue with a `Dialogue` control entity, which names a file, without its directory or extension, and a title in it, as strings. Each time it is asked, it presents that title, after any dialogue already waiting, and finishes when the dialogue ends. A world checks, when it is built, that each of its dialogues' files exists and holds its title, and fails to build if not; a test builds every world, so a wrong name fails in CI, not in play (`044 §5.2`).

## The game while dialogue plays

Dialogue plays in a cutscene, a mode of its own, which doesn't pause the game: the world runs, so the camera, animations and shaders keep going, and the player's controller gets no input, so the player lands, comes to rest and stands. The world goes on independently of the player's story, so a dialogue is placed where nothing in the world can harm the player. Sound isn't muffled, so that music could cue a dialogue. The menu and the map can be opened from it (`044 §2.1`, `050 §1.2`, `§1.3`, `§3.3`). A dialogue is presented at once: the player may land under the box (`050 §1.4`). Dialogue that plays without pausing, a callout, is a separate feature, for when one is needed (`044 §2.2`).

## The text box

The box is along the bottom of the game's viewport, at 320x240, above the colour pass. It shows the speaker's avatar, the speaker, the line, and the line's responses.

- **Avatars** are 48 by 48 pixels, in a light border on a dark background, beside the speaker's name and the line. Each is a PNG in `game/assets/avatars/`, named for the speaker as the dialogue files name them, before translation, such as `Voice.png`; a speaker without one has none, and the line takes the box's width. The player is `B-CON` in the dialogue files. The Voice's avatar is a big question mark, a placeholder (`044 §2.3`).
- **Typing.** Each line types out, pausing at punctuation, with Dialogue Manager's `DialogueLabel`, which handles its inline tags, such as waits and speed changes, and runs its inline mutations as it reaches them. Jump skips the typing, running the inline mutations left, and once the line has typed out, goes on. The responses show once it has (`044 §1.5`).
- **The font** is Fusion Pixel at 10 pixels, proportional, in its Latin version, imported without antialiasing, hinting, subpixel positioning or system fallback (`044 §4.1`). Its family covers Latin, Greek, Cyrillic, kana, Hangul and CJK. Its licence, the SIL Open Font License, is beside it in `game/assets/fonts/fusion_pixel/`.
- **Fake choices** are writing, not code: responses whose branches meet again, as in the debug dialogue (`044 §5.4`).
- **Laid out from the font's metrics.** The font and its size are a theme's default, `game/assets/fonts/dialogue_theme.tres`, and every line's height comes from the font, never fixed pixels, so that a locale's font sets it (`044 §4.2`). A locale with a font of its own remaps the theme (`044 §4.3`).

## The garble

The Voice's broken lines are garbled when they are shown, not as written: writers and translators write the clean line (`044 §3.1`).

- A line is garbled only if it says so, with a tag in the dialogue file that gives the fraction of its letters and digits replaced, rounded to the nearest glyph: `Voice: ...can you hear me? [#garble=0.75]`. So the player's lines, and any line not marked, are never garbled, and the trigger that presents a dialogue says nothing about it (`044 §3.7`). Dialogue Manager takes tags out of a line's text, so translators don't see them. A `garble` tag whose value isn't a number from 0 to 1, or a second one on a line, fails the dialogue when the line is shown.
- Spaces and punctuation are never garbled, so lines wrap and pause as written (`044 §3.4`).
- Which glyphs are replaced comes from a seed taken from the line's ID: its static ID, which is its translation key, or Dialogue Manager's own if it has none, and a step. A line garbles the same way every time, and a more garbled line garbles the same glyphs as a less garbled one, and more.
- **The garble shifts**, to draw attention to it: every half second the line is shown, the step goes up, and other glyphs are garbled, the same number of them. So a patient player can piece the whole line together, and the line shifts the same way each time it is shown (`044 §3.2`).
- **Tofu.** A garbled glyph is a box, the tofu glyph, at U+EB00 in the Private Use Area, from a fallback font of that one glyph, so that it looks the same whatever the locale's font (`044 §3.3`). The locale's font must have no glyph there, which a test checks; Fusion Pixel draws its own up to U+E24F. It is in the Basic Multilingual Plane because Dialogue Manager's C# label counts its text in UTF-16 code units (`044 §3.5`).
- The tofu font is a PNG, `game/assets/fonts/tofu/tofu.png`, imported by Godot as an image font, so that it can be redrawn in any pixel editor (`044 §3.6`). The image is one cell, 6 pixels wide and 14 tall, the font's line. Godot places an image font's glyph with the cell's middle on the baseline, so the box, 5 by 7 like a capital, is drawn in the top half, and the cell's width is its advance.

## Open questions

- The Voice's real avatar, if it gets one.
- Fonts for other locales: Fusion Pixel's other versions, or others such as Ark Pixel or Unifont; licences and quality to be checked (`044 §4.1`). How many lines a box holds in each locale.
- Translation keys: static IDs as keys, with an English translation file, or English text as keys (`044 §5.3`). Speaker names are extracted for translation, but the box doesn't yet translate them (`044 §1.4`).
- Garble tags are checked only when their line is shown. A test could check every dialogue file's tags, as it could check translation keys.
- Inline mutations run as the line types out, but no dialogue has one yet, so it is untested in the game.
