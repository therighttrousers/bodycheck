# Review of the rewrite of src/BodyCheck.Core/Scripts/architecture.md

## Title and "Relevant decision records"

- (4) The list's heading has become a bold line, `**Relevant decision records**`, with a blank line after it. The old doc, and the other architecture docs (such as `src/BodyCheck.Core/architecture.md`), introduce the list with the plain line `Relevant decision records:`, followed directly by the list. The fix must restore that form.
- (3) The doc cites `045 §3.3` (Runners) but the list lacks record 045: `045-control-entities-in-the-world.md`. The old doc lacked it too, but it still must be added.
- (3) The doc cites `054` (Scripts about entities) but the list lacks record 054: `054-scoped-ticks.md`. The old doc lacked it too, but it still must be added.

## Whole doc

- (3, 4) Every citation has lost its backticks: the old doc wrote `` `043 §3.1` ``, as docs/designs/AGENTS.md's "Cross-references" gives (`NNN §x.y`, in code formatting); the new one writes plain 043 §3.1. The fix must put every citation back in backticks.

## What it is

- (2) The old doc says what the control entities are: the small language in which a level wires its story beats. The new one says only that a level uses control entities to wire its beats, losing that they form a small wiring language.
- (3) The old citation (`043 §3.1`, `051 §1.2`) covered the whole description, including what a script is and what it can see and do. The new doc attaches both only to the sentence about runners, and the paragraph about scripts has no citation. `051 §1.2` is about what scripts the run (the C# scripts), and `043 §3.1` creates the subsystem with both the runners and the scripts, so the scripts paragraph must carry them too (at least `051 §1.2`, and `043 §3.1` for the subsystem as a whole).

## Runners: Switched runners

- (3) `OnceRunner`: the new doc attaches `047 §1.2` to the reason the switch turns off at the end of the step rather than the start (that a world rebuilt while the step runs runs it again from its start, instead of leaving a beat half done). `047 §1.2` says nothing of that; it states only that one asked while it runs finishes when it does and one asked after finishes at once. In the old doc the end-not-start reason carried no citation. The fix must keep `047 §1.2` on the behaviour for a second `Run` only, and leave the reason uncited.

## Whole doc: style

These aren't about accuracy, but AGENTS.md asks for them:

- Many paragraphs repeat the same citation on several sentences, such as `051 §2.2, §2.3, 056 §2.3` three times in "Where scripts come from". Cite each record once per paragraph or bullet that it supports.
- About a dozen sentences start with "The reason is that". Give each reason in the sentence that states the rule, with "so that" or "because".
- In "Where scripts come from", a constructor parameter "takes" an entity. Use "receives" for an argument that a parameter is given, as the code's comments do.
