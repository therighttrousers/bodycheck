# 064: Starting again at a respawn

Status: open. Settled and built: the fix for the intro's run (§1). Settled, not yet built: `GameStart` and `WorldStart` (§2.2), a `Once` per game (§2.3), worlds in the game state (§2.5). Open: whether a debug restart keeps the game state (§2.4), the test worlds' state (§2.6), and `GameStart` outside the first world (§2.7). Deferred: §3.

## Context

Inciting event (2026-11-17): issue #218. In the main world, if the player comes within reach of a respawn and the user presses F5, `RunRightOffLedge` runs again after the player starts at the respawn, and never ends.

Found (Claude):
- In `Outside`, a trigger set off by `Start` runs the `Script` that names `RunRightOffLedge`, directly, with no `Once`. Its area is a 16×16 placeholder away from the `PlayerStart`, which doesn't matter for `Start`. This differs from `051 §1.1`, which planned a trigger around the `PlayerStart`, set off by touch, through a `Once`; `056 §1.1` then built `Start`, and the intro was wired with it.
- A world sets its `Start` triggers off in its first tick that sets triggers off (`056 §1.2`). F5, `;` and `'` all build a new world, at the world's current respawn, so each of them is a start. A `Once` wouldn't help: its state is in its runner, which is built again with the world, and the game makes a new `GameState` for each world it builds.
- The script's entity, the `PlayerStart`, is the player wherever it starts, as it should be. At a respawn on solid ground, the script holds right until the player leaves the ground, which it may never do, so its cutscene is never let go of, and the game takes no input: a softlock.
- No death rebuilds the world yet, so today only the debug restarts reach this. Respawning after a death and loading a save will.

`056 §1.2` left this open: "Whether a world rebuilt after the game state loads counts as starting again, and so whether `Once` matters for it, waits for the game state." `063` has since built the game state, and `063 §1.7` notes that a `Once`, and whether a trigger is on, belong in it, and that level designers need ways to hook into the world's life cycle.

Two kinds of start have shared one mechanism:
- **The world's start**, what `056` built `Start` for: scripts about entities, such as a block that hovers and follows the player. Running again whenever the world is built is right for them, since their entities are built again too.
- **A beat's start**, such as the intro's run: something in the story, which happens where the player is, once in a playthrough.

Goals:
- A restart at a respawn, from debugging, death or a save, never replays a beat the player is past, and never strands the player in a cutscene.
- Defence in depth: what sets a beat off is right, and a script that can't finish still lets go.

Anti-goals:
- Building saving and loading (`063` anti-goals).

## 1. The intro's run

- [x] 1.1 What sets it off. Options:
  - (a) As `051 §1.1` planned: a trigger around the `PlayerStart`, set off by touch, through a `Once`, to the `Script`. A player that starts in a trigger sets it off (`048 §1.5`), in the world's first tick that sets triggers off (`056 §1.3`). At another respawn, the trigger doesn't hold the player, so nothing runs; at the start, the run plays again, which is right, since the player is up on the ledge. Needs only wiring.
  - (b) Change what `Start` means (§2.2).
  - Decision (Neil, as Claude suggested): (a) now, wired by Neil in LDtk. §2 is for when deaths and saves make it matter.
- [x] 1.2 A timeout on `RunRightOffLedge`, so that if it can't finish, such as when it starts somewhere it can't run off a ledge, it ends, and lets go of its cutscene. Decision (Neil): defence in depth beside §1.1. Built (Neil): a tick scoped to 2 game seconds from the start, which the script's waits use; the run and fall take about 1.5. Neil: every script that waits for a condition, and doesn't detect when it can't be met, such as by noticing a stall, should have a timeout. It was easy to write.
- [x] 1.3 Built (Neil): the script reaches the player through `IScriptWorld` rather than taking it as its entity, so the `Script`'s `Entities` is empty.

## 2. What a world built again sets off at the start

Framing (Neil): treat several worlds as a feature of the shipped game, which has a way for the player to move among them, so that decisions here hold when one does (§2.5).

- [x] 2.1 The ways a world is built, and which start each is. Decision (Neil):
  - The first world of a new game: the game's start.
  - A switch between worlds: the world's start. Scripts that run as long as the world does, such as one day an N-body simulation, start with it.
  - A debug restart (F5, `;`, `'`): the world's start.
  - A respawn after a death, and a load from a save: the world's start.
- [x] 2.2 What `Start` means. Options:
  - (a) The world's start, as now: every world built. Beats use touch around where they happen, as §1.1 does, through a `Once` whose state is in the game state (§2.3).
  - (b) The game's start: set off only when the world is built with the player at the `PlayerStart`, not at a respawn or from a save. Changes `Start` for every trigger, and so breaks it for scripts about entities, which then need another way to start with the world.
  - (c) Two values of `SetOffBy`, such as `WorldStart` and `GameStart`, or `Start` with a field saying which. Says both, at the cost of a word in the level designers' language.
  - Decision (Neil): (c), as `GameStart` and `WorldStart`, at least: until §1.1 the main world had one of each, the intro's run and, through `056`, scripts about entities. `Start` becomes `WorldStart`, as it behaves now; `GameStart` is set off only at the game's start (§2.1).
- [x] 2.3 Whether a `Once` outlives a world. Decision (Neil): yes. Every `Once` so far is for once in a game, so the game state keeps whether each has run. If something needs once per respawn or load, `Once` can gain a field, or a new control entity can say it. Whether a trigger is on outlives a world too, under `063 §1.7`, but nothing has decided it yet.
- [~] 2.4 Whether the debug restarts keep the game state. Now each world the game builds has a new one, so a restart is a new game at a respawn, a combination no player can reach. Options: (a) keep it, so that a restart is as a respawn after a death will be; (b) keep it for `;`, `'` and a restart, with a key for a fresh one; (c) a new one, as now. Leaning (Neil): (a), as more useful, though Neil could be persuaded otherwise.
- [x] 2.5 Worlds in the game state, from the framing above. Decision (Neil):
  - Which world is loaded is in the game state.
  - The world named `Main` in LDtk is special only in being the one a new game's state starts in.
  - Switching between worlds resets none of the game state.
- [ ] 2.6 The test worlds' state. Under §2.5, the state of the test worlds, such as the library's rooms, and of the main world mix in one game state, which Neil doesn't like. Options are to be found.
- [ ] 2.7 A `GameStart` trigger in a world other than the first of a new game, which under §2.1 is never set off. Options: (a) the reader rejects it, as it rejects a `Start` trigger that starts off (`056 §1.4`); (b) it is set off the first time the game enters that world, which a `WorldStart` trigger through a `Once` already says (§2.3); (c) it is allowed and never set off. Claude: (a), since (b) has a way to say it already.

## 3. A script that never ends

Deferred (Neil): more design than this record needs now. §1.2 has each script guard itself for now.

- [ ] 3.1 Whether anything beyond a script's own timeout (§1.2) keeps a cutscene from being held forever. Options: (a) nothing, each script is responsible; (b) a timeout on every hold, given when it is taken, such as in `CutsceneRequest`; (c) a debug key that cancels every script; (d) a warning in debug builds when a hold lasts longer than some time.
