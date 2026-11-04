# 044: Dialogue

## Context

Inciting event (2026-11-04): breaking down the first slice, "The call and the lock-in" (issue #142), which needs the first parts of the dialogue infrastructure (issue #128): a text box at 320x240, lines in order, and fake choices. Later slices need branching, conditions on the game state, flags set from choices, and a "Sleep" option for every robot.

Goals and requirements (Neil):
- Not our own dialogue system.
- Good support for translation. The game's text is drawn at 320x240, where scripts differ a lot in how many pixels they need: Latin letters can be 8 or 9 pixels tall, Chinese characters at least 16.
- Using it may need coding. Every prospective collaborator codes, or wants to learn.

## 1. The backend

- [x] 1.1 Candidates: Ink, through GodotInk; Yarn Spinner; Dialogue Manager; and Clyde (Neil added the last two). Decision (Neil): Ink is ruled out on translation, which is not part of its model.
- [~] 1.2 What a desk spike found (Claude, 2026-11-04), from the projects' docs, not yet tried:
  - **Yarn Spinner**, the official C# integration for Godot. Lines get IDs, and its tools write CSV string tables, which become Godot `.translation` files read through `Tr()`. Its docs name Godot 4.1 and newer, and recommend 4.5; 4.7 is unconfirmed. A GDScript version is in alpha.
  - **Dialogue Manager**, an addon in GDScript with first-class .NET support. It uses Godot's own translation: static line IDs, `[ID:...]`, extraction through Godot's "Extract Translatable Strings", and CSV or PO. Its releases name Godot 4.7, and it is actively maintained.
  - **Clyde**, an addon in GDScript only, so C# reaches it through Godot's dynamic calls. Translation is a stated requirement of every feature, with tools to export lines for translators. Minimum Godot 4.3; the repository was last updated in August 2025, so its maintenance is in doubt.
- [x] 1.3 Decision (Neil): Dialogue Manager, provisionally, with no hands-on spike: we find out what it costs us while implementing. What to watch for, which a spike would have checked:
  - It builds and runs, and passes CI's checks: no files changed by the build, and none Godot writes left uncommitted.
  - A C# API that is typed, or can be wrapped thinly.
  - We can drive it: ask for the next line when our dialogue mode is ready for it, on our clock, rather than it running itself on Godot's timers or signals.
  - Translation through Godot's `TranslationServer`, with stable line IDs.
  - Its presentation can be ours: our text box, our font, and the garble (§3).

## 2. The game while dialogue plays

- [x] 2.1 Dialogue pauses the game (Neil). A new mode in `GameModeManager`, like phasing: the menu and the map can be opened from it, and closing them goes back to it.
- [x] 2.2 Dialogue that plays without pausing, such as a rocket's "Yeehaw!" or a human's remarks, is a callout: a separate feature, for when one is needed. What would cancel a dialogue in progress is why it waits (Neil).
- [x] 2.3 The Voice has no avatar, or a big question mark as a placeholder (Neil).

## 3. Garbled text

- [x] 3.1 The Voice's broken lines are garbled when they are shown, not as written (Claude, agreed by Neil). Each trigger, or line, says how garbled; the text box replaces that fraction of the glyphs, from a seed taken from the line's ID, so a line garbles the same way every time. Writers and translators write the clean line. They lose control over exactly what shows, which matters little as long as the last of the call is clear (Neil).
- [x] 3.2 The garble can be animated, to draw attention to it (Neil).
- [x] 3.3 The garbled glyph is a box, "tofu", from a fallback font of one glyph at a codepoint in the Private Use Area, so that it looks the same whatever the locale's font (Claude, agreed by Neil). Tofu is not a codepoint but whatever a font draws for one it lacks, and Godot draws its own missing glyph with the hex code inside, and may fall back to a system font first.

## 4. Fonts

- [ ] 4.1 Fonts with good coverage at 320x240. Pixel fonts exist for CJK at 10 to 16 pixels, such as Ark Pixel, and Unifont covers the Basic Multilingual Plane at 16. Licences and quality to be checked.
- [~] 4.2 The text box lays out lines from the font's metrics, never in fixed pixels, so that a locale's font sets its line height (Claude, agreed by Neil). How many lines a box holds in each locale is open.
