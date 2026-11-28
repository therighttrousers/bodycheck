# Comment style experiments

Tools for the experiments in [decision record 067](../../docs/decision-records/067-comment-style.md), about the style of the comments and docs that agents write.

- [`test-set.txt`](test-set.txt): the files whose comments a trial removes and an agent writes again.
- [`New-Trial.ps1`](New-Trial.ps1): prepares a trial's workspace and prompt, under one of the conditions, and optionally runs a fresh Claude Code session on it.
- [`guideline.md`](guideline.md): the draft guideline for writing comments and docs that the `RepoGuideline` condition adds to `AGENTS.md`.
- [`Measure-CommentStyle.ps1`](Measure-CommentStyle.ps1): counts rough markers of the style in C# comments, or in a compiled XML doc file for a baseline.
- [`Measure-Trials.ps1`](Measure-Trials.ps1): scores trials' workspaces, one row each, and checks that each changed only comments.
- [`pilot-1/`](pilot-1/): the first experiment's outputs, one directory per trial, with each file saved as `.cs.txt` so that the build ignores it, and a blind rating sheet with its key.

Workspaces are large, so put them outside the repository, such as in `$env:TEMP`. To score them:

```powershell
.\experiments\comment-style\Measure-Trials.ps1 (Get-ChildItem $env:TEMP\trials -Directory).FullName
```
