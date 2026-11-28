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
- [ ] 2.2 Pilot experiment (2026-11-27). Six files from Core and the game have their comments removed, and a fresh agent writes them again, under three conditions: **Isolated** (only the six files), **Repo** (the whole repository, following its `AGENTS.md`) and **RepoGuideline** (as Repo, with `guideline-1.md` added to `AGENTS.md`). Two Opus 5.5 runs each, and one Sonnet 5.5 and one Haiku 5.5 run under Isolated, to see whether the style is specific to one model. If Isolated scores far lower than Repo, (a) is a large part of the cause; if Isolated scores high too, (b) or (c) is. Neil rates the outputs blind as well, since the markers are rough.

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

- [~] 2.5 Drift (Neil, 2026-11-27). The style seems to be mostly drift: each session imitates what earlier ones wrote, so a style compounds once it is common in the repository. Claude: the Isolated runs still use the general-purpose verbs far more than Godot's docs do, so drift seems to amplify a smaller trained tendency, 2.1(b), rather than to start from nothing.

## 3. Mitigations

- [ ] 3.1 Options, from the issue; they can be combined:
  - (a) Instructions for writing comments and docs, in `AGENTS.md`. `guideline-1.md` is a first draft, written as what the reader needs rather than as a list of banned words, so that it ages better.
  - (b) A skill that reviews and rewrites comments and docs, perhaps run by cheaper subagents.
  - (c) Instructions to write `// TODO: Explain X, Y and Z` placeholders instead, and a skill that fills them in.
  - (d) Instructions to use the industry-standard term. A small shift in word choice may be enough.
  - (e) A rewrite run in isolation: a cheaper model is given the code and the docs of what it uses, but not the rest of the repository's prose (2.4).
- [ ] 3.2 How a mitigation ages. The test set and the scorer can be run again on each new model, to tell whether a mitigation is still needed and whether it still helps.
- [ ] 3.3 Next experiments (Claude, 2026-11-27):
  - Run the guideline under Isolated as well, to tell whether it doesn't work at all or is outweighed by the repository (2.3).
  - Rewrite the guideline around 1.3's framing, with Neil's rewrites as examples, and try it under Isolated and Repo.
  - Use Neil's rewritten files as references. For judging, ask Neil to choose between two versions of one comment at a time, rather than to rate whole files, which he found too demanding.
- [~] 3.4 Pilot 2 (2026-11-27). [`guideline-2.md`](../../experiments/comment-style/guideline-2.md) frames doc comments as contracts (1.3), with Neil's rewrites of `StallDetector.cs`, `ScopedTick.cs` and `Muffler.cs` as examples. So the trials rewrote the other three files of pilot 1: `PlatformController.cs`, `PatrolBehaviour.cs` and `GameState.cs`. With a guideline, the Isolated workspace has an `AGENTS.md` that contains only the guideline. Pilot 1's trials, scored on the same three files, are the baselines. The outputs are in [`pilot-2/`](../../experiments/comment-style/pilot-2/). No trial changed any code, except one that deleted a space. Rates per 100 comment words:

  | Trial | Words | Words per sentence | General-purpose verbs | Stranded auxiliaries | Starts with "What" |
  | --- | --- | --- | --- | --- | --- |
  | The three files as they are | 1,359 | 16.4 | 2.58 | 0.59 | 0.15 |
  | Pilot 1: Isolated, Opus, runs 1 and 2 | 1,361, 1,324 | 14.5, 14.5 | 1.10, 1.28 | 0.51, 0.60 | 0.07, 0.15 |
  | Pilot 1: Isolated, Sonnet | 1,031 | 12.3 | 1.36 | 0.10 | 0.10 |
  | Pilot 1: Isolated, Haiku | 1,160 | 11.7 | 1.29 | 0 | 0 |
  | Pilot 1: Repo, Opus, runs 1 and 2 | 1,615, 1,532 | 14.3, 16.0 | 2.17, 2.48 | 0.68, 0.59 | 0.25, 0.26 |
  | Isolated, guideline 1, Opus, runs 1 and 2 | 1,404, 1,204 | 14.3, 14.3 | 0.50, 0.66 | 0.07, 0 | 0, 0 |
  | Isolated, guideline 2, Opus, runs 1 and 2 | 1,101, 1,111 | 12.8, 11.8 | 0.36, 0.27 | 0, 0.18 | 0, 0 |
  | Isolated, guideline 2, Sonnet | 1,312 | 13.1 | 2.06 | 0.38 | 0.08 |
  | Isolated, guideline 2, Haiku | 1,422 | 11.0 | 0.91 | 0.14 | 0.07 |
  | Repo, guideline 2, Opus, runs 1 and 2 | 1,522, 1,597 | 16.5, 16.8 | 2.63, 2.32 | 0.53, 0.56 | 0.26, 0.25 |

  Leaning (Claude), pending Neil's choices in [`pairs.md`](../../experiments/comment-style/pilot-2/pairs.md):
  - Either guideline works for Opus without the repository: it halves the general-purpose verbs or better, and nearly removes the stranded auxiliaries and the free relatives. Guideline 2 scores lowest, and its comments are shortest.
  - Neither works with the repository: Repo with guideline 2 scores as Repo did without one. So the guideline isn't too weak; the repository's prose outweighs it. This answers 3.3's first question, and makes rewriting what exists (section 4), or rewriting in isolation (3.1(e)), more important than instructions alone.
  - Sonnet scored worse with guideline 2 than without. Many of its general-purpose verbs are in their literal, physical sense, such as a spring that "holds the platform on the path's line", which the marker can't tell apart. One Sonnet file also repeats five comment lines. One run each is too few to conclude anything about Sonnet or Haiku.

  The pair sheet has 33 pairs, shuffled, each of one comment in two versions: guideline 2 against no guideline, under Isolated (8) and Repo (8); guideline 2 against guideline 1 (6); Opus against Haiku, both with guideline 2 (6); and guideline 2 against the original (5).
