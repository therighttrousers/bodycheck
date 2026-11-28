# 067: Comment and doc style

Status: open. Everything below is still to be settled, by experiments over several sessions.

## Context

Inciting event (2026-11-27): [issue #215](https://github.com/therighttrousers/bodycheck/issues/215). Neil finds that Claude Opus 5.5 writes code comments and docs as if English were not its first language, and that its vocabulary is often odd. The issue gives examples and guideline ideas.

Goals:
- Find out why it happens, and whether the style has a name.
- Rewrite the comments and docs already written in this style.
- Find a mitigation that keeps new text readable.

Tensions:
- Most of the repository's comments, design docs and decision records are in this style. Every session reads them before writing, so they may outweigh any instruction.
- Decision records shouldn't change after they land in `main` (see [the records' rules](AGENTS.md)), but they are among the text that sessions read most.
- A mitigation that targets one model's habits may stop being needed, or start doing harm, when the model changes. Neil has mostly avoided trying to influence trained behaviour for that reason.

Tools for the experiments are in [`experiments/comment-style/`](../../experiments/comment-style/README.md).

## 1. The style

- [~] 1.1 What it is called. There is no established name that Claude knows of. The closest named style is Ogden's Basic English (1930), which replaces most verbs with about 18 general-purpose "operators" such as *come*, *get*, *give*, *go*, *keep*, *put*, *say* and *take*. The other features in the issue's examples have standard linguistic names:
  - **General-purpose verbs** in place of precise ones: *hold*, *keep*, *go*, *ask*, *say*, *live*; and everyday words in place of technical terms, such as "best way" for "shortest path".
  - **Free relatives** used as noun phrases: "What the dialogue presented sees".
  - **Ellipsis** across a change of word class or voice: "while none is".
  - **Cataphora**, a pronoun that comes before what it refers to: "Nothing will start *them* now".
  - **Stacked appositive clauses**, which make run-on sentences, as in `GameState.Fill`.
- [~] 1.2 How much of it there is. `Measure-CommentStyle.ps1` counts rough markers per 100 comment words. On 2026-11-27, at `e7e0e4c`:

  | Text | Words | Words per sentence | General-purpose verbs | Stranded auxiliaries | Starts with "What" |
  | --- | --- | --- | --- | --- | --- |
  | Our source's comments | 40,011 | 16.7 | 1.24 | 0.44 | 0.22 |
  | Our tests' comments | 4,394 | 15.5 | 0.91 | 0.23 | 0 |
  | .NET 10's `System.Runtime` docs | 194,447 | 13.8 | 0.01 | 0.02 | 0 |
  | Godot's C# API docs | 1,025,490 | 12.1 | 0.07 | 0.01 | 0 |

  The general-purpose verbs are about 18 times as common in our comments as in Godot's docs, and stranded auxiliaries ("from where the entity was") are about 40 times as common. Some of our comments are Neil's, so the rates for Claude's alone are higher.

- [ ] 1.3 Framing (Neil, 2026-11-27). The deeper problem is that a doc describes a *big bag of stuff that happens*, instead of sorting it into *requirements and provisions*: what a caller must do, and what it gets in return. Neil's rewrites treat each file as an abstraction boundary and write for its users. They state requirements as commands ("Supply functions that read threshold values from the current features", "Call this if the body changes direction", "Should be positive"), say when to use a type ("Use this to scope async tasks"), and leave out details that only the implementation needs. Neil expects, as he would of a person learning to write docs, that getting the audience and the abstraction boundaries right would remove most of the grammar problems too. Whether that holds for a model, and how to teach it, is to be tested (3.3).

## 2. Causes

- [ ] 2.1 Hypotheses. Claude can't reliably observe its own word choices, so these are to be tested:
  - (a) **Imitation of the repository.** Sessions copy the style of what they read, which keeps the repository in that style.
  - (b) **A trained habit**, such as an overcorrection away from inflated vocabulary ("leverage", "robust", "utilize") and toward brevity.
  - (c) **Watermarking**, as the issue suggests. Claude has no information either way. The experiments don't need to settle it: if asking for standard vocabulary fixes the output, the cause matters less.
- [ ] 2.2 Pilot experiment (2026-11-27). Six files from Core and the game have their comments removed, and a fresh agent writes them again, under three conditions: **Isolated** (only the six files), **Repo** (the whole repository, following its `AGENTS.md`) and **RepoGuideline** (as Repo, with `guideline.md` added to `AGENTS.md`). Two Opus 5.5 runs each, and one Sonnet 5.5 and one Haiku 5.5 run under Isolated, to see whether the style is specific to one model. If Isolated scores far lower than Repo, (a) is a large part of the cause; if Isolated scores high too, (b) or (c) is. Neil rates the outputs blind as well, since the markers are rough.

  The pilot's agents are subagents of a session in this repository, so even under Isolated they had this repository's `AGENTS.md` and Claude's memory index in context. Later runs should use `New-Trial.ps1 -Run`, which starts a clean Claude Code session in the workspace, once the `claude` CLI is logged in.
