# Dialogue

Relevant decision records:
- [043: Triggers, actions and scripts](../decision-records/043-triggers-and-scripts.md)
- [044: Dialogue](../decision-records/044-dialogue.md)

How dialogue is written, shown and translated. What the characters say is in [story.md](story.md#dialogue); how the code is put together is in [`game/main/`](../../game/main/architecture.md#the-games-modes).

## The backend

Dialogue is written in Dialogue Manager's files, in `game/dialogue/`, and fetched a line at a time through its C# API. Presentation is ours: we use none of its balloons (`044 §1.3`, `§1.4`).

Files are organized by story beat, not by level. Lines shared everywhere, such as "Sleep", go in a common file that others import (`044 §5.1`).

## The game while dialogue plays

Dialogue pauses the game, in a mode of its own; the menu and the map can be opened from it (`044 §2.1`). Dialogue that plays without pausing, a callout, is a separate feature, for when one is needed (`044 §2.2`).

## The text box

The box is along the bottom of the game's viewport, at 320x240, above the colour pass. It shows the speaker, the line, and the line's responses.

- **Typing.** Each line types out, pausing at punctuation, with Dialogue Manager's `DialogueLabel`, which handles its inline tags, such as waits and speed changes, and runs its inline mutations as it reaches them. Jump skips the typing, running the inline mutations left, and once the line has typed out, goes on. The responses show once it has (`044 §1.5`).
- **The font** is Fusion Pixel at 10 pixels, proportional, in its Latin version, imported without antialiasing, hinting, subpixel positioning or system fallback (`044 §4.1`). Its family covers Latin, Greek, Cyrillic, kana, Hangul and CJK. Its licence, the SIL Open Font License, is beside it in `game/assets/fonts/fusion_pixel/`.
- **Laid out from the font's metrics.** The font and its size are a theme's default, `game/assets/fonts/dialogue_theme.tres`, and every line's height comes from the font, never fixed pixels, so that a locale's font sets it (`044 §4.2`). A locale with a font of its own remaps the theme (`044 §4.3`).

## The garble

The Voice's broken lines are garbled when they are shown, not as written: writers and translators write the clean line (`044 §3.1`).

- A line is garbled only if it says so, with a tag in the dialogue file that gives the fraction of its letters and digits replaced, rounded to the nearest glyph: `Voice: ...can you hear me? [#garble=0.75]`. So the player's lines, and any line not marked, are never garbled, and the trigger that presents a dialogue says nothing about it (`044 §3.7`). Dialogue Manager takes tags out of a line's text, so translators don't see them. A `garble` tag whose value isn't a number from 0 to 1, or a second one on a line, fails the dialogue when the line is shown.
- Spaces and punctuation are never garbled, so lines wrap and pause as written (`044 §3.4`).
- Which glyphs are replaced comes from a seed taken from the line's ID: its static ID, which is its translation key, or Dialogue Manager's own if it has none. A line garbles the same way every time, and a more garbled line garbles the same glyphs as a less garbled one, and more.
- **Tofu.** A garbled glyph is a box, the tofu glyph, at U+EB00 in the Private Use Area, from a fallback font of that one glyph, so that it looks the same whatever the locale's font (`044 §3.3`). The locale's font must have no glyph there, which a test checks; Fusion Pixel draws its own up to U+E24F. It is in the Basic Multilingual Plane because Dialogue Manager's C# label counts its text in UTF-16 code units (`044 §3.5`).
- The tofu font is a PNG, `game/assets/fonts/tofu/tofu.png`, imported by Godot as an image font, so that it can be redrawn in any pixel editor (`044 §3.6`). The image is one cell, 6 pixels wide and 14 tall, the font's line. Godot places an image font's glyph with the cell's middle on the baseline, so the box, 5 by 7 like a capital, is drawn in the top half, and the cell's width is its advance.

## Open questions

- The animated garble, to draw attention to it (`044 §3.2`).
- The Voice's avatar: none, or a big question mark as a placeholder (`044 §2.3`).
- Fonts for other locales: Fusion Pixel's other versions, or others such as Ark Pixel or Unifont; licences and quality to be checked (`044 §4.1`). How many lines a box holds in each locale.
- Translation keys: static IDs as keys, with an English translation file, or English text as keys (`044 §5.3`). Speaker names are extracted for translation, but the box doesn't yet translate them (`044 §1.4`).
- Garble tags are checked only when their line is shown. A test could check every dialogue file's tags, as it could check translation keys.
- Inline mutations run as the line types out, but no dialogue has one yet, so it is untested in the game.
