# Comment style experiments

The rewrite process these experiments led to is now the [`rewrite-prose`](../../.agents/skills/rewrite-prose/SKILL.md) skill.

Tools for the experiments in [decision record 067](../../docs/decision-records/067-comment-style.md), about the style of the comments and docs that agents write.

- [`test-set-1.txt`](test-set-1.txt) and [`test-set-2.txt`](test-set-2.txt): the files whose comments a trial removes and an agent writes again, for pilots 1 and 2. Pilot 2's are the three of pilot 1's that `guideline-2.md`'s examples don't come from.
- [`New-Trial.ps1`](New-Trial.ps1): prepares a trial's workspace and prompt, with or without the repository and a guideline, and optionally runs a fresh Claude Code session on it.
- [`guideline-1.md`](guideline-1.md) and [`guideline-2.md`](guideline-2.md): draft guidelines for writing comments and docs, which a trial can add to `AGENTS.md`. The second is about framing doc comments as contracts, with Neil's rewrites from pilot 1 as examples.
- [`Measure-CommentStyle.ps1`](Measure-CommentStyle.ps1): counts rough markers of the style in C# comments, or in a compiled XML doc file for a baseline.
- [`Measure-Trials.ps1`](Measure-Trials.ps1): scores trials' workspaces, one row each, and checks that each changed only comments.
- [`New-PairSheet.ps1`](New-PairSheet.ps1): writes a blind sheet of choices between two versions of one comment at a time, with its key.
- [`rewrite-scripts/`](rewrite-scripts/): from the rewrite of Core's `Scripts` subsystem, the comments' review notes and Haiku's first draft, and the `architecture.md` facts and review notes.
- [`pilot-1/`](pilot-1/) and [`pilot-2/`](pilot-2/): the experiments' outputs, one directory per trial, with each file saved as `.cs.txt` so that the build ignores it, pilot 1's blind rating sheet, and pilot 2's pair sheet, each with its key.

Workspaces are large, so put them outside the repository, such as in `$env:TEMP`. To score them:

```powershell
.\experiments\comment-style\Measure-Trials.ps1 (Get-ChildItem $env:TEMP\trials -Directory).FullName
```