- [~] 3.5 Neil's choices in pilot 2's pairs. Choosing between two versions of one comment worked where rating whole files didn't. Tallied against the key:

  | Comparison | Wins | Ties |
  | --- | --- | --- |
  | Isolated, Opus: guideline 2 against none | 2 against 4 | 2 |
  | Repo, Opus: guideline 2 against none | 0 against 6 | 2 |
  | Isolated, Opus: guideline 2 against guideline 1 | 3 against 2 | 1 |
  | Isolated, guideline 2: Haiku against Opus | 4 against 2 | 0 |
  | Isolated, Opus, guideline 2, against the original | 5 against 0 | 0 |

  Neil called three comments very clear: two from Isolated with guideline 2, including the rewritten `GameState.Fill`, and one from Isolated with guideline 1. He called a fourth, from Isolated with no guideline, fairly clear. All four came from Isolated runs.

  Leaning (Claude):
  - Working without the repository's prose matters most. Isolated, with guideline 2, beat the original every time.
  - The markers don't measure what Neil cares about. Guideline 2 cut the markers, but Neil preferred the versions without it more often. Each comparison sets one run against one other, so differences between runs, rather than between conditions, may explain much of this. The markers remain useful for detecting the style, not for ranking versions that are free of it.
  - Once the style is gone, what remains is mostly accuracy and terminology. Neil's notes are about a term that differs from the code's ("pull" for "thrust", "units"), a wrong impression (that the Voice is no character, or that `ToSave` writes a file), a doc that mentions what callers don't need (the range's ends, characters that aren't entities), and a doc so short that it confuses until the code is read. These need someone who understands the code, so they are a matter for review more than for a style guideline.
  - Haiku did at least as well as Opus, so a cheap model could do the rewriting, as 3.1(e) proposes.

  Neil (2026-11-27): guideline 2 may matter more than the pilots show. If training pulls a model slightly toward this style, and recursion magnifies the pull (2.5), then an instruction that pulls slightly the other way is what recursion should magnify instead. Leaning: adopt it, in some form, in `AGENTS.md`, alongside the rewrites, even though it can't outweigh the repository's prose while that prose is still in the style.
