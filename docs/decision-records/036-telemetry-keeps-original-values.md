# 036: Telemetry keeps the values reported to it

## Context

Inciting event (2026-10-30): issue #109. Telemetry turned every value into a pair of floats with a kind (`008 §1.2`), which has already caused a bug: a `ulong` was cast to a `float` where an `int` was meant, so `GetInt` always returned null, and nothing said so.

Goals:
- A value comes back as what was reported.
- A read that can't work fails loudly.
- Code that reads telemetry is easy to write.

Closed on 2026-10-30. Outcomes are in [architecture.md](../designs/architecture.md).

## 1. What telemetry stores

- [x] 1.1 Each metric keeps the value reported for it, as an `object?`, instead of a kind and a pair of floats. There is one generic `Report<T>`, for any value type, in place of an overload per type. Decision (Neil, #109).
- [x] 1.2 Cost. Reporting now boxes each value, a few small allocations a tick, where it stored the value in place (`008 §1.2`). Decision (Claude): accept it. It is negligible, and avoiding it would take a generic slot for each metric, which is more code than the kinds it replaces.

## 2. Reading

- [x] 2.1 What a read does when it can't return a value. `008 §1.2` returned null, both for a metric that hadn't been reported and for one reported as another type. That is what hid the bug, and it made every reader deal with null. Decision (Neil): it throws: "I'm not a big fan of silent failures", and code that uses telemetry is easier to write. So `Get<T>(metric)` returns a `T`, not a `T?`.
- [x] 2.2 Metrics that may not have been reported yet. The debug overlay reads them from the first frame, and throwing there would crash it. Decision (Claude): `Get(metric, fallback)`, which returns the fallback if nothing has been reported, and still throws on the wrong type. So `Get(Metric.GroundSpeed, 0)`, an `int` for a `float` metric, fails loudly.
- [x] 2.3 `Get(metric)`, not generic, returns the `object?`, for code that takes any type: the overlay's list of the remaining metrics, which shows `?` for a value it has no format for or that hasn't been reported.

## 3. Corner correction time

- [x] 3.1 `CornerCorrectionTime` is in seconds, not milliseconds, since telemetry prefers the game's equivalents of SI units (`008 §1.2`) (Neil).
