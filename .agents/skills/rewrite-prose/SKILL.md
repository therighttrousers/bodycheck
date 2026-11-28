---
name: rewrite-prose
description: Rewrite a subsystem's or part's comments, its architecture.md, or other docs, out of the hard-to-read prose style that has drifted into this repository, keeping every fact. Use for the rewrite issues that issue 215 lists, or when asked to rewrite prose.
---

# Rewriting prose

Much of this repository's prose, in comments, docs and decision records, is in a style that is hard to read: general-purpose verbs such as "hold", "keep", "take" and "go" in place of precise ones, sentences that start with "What", verbs left implied, pronouns that point forward, and long chains of clauses. Agents imitate the prose they read, so the style spread from session to session. [Decision record 067](../../../docs/decision-records/067-comment-style.md) has the evidence. In short:

- An agent that writes in this repository copies its style, whatever its instructions say.
- An agent that writes from the code alone, without the old prose, writes much more clearly, and a cheap model does it as well as an expensive one.
- Without the old prose, the writer drops facts that the old prose stated, so a reviewer who can see the old prose must check the result.

So a writer works in an isolated workspace, and a reviewer checks its work in the repository. The writer is Haiku. The reviewer is Opus. Run each as a subagent, with the model set.

## Order

- Rewrite a directory's comments before its `architecture.md`, so that the doc's writer can read clean comments.
- Rewrite the docs bottom-up: a directory's `architecture.md` after those of its subdirectories, since it summarizes them.
- One subsystem or part per PR, its comments and its `architecture.md` together.

## Comments

1. Prepare the workspace: `.\.agents\skills\rewrite-prose\New-Rewrite.ps1 -Path <directory> -OutDir <scratch directory>`. It contains the C# code of Core and the game, with every whole-line comment removed, and an `AGENTS.md` that contains only [the guideline](guideline.md).
2. **Write.** Give a Haiku subagent the prompt in `prompt.txt`, and tell it the workspace is its project root: it must not read or change anything outside it.
3. Apply the draft: `New-Rewrite.ps1 -OutDir <scratch directory> -Apply`. It warns if anything other than comments changed.
4. **Review.** Give an Opus subagent the review prompt below. It writes `review.md` beside the workspace.
5. Read `review.md`, then copy it into the workspace.
6. **Fix.** Tell the Haiku subagent, or a new one, to fix each problem that `review.md` lists, in its own words, changing only comments, and keeping each comment as short as it can be while saying what the reader needs.
7. Apply again, and finish (below).

The review prompt:

> You are reviewing a rewrite of the comments in `<files>` in the repository at `<repository>`. The rewrite is uncommitted, so `git diff` shows it, and `git show HEAD:<path>` shows each file's old comments. Only comments changed. Read anything in the repository you need, but don't change any file except the one you write below.
>
> The old comments are often hard to read, so they were rewritten by someone who could see the code but not the old comments. Check the new comments for accuracy and completeness. Don't judge or rewrite their prose style, and don't propose wording: a writer will fix each problem from your notes, in their own words.
>
> For each new comment, check it against the code, and against how the code is used elsewhere in the repository. Report:
> 1. A statement that is wrong, or that could give a wrong impression.
> 2. A term that differs from the name the code uses for the same thing, or a reference written as plain text where a `<see cref>` to the member would work.
> 3. A fact that a caller or maintainer needs, which the old comments state and the new ones don't. This includes requirements on callers, ordering and timing, and the reasons behind non-obvious choices. Don't report facts that the code or signature already makes clear, or that only the implementation needs.
> 4. A detail that callers don't need, in a doc comment.
> 5. A doc comment so short that it can't be understood without reading the code.
>
> Write your notes to `<scratch directory>\review.md`, grouped by file, then by member. For each problem, give its number from the list above, and say what the fix must convey, as plain facts. Leave out files and members with no problems.

## An architecture.md, or another doc

A doc states design decisions that the code can't show, so its writer works from a list of its facts instead of from the code alone.

1. Prepare the workspace: `New-Rewrite.ps1 -Doc <doc> -OutDir <scratch directory>`. It is as for comments, except that it keeps the comments of the code under the doc's directory, which are already rewritten, copies the docs of its subdirectories, and adds `format.md`, the rules for the doc's format.
2. **Extract.** Give an Opus subagent the extraction prompt below. It writes `facts.md` into the workspace.
3. **Write.** Give a Haiku subagent the prompt in `prompt.txt`, isolated as for comments. The prompt tells it not to copy `facts.md`'s bullets one to a line: without that, the doc it writes is a list of facts, not an explanation.
4. Apply the draft, as for comments.
5. **Review.** Give an Opus subagent the doc review prompt below. Read the draft and `review.md` yourself, and add notes on anything the guideline asks for that the draft doesn't do, such as a phrase repeated throughout. Then copy `review.md` into the workspace.
6. **Fix**, as for comments, then apply again, and finish.

The extraction prompt:

> In the repository at `<repository>`, `<doc>` is a design doc whose prose is hard to read. A writer who can't see it will write a new version, so they need its content as a list of facts, without its prose. Read anything in the repository you need, but don't change any file except the one you write below.
>
> Write `<workspace>\facts.md`, containing:
> 1. The doc's title, and its "Relevant decision records" list, copied exactly, links included.
> 2. For each section of the doc, in order, its heading and the facts it states, as a bulleted list: one fact per bullet, in short, plain sentences of your own, not the doc's; the code's names for things, in backticks; every citation, such as `043 §1.7`, exactly as written, beside the fact it supports; the reasons the doc gives, as facts. If the code contradicts a fact, give the fact as the code has it, and mark it "Changed:".
> 3. The doc's open questions, as facts in the same way.
>
> Don't leave out any fact, and don't add any that the doc doesn't state.

The doc review prompt:

> You are reviewing a rewrite of `<doc>` in the repository at `<repository>`. The rewrite is uncommitted, so `git diff` shows it, and `git show HEAD:<doc>` shows the old version. It was written by someone who could see a list of the old version's facts and the code, but not the old version. Read anything in the repository you need, but don't change any file except the one you write below. Don't judge or rewrite the prose style, and don't propose wording.
>
> Report:
> 1. A statement that is wrong, or whose meaning differs from the old version's, unless the code shows the old version was out of date.
> 2. A fact or reason in the old version that the new one lacks.
> 3. A citation, such as `043 §1.7`, that is missing, attached to the wrong fact, or changed; or a link that is broken or changed.
> 4. A departure from the format that `AGENTS.md`'s "Architecture docs" section and `docs/designs/AGENTS.md` give.
> 5. A term that differs from the code's name for the same thing.
>
> Write your notes to `<scratch directory>\review.md`, by section. For each problem, give its number from the list above, and say what the fix must convey, as plain facts.

## Finishing

- Rewrap comments to 120 characters: `.\.agents\skills\rewrite-prose\Format-CommentWidth.ps1 <files>`.
- Build as CI does: `dotnet build -p:CI=true`.
- Check doc references: `dotnet build src\BodyCheck.Core -p:CI=true -p:GenerateDocumentationFile=true -p:NoWarn=CS1591`. Fix any error in the rewritten files, other than a missing `<param>` tag.
- Run `dotnet format --verify-no-changes`.
- Read the result yourself before asking the user to review it.

Don't change code, error messages included, even where a comment's new wording suggests it. Leave that to the user.
