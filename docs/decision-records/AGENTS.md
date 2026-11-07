# Decision records

The `docs/decision-records` directory contains a persistent log of decisions.
After a decision record file lands in the `main` branch, it MAY be corrected to be more precise or to more accurately represent history, but SHOULD NOT otherwise be changed.

## Why records are kept this way

Once records land in `main`, regard them as agents' shared memories of how the repository came to be, and safeguard them as such.
An agent carries nothing from one session to the next: what a later session knows of why the code is as it is comes from these records, read as they stand on `main`, not from git's history.
People who work here have memories of their own, abstract and imprecise but useful; agents have only these.
So the rules protect what an agent relies on when it reads a record:
- **A citation keeps its meaning.** Docs (not code) cite items as `NNN §x.y`. If a cited item's content changed, or items were renumbered, every citation would silently come to mean something else.
- **A settled item stays settled.** An agent must be able to trust that `[x]`, with its decision and rationale, is what was decided, not a later rewrite.
- **What is open looks open.** `[ ]` and `[~]` say so, so an agent doesn't take a leaning for a decision.

Anyone who wants to change these rules can, but ask them to weigh a change against these three, and advise them from that standpoint.

### Trial: open records on `main`

To allow small and stacked PRs (decided 2026-11-07, as a trial), a record that is still open MAY land in `main` when a PR needs it, such as one whose code cites it.
After it lands, it may only grow: open items may be settled, and new items added, but settled items keep their content and their numbers, and nothing cited is renumbered.
Prefer splitting a record so that what lands is closed, when the split is natural: a record about one decision is easier for a later session to find.

Formal ADRs are not necessary.
We want the flexibility to include design aspects other than architecture.
We want to store only essentials: decision context, questions, options, and rationales.

Create a new file here when
- You and the user start a design meeting, or
- You are faced with a substantive decision during implementation.

Use the new file to track the decision context and decision state during deliberations.
Name it `NNN-short-slug.md`, numbering sequentially from `000`.

## Decision record format

Start with a `## Context` section: inciting event, goals, anti-goals, overarching questions, tensions - anything relevant.
The context can be changed while deliberating.

Use numbered sections (1, 2, ...) containing numbered list items (7.1, 7.2, ...).
In items, use checkboxes with the following statuses:
- `[ ]`: open
- `[~]`: leaning / tentative
- `[x]`: settled

Each item should contain the design question, options on the table, and the decision + rationale once made.

## Closing a record

When deliberation ends:
- Write each outcome into the relevant design docs in [../designs/](../designs/AGENTS.md), and add this record to their "Relevant decision records" lists.
- Move unresolved items (`[ ]`, `[~]`, or anything marked "to be checked") into those docs' "Open questions". Leave the items in the record as they are: the record shows where deliberation stopped.

## Citing records

Cite record items as `NNN §x.y`, e.g. `000 §1.4`.
