# 066: Actions that name what they do

Status: closed.

## Context

Inciting event (2026-11-23), while planning `065 §1.6`: Neil decided that a `Once` responds to `TurnOn`, `TurnOff` and `ToggleOnOff`, as a trigger does, so that a route can arm it again. Routes reach a `Once` by `Default`, and `043 §1.4` lets `Default` be accepted only by a kind that responds to nothing else, so that it never stands for one of several. A `Once` would break that rule.

## 1. Actions

- [x] 1.1 What a `Once` responds to. Options (Claude): (a) an exception to `043 §1.4` for a `Once`; (b) a new action to run it, such as `SetOff`, in place of `Default` for a `Once` alone; (c) no switching for a `Once`. Neil: get rid of `Default`, and give each kind that responds to it an action that names what it does. Decision (Neil): so, in a PR of its own, before `065`'s game state that outlives worlds. It supersedes `043 §1.4`, which has nothing left to protect once no action stands for whatever its target does.
- [x] 1.2 The actions. Decision (Neil, as Claude laid out): `Run` for a `Script`, a `Sequence`, a `Fanout` and a `Once`; `Present` for a `Dialogue`. A `Respawn`, which responded to `Default` and did nothing, since it becomes current when the player comes near it, responds to no action until something needs one. Every action names what it asks for, so a route reads as what it does, such as a sequence that presents a dialogue and then closes a door. The cost is more words for level designers, and a wrong one is an error the reader reports, with where it is, as before.
- [x] 1.3 The levels. Neil changed LDtk's `TriggerAction` enum, replacing `Default` with `Run` and `Present`, and every route that used it, by hand; no `Action` field has a default value any more.
- [x] 1.4 Built (Claude): `IActionTarget.Default` became `Run` and `Present`, each runner implements the one it responds to, and the test of `043 §1.4`'s rule went with the rule. A test whose triggers ran a `Respawn`, as a target that did nothing, runs a script that does nothing instead.