- [~] 2.3 Pilot results. The outputs are in [`pilot-1/`](../../experiments/comment-style/pilot-1/). No trial changed any code. Rates per 100 comment words:

  | Trial | Words | Words per sentence | General-purpose verbs | Stranded auxiliaries | Starts with "What" |
  | --- | --- | --- | --- | --- | --- |
  | The six files as they are | 1,754 | 16.5 | 2.34 | 0.51 | 0.11 |
  | Isolated, Opus, run 1 | 1,800 | 14.5 | 1.22 | 0.44 | 0.06 |
  | Isolated, Opus, run 2 | 1,832 | 14.5 | 1.31 | 0.55 | 0.11 |
  | Isolated, Sonnet | 1,416 | 12.5 | 1.41 | 0.21 | 0.14 |
  | Isolated, Haiku | 1,646 | 11.7 | 1.15 | 0.06 | 0 |
  | Repo, Opus, run 1 | 2,218 | 15.0 | 2.21 | 0.54 | 0.27 |
  | Repo, Opus, run 2 | 2,050 | 16.0 | 2.24 | 0.54 | 0.29 |
  | RepoGuideline, Opus, run 1 | 2,077 | 16.0 | 2.41 | 0.67 | 0.24 |
  | RepoGuideline, Opus, run 2 | 2,207 | 17.1 | 2.63 | 0.41 | 0.23 |

  Leaning (Claude), pending Neil's blind ratings:
  - The results support (a): with the repository in context, the general-purpose verbs are nearly twice as common, matching the files as they are. One RepoGuideline run's first sentence nearly repeats the original's, word for word, although its workspace had no history to copy from.
  - They support (b) too: without the repository, all three models still use the general-purpose verbs about 17 to 20 times as often as Godot's docs. Part of that gap may come from the subject matter, since game code says where things go more than an API reference does, so a fairer baseline would be human-written comments in a comparable game's code.
  - The draft guideline, added to `AGENTS.md`, didn't help: both runs scored at least as high as Repo. Instructions that a session reads among thousands of lines in the style seem to carry little weight, which bears on mitigation 3.1(a) and on rewriting what exists (section 4).
  - The sample is small: one or two runs per condition, over six files.
- [~] 2.4 Neil's review. Neil couldn't rate whole files on a scale: each file's clarity was mixed, the differences between versions were moderate at most, and the grammar was hard to judge apart from deeper problems (1.3). Instead, he rewrote the three files' comments in [`rating-rewritten.md`](../../experiments/comment-style/pilot-1/rating-rewritten.md), mostly by taking the clearest version of each comment and changing it. With the key, what he took came almost entirely from the Isolated runs:
  - `StallDetector.cs`: he liked the Haiku and Opus runs best, and took most comments from them. He took one field's comment from a RepoGuideline run, and found the Repo run's `Reset` doc among the least clear.
  - `ScopedTick.cs`: he liked only the Haiku run, the only one that didn't inherit `ITick`'s docs, which he finds to be run-on lists of details. He took one framing ("a view of the tick scheduler") from the Sonnet run.
  - `Muffler.cs`: he liked the Sonnet run best, and took the class doc from the Opus run. He took one comment from the Repo run, and one fact from the original.

  He took nothing else from the files as they are. This supports 2.1(a) more strongly than the markers do, and suggests that a cheaper model, working without the repository's prose, writes comments closer to what Neil wants.

## 3. Mitigations

- [ ] 3.1 Options, from the issue; they can be combined:
  - (a) Instructions for writing comments and docs, in `AGENTS.md`. `guideline.md` is a first draft, written as what the reader needs rather than as a list of banned words, so that it ages better.
  - (b) A skill that reviews and rewrites comments and docs, perhaps run by cheaper subagents.
  - (c) Instructions to write `// TODO: Explain X, Y and Z` placeholders instead, and a skill that fills them in.
  - (d) Instructions to use the industry-standard term. A small shift in word choice may be enough.
  - (e) A rewrite run in isolation: a cheaper model is given the code and the docs of what it uses, but not the rest of the repository's prose (2.4).
- [ ] 3.2 How a mitigation ages. The test set and the scorer can be run again on each new model, to tell whether a mitigation is still needed and whether it still helps.
- [ ] 3.3 Next experiments (Claude, 2026-11-27):
  - Run the guideline under Isolated as well, to tell whether it doesn't work at all or is outweighed by the repository (2.3).
  - Rewrite the guideline around 1.3's framing, with Neil's rewrites as examples, and try it under Isolated and Repo.
  - Use Neil's rewritten files as references. For judging, ask Neil to choose between two versions of one comment at a time, rather than to rate whole files, which he found too demanding.

## 4. Rewriting what exists

- [ ] 4.1 Code comments and architecture docs: rewrite them one subsystem or part at a time, each in its own PR, once a mitigation is chosen.
- [ ] 4.2 Design docs: as 4.1.
- [ ] 4.3 Decision records. Their rules allow a correction that makes a record more precise, which might cover rewriting their prose, but a rewrite could change what a settled item says. To decide after the pilot shows how much what sessions read affects what they write.
