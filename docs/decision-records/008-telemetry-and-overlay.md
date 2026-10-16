# 008: Telemetry, and a debug overlay drawn at the window's resolution

## Context

Inciting event (2026-10-16): while tuning jump heights, Neil asked for a debug overlay that can be switched on and off, in the top right, showing measurements for tuning and level design: frame rate and time, the player's position and velocity, and anything else easy to read and relevant. Numbers have a fixed width, so that they don't shift: two decimals, the whole part padded with zeros to four digits, and a `+` for positive values.

Goals: measurements read at a glance while playing; the overlay doesn't constrain how game code is structured.

Closed on 2026-10-16. Outcomes are in [architecture.md](../designs/architecture.md) and [rendering.md](../designs/rendering.md), and so are the unresolved items.

## 1. How the overlay gets its measurements

- [x] 1.1 Pull or push?
  - Pull: the overlay reads the room, the player and its entity. A first design did this. It needed `Entity.AnimationTree` made public, entities named so that the overlay could say what the player stands on, and the overlay to know the animation tree's parameter paths.
  - Push: game code reports measurements to telemetry, and the overlay shows whatever was reported.
  - Decision (Neil): push. Pulling means the overlay knows a lot about the rest of the game, which constrains where abstraction boundaries can be drawn and makes them harder to change. Pushing, like logging, leaves game code free to have whatever boundaries make sense. It also lets code report private state, such as the controller's coyote and thrust ticks, without making it public.
- [x] 1.2 Shape.
  - `Telemetry` is a static class in Core. `Telemetry.Report(Metric, value)` keeps the last value reported for each metric, until it is reported again or `Telemetry.Reset` clears every value. A value reported once, such as a jump's height on landing, stays readable.
  - `Metric` is an enum, so that a misspelt or renamed name doesn't compile (Neil). The overlay shows metrics in the enum's order, so its layout doesn't depend on who reported first.
  - A value is a number, a count, a pair of numbers, a pair of counts, or a flag. Reporting stores it in place, so it costs next to nothing each tick, whether or not anything reads it.
  - The overlay reads the values each frame. It doesn't subscribe to them, so there is no subscription to manage.
  - Not built, until something needs them: channels or categories, history or graphs, events and subscriptions, more than one reader, and naming per entity.
- [x] 1.3 Who reports what.
  - `PlayerController`: the player's feet (the middle of the bottom of its box), velocity, speed and angle, speed relative to its ground, whether it is on the ground, and coyote and thrust ticks left. On landing, the last time in the air: how far the feet rose, how far they moved along X, where they landed relative to where they started, and how many ticks it lasted.
  - `Main`: the tile under the mouse.
  - The overlay itself: frame rate, frame time and physics time per tick, which are the engine's and not the game's.
  - Animation is left out. Every walker steps, so values per walker would share one metric until there is naming per entity.
- [x] 1.4 The take-off point. The engine still reports the floor contact for a tick after a jump, by when the feet have risen about 0.19 tiles. So the controller doesn't move the take-off point while thrust ticks run, which they do from the jump. With that, the reported rise equals the height the tests measured: 2.384 tiles for a tap and 4.550 held, with `JumpSpeed` 12.4 and `JumpThrustTicks` 15.
- [x] 1.5 Tests. `Main` resets telemetry when it loads a room. Tests reset it before a jump and read `AirRise` after landing, in place of their own tracking of the apex, which is deleted (Neil: "I like deleting code").

## 2. Drawing the overlay

- [x] 2.1 Text at 320x240. The project drew everything, UI included, at 320x240 (`003 §1.1`, `§2.1`).
  - System fonts at 8 pixels are unreadable. Consolas and Cascadia Mono draw 0 like 8, Courier New mangles 1, 4 and 7, and Lucida Console's lines run together.
  - A 3x5 pixel font, drawn in code as rectangles, was readable, but the overlay took about 38% of the screen's width and 40% of its height, and it will grow.
- [x] 2.2 Dither. A canvas layer above the colour pass's layer is drawn after the pass and isn't dithered, even at 320x240. So keeping UI out of the colour pass doesn't by itself need a separate viewport. Neil: UI must stay out of it, because even colours taken exactly from the palette are changed by the dither in about half the cases.
- [x] 2.3 Decision (Neil): draw the game into a viewport of its own, and the overlay in the window's viewport at full resolution, in a system font. The reason is more readable text, in a smaller overlay.
  - `LowResView` is a `SubViewportContainer`. Its `SubViewport` holds the game: the room, the camera, the lights and the colour pass. It draws the viewport scaled up by the largest whole number that fits the window, with nearest-neighbour sampling, centred, with black bars. It passes input on to the viewport, scaling the mouse's position to match.
  - The project's stretch mode goes back to the default, `disabled`, so the window's own viewport is the window's size: 1280x960 at the start, as before.
  - `LookSwitches` sets the view's resolution and anti-aliasing instead of the window's content scale. "Low resolution off" draws the game at the window's size.
- [x] 2.4 Result. Mid-jump in the player probe, the game's image is the same as before the change in 63,797 of 63,800 pixels compared, and each scaled-up pixel is a uniform 4x4 block. Frames from the movie writer are now the window's size, 1280x960, not 320x240.
- [ ] 2.5 Left open: the resolution of game UI, when there is some. It stays outside the colour pass; whether it is drawn at the window's resolution or at 320x240 on a layer of its own is undecided.
