# 018: Game time and real time

## Context

Inciting event (2026-10-20): issues #59, "Convert tick-length cooldowns and tick counters to use time", and #68, "Phase leniency: if releasing direction was a few frames before releasing phase button, phase anyway", taken up together in one PR.

Godot runs physics ticks at the same real rate whatever the engine's time scale, and scales the delta it gives each tick. Since the slowdown before a phase's pause (`015 §5.12`), the time scale falls to 0.05 over half a real second, so timers that count ticks, coyote time and jump thrust, run up to 20 times too fast in game time. While the game is paused for a phase, `GameModeManager` steps the controllers at a time scale of 1, so they get a delta of 1/60 then, although no game time passes.

Releasing phase within a tick of letting go of the direction cancels the phase: the direction is read on the release tick only, and with none held, the phase is cancelled (`015 §5.7`). Players release both together, or the direction slightly first, and see phase fail.

## 1. Clocks

- [x] 1.1 Which clock does each timer run on? Rule (Claude, agreed by Neil): a timer that governs the world runs on game time, and one about the player's fingers runs on real time. So coyote time, jump thrust and every cooldown, including phase's, run on game time; the leniency for releasing a phase runs on real time, since it measures the player's hands and runs through the slowdown and the pause alike. No counterexample among cooldowns was found: one that guards against the player's fingers, such as ignoring a second press just after a release, is a leniency, not a cooldown.
- [x] 1.2 How does a controller get real time? Options: (a) both deltas as arguments; (b) a shared source of real time. Decision (Neil): (a). `Apply` takes the game and the real delta; `ApplyPaused` takes the real delta only, since no game time passes then. `WorldNode` gives every tick a real length of one physics tick, which the time scale doesn't change.
- [x] 1.3 Telemetry that counts ticks becomes time, in game seconds, as telemetry prefers SI units: `AirTicks`, `CoyoteTicks` and `ThrustTicks` become `AirTime`, `CoyoteTime` and `ThrustTime`.

## 2. Tuning in ticks

- [x] 2.1 Are durations tuned in seconds or ticks? Neil: ticks at 60 per second, as now. At a time scale of 1, coyote time and thrust must always last the same number of ticks, and adding up deltas can drift. Each is converted to a time half a tick away from a whole number of ticks, the most margin there can be. Since timers count down and run while above zero, a duration of n ticks is n - 1/2 ticks long: it outlasts n ticks and not n + 1. The conversion is the player controller's own, since only it uses it.

## 3. Phase release leniency

- [x] 3.1 What does leniency do? While a phase is aimed, the last direction held still counts for a few ticks after it is let go of. Releasing phase within them phases in that direction. Counting the tick the direction is let go on, so that at 1, releasing both on the same tick phases. The feature is `PlayerPhaseReleaseTicks`, 4 by default, to be checked by playing.
- [ ] 3.2 Letting go of a diagonal rarely lifts both keys on the same tick, and a stick passes neighbouring directions on its way back to the middle, so the phase may go in a direction next to the one meant. Leniency doesn't help there. Open.