- [~] 3.6 First real rewrite (2026-11-27): Core's `Scripts` subsystem, chosen for having the most markers per comment line of the subsystems and parts with more than a few comments. `New-Rewrite.ps1`, now in the [`rewrite-prose`](../../.agents/skills/rewrite-prose/SKILL.md) skill, prepares a workspace with the C# code of Core and the game, every whole-line comment removed, and an `AGENTS.md` that contains only guideline 2. Then:
  1. Haiku wrote the 14 files' comments in the workspace.
  2. Opus, in the repository, checked them against the code and the old comments, and wrote [notes](../../experiments/comment-style/rewrite-scripts/review.md) on what was wrong or missing, without proposing wording. It found 50 problems. Most were facts that the old comments stated and callers need: requirements, ordering and timing, what happens on a second call, and why an interface exists. A few were wrong statements, such as calling a runner "a step", or saying that a dialogue's first entity is the speaker, which only the game's implementation assumes.
  3. Haiku fixed every problem in the workspace, in its own words.
  4. Claude fixed one `<see cref>` that didn't resolve, and rewrapped lines at 120 characters.

  Haiku's first draft is kept beside the notes. No code changed. With doc generation turned on, the subsystem has no doc errors, against eight before. Rates per 100 comment words, for the 14 files:

  | Version | Words | Words per sentence | General-purpose verbs | Stranded auxiliaries | Starts with "What" |
  | --- | --- | --- | --- | --- | --- |
  | Before | 1,270 | 16.1 | 1.89 | 0.71 | 0.79 |
  | Haiku's first draft | 1,190 | 9.6 | 1.34 | 0.84 | 0.17 |
  | After the review | 1,996 | 12.0 | 1.45 | 0.60 | 0.10 |

  The comments are longer than before, because they state what callers need instead of compressing it. Some of the style remains where it comes from the API's own names, such as "holds the title" for `IDialoguePresenter.Holds` and "while any hold is held" for `IScriptWorld.HoldCutscene`. Renaming those is out of this record's scope.

  Neil's review: much improved. He changed "take" to "receive" where it means receiving an argument, since "take" suggests removing from a collection, which isn't happening; changed "hold" to "contain" where it means containing; and cut "never once and kept". He also changed an error message to match, which a rewrite of comments shouldn't do, though he may do it again by hand. He isn't sure that the guideline should name the overused "keep", "hold" and "take".
- [~] 3.7 Rewriting a doc (2026-11-27): `Scripts/architecture.md`. A doc states design decisions and their reasons, which the code can't show, so its writer can't work from the code alone. Instead:
  1. Opus extracted the old doc's facts into a list of 170, in plain sentences of its own, each with its citations.
  2. Haiku wrote the doc in the isolated workspace, from the facts, the code with its rewritten comments, and the rules for the doc's format. Its first draft copied the facts one to a bullet, 245 lines of them, so the prompt now tells it to explain the design in short paragraphs, and how a doc for a directory with no parts is organized. The second draft read well.
  3. Opus reviewed the draft against the old doc and the code, and found 7 problems: lost backticks on every citation, citations on the wrong facts, a lost fact, and two records, 045 and 054, that the old doc cited but didn't list. Claude added notes on two habits of the draft: the same citation on every sentence of a paragraph, and about a dozen sentences that start "The reason is that". The guideline now covers both.
  4. Haiku fixed every problem.

  The [`rewrite-prose`](../../.agents/skills/rewrite-prose/SKILL.md) skill now gives this process for docs, beside the one for comments.

## 4. Rewriting what exists

- [ ] 4.1 Code comments and architecture docs: rewrite them one subsystem or part at a time, each in its own PR, once a mitigation is chosen.
- [ ] 4.2 Design docs: as 4.1.
- [~] 4.3 Decision records. Their rules allow a correction that makes a record more precise, which might cover rewriting their prose, but a rewrite could change what a settled item says. Neil (2026-11-27): the pilots show that precedent carries this much weight, so the records must be reworded too, although they are agents' memories and should be protected more strongly than the code. Each reworded record says so in one sentence under its status line. Leaning (Claude): "Reworded on YYYY-MM-DD, keeping its meaning and numbering, to steer agents away from a prose style that had drifted into these records (067 §4.3)." Rewording keeps each item's number and decision, so the three protections in [the records' rules](AGENTS.md) still apply, but the rules need a line that allows it. A reviewer should check each reworded record against the original for any change of meaning.
