# 065: Persisted game state

Status: open. Settled: the split into PRs (§0), the player with a constant id (§1.3), the game state outlives worlds (§1.1) and is filled in lazily (§1.2), what moves into it (§1.4), one state for a `Once` and a trigger (§1.6), the test worlds (§1.7), switch states in Core's view (§1.9), names worked out per world (§1.10), putting a door back (§2.3), and debug restarts (§4.1). Open: names from LDtk (§1.5, §1.8), death and save points (§2.1, §2.2), saving (§3), and the playtester's controls (§4.2).

## Context

Inciting event (2026-11-23): issue #2, persisted game state. One typed record of what has happened, which saves, death, world changes, dialogue conditions and the map read; a save file is that record, as typed, validated JSON. The idea to settle: the world is a function of the LDtk data and the game state, so that death is "build the world again at the last save point", the path F5 and the respawn keys already take, rather than a rewind of its own. The catch: a world change has to be in the state as it happens, or dying would undo it.

What there is now (Claude, from the code):
- `GameState` (`063`) is made fresh from a `World` each time `Play` builds one, so every build, including F5 and the respawn keys, is a new game.
- It holds each entity's name, the player's abilities, and each door's `LastAction`. Names are derived from the world's data.
- State that outlives a beat but isn't in `GameState` yet (`063 §1.7`): whether each `Once` has run, in its `OnceRunner`; whether each trigger is on, in its `TriggerRunner`; which world is loaded, in `Play._worldIndex`; each world's current respawn, in `Play._respawnIds`.
- No death builds the world again yet: there are no hazards.
- Settled, not built: `GameStart` and `WorldStart` (`064 §2.2`), a `Once` per game in the game state (`064 §2.3`), which world is loaded in the game state, starting in `Main`, with switching resetting nothing (`064 §2.5`). Leaning: debug restarts keep the game state (`064 §2.4`).

Principles that bear on it ([high-level-design.md](../designs/high-level-design.md#experimentation-and-failure-have-low-cost)): save points are frequent; quitting saves at the last save point, with no loss of world state or inventory; death restores everything.

Goals:
- A world built again, from a restart, a death or a load, is whole: every beat the player is past stays past, and nothing is halfway.
- One source of truth for what has happened (`063 §1.7`: a single class first, a separate save file only if it fails).
- Devs, whose levels and state classes change often, aren't bitten by saves that no longer fit (`063 §1.7`).

Anti-goals:
- Hazards and death themselves, beyond the rebuild a death will take.
- Save slots, a title screen, and menus for saving and loading, unless they are needed to test the rest.

## 0. How the work is split

- [x] 0.1 Neil: smaller PRs; first get ready to persist, then persist in a later PR. Claude, in order:
  1. The player as a control entity's start, with a constant id (§1.3). Small, and the next PR leans on it.
  2. The game state outlives worlds, in memory only: §1.1, §1.2, §1.4, §1.6, a door put back by its state (§2.3), and debug restarts that keep the state, with a key for a new game (§4.1). A door has to be put back in this PR, not the saving one: once F5 keeps the state, a door whose `LastAction` is `Open` is built closed.
  3. Names from LDtk (§1.5), whenever, but before saving.
  4. The save file (§3), and death once there are hazards.
  `GameStart` (`064 §2.2`) needs none of these, and can go in any of them, or on its own.
  Decision (Neil): this split.

- [x] 0.2 More PRs, found while planning the second (Neil): replacing `Default` with actions that name what they do, before the second, since a `Once` that answers switching actions needs it (`066`); removing `RoomLibrary` (§1.7); and `GameStart` and `WorldStart` (`064 §2.2`), in a PR of their own.

## 1. What the game state is

- [x] 1.1 Lifetime. Decision (Neil, as Claude suggested): the game state outlives worlds, made once for a new game or, later, read from a save, and handed to every world built, as `064 §2.5` implies.
- [x] 1.2 Entity states across worlds. They are made from a world's data now. Options: (a) filled in for every world when the game starts, from all of them; (b) filled in for a world the first time it is entered, kept after; (c) only what differs from the world's data is stored, and the rest is derived each build; (d) each build fills in the states its world's entities lack, from their data, and keeps those it has. Found (Claude): (b) becomes (d) as soon as a level is edited, since `Reload` reads the files again and a new entity has no state; and (d) is one path for a new game, a world entered the first time, an edited level and a load. Decision (Neil): (d), lazy.
- [x] 1.3 The player's state is the game's, not a world's. Found (Claude): every world has a player entity of its own, with its own id, so abilities kept under the player's id would differ from world to world. Neil, already considering it: `PlayerStart` becomes a control entity, where the player starts, rather than the player itself, and the player gets a constant id, `player`, as the player's block has `player-block`, which is on no map. A few `EntityConfig.Player`s made in code change their ids.
  Decision (Neil): so, in the first PR of §0.1. Built (Claude): `EntityKind.Player` and `EntityConfig.Player` became `EntityKind.PlayerStart` and `EntityConfig.PlayerStart`, a control entity that responds to no action. The world builds the player itself, first of its entities, so that it is stepped first, at the current respawn or else at the `PlayerStart`, with its look from the asset library's `GetPlayerVisual`. Its id and the player's block's are `Constants.PlayerId` and `Constants.PlayerBlockId`. The game state makes the player's state itself, not from a configuration. Neil moved the LDtk project's `PlayerStart`s to the `Control` layer, tagged `Control`. Nothing refers to one by its `iid`. Neil notes, and accepts, that the intro's run off the ledge can now be started at the game's start only by a trigger.
- [x] 1.4 What moves into it: whether each `Once` has run (`064 §2.3`); whether each trigger is on; which world is loaded; each world's current respawn. Decision (Neil): all four, the trigger's being on included. An enum rather than a bool, if either has more than two states (§1.6).
- [ ] 1.5 Names. Found (Claude): an entity's serial is the shortest start of its id that tells it from the others of its kind in its world, so adding an entity to a level can lengthen another's name, and a name saved, or written in a dialogue file, goes stale. Neil: evidence against naming entities automatically. Option: a `Name` field on entities in LDtk, with the kind and the start of the id as a convention for what to write there, not a rule in code; possibly easier to translate, since names are then data a translator sees.
- [x] 1.6 Merging a `Once`'s state with a trigger's. Neil asks what the issues are. Found (Claude): they behave alike. A trigger that is off finishes at once when set off; a `Once` that has run finishes at once when it is reached. While either runs, what reaches it joins the run. The only difference is who switches it: a trigger is switched by actions (`TurnOn`, `TurnOff`, `ToggleOnOff`) and its `StartsOn`; a `Once` switches itself off by running. So one state, on or off, held per control entity by id, would do for both. The issues:
  - When a `Once` turns off: as it starts, or as its step finishes. If as it starts, a world built again in the middle, by a death or a load, never finishes the beat, which is halfway. If as it finishes, the beat runs again from its start, which is whole as long as a beat's steps can run twice, as asking a door to open again can (`063 §1.2`). Claude: as it finishes. Its running is the runner's, in memory, and never saved, so it needs no third state.
  - Whether a `Once` answers `TurnOn` and the rest once merged. Doing so arms it again, which is new power for level designers, such as a beat once per something; refusing keeps `Once` meaning once. Claude: it answers them, since uniformity is cheap here, and a `Once` no one switches behaves as now.
  - Control entities have no state in the game state now: only entities with bodies do, with names. They would get one, with an id and whether it is on, and no name.
  - A third state Claude can't find: a trigger that is on but hidden, or spent but showing, is presentation, not state. So a bool for now, and an enum if one turns up.
  - Decision (Neil): merged, as Claude suggested. A `Once` turns off as its step finishes. It answers `TurnOn`, `TurnOff` and `ToggleOnOff`, as a trigger does. Control entities get states of their own in the game state. A bool, since neither Neil nor Claude can find a third state. `Reentry.Once` goes, rather than being made more elaborate (Neil): a reentry policy shouldn't read or write the game state.
- [x] 1.7 The test worlds' state (`064 §2.6`). Found (Claude): ids don't clash. LDtk's are unique across the project, and the room library's are the room's name and a tile, such as `Test 7,2`, unique among its rooms. Neil: playtesters' test state in the save could help. Neil asks whether worlds' states should be isolated from each other, which would make test state easy to leave out of a save, at the cost of a dictionary per world in `GameState`. Claude: the only thing that is a world's own is its current respawn, a dictionary by world name; every other state is found by an id that no other world uses, so `GameState` needs no level of worlds. Isolation, if wanted, is `Play` holding more than one `GameState` and choosing which a world is given, such as one per library room, with no change to `GameState`. Until it is wanted: one game state for every world.
  - Found (Claude, at Neil's question): the only test worlds `Play` builds with the game's state are `RoomLibrary`'s, `Phase` and `Look`; tests and probes build their own. Decision (Neil): remove `RoomLibrary`, in a PR of its own. `Look` moves into the LDtk project; the `Phase` room isn't needed for playing, and stays only where the phase probe needs it, among the rooms tests and probes measure in. Then every world `Play` builds is from LDtk, and one game state for all of them is the game's.

- [~] 1.8 Names for control entities (Neil): a dialogue that asks whether a trigger is on, to know whether the player can do something, should be able to, and finding the trigger by its `iid` is awful. Leaning (Neil): every control entity can be named, along with §1.5's names for entities with bodies.
- [x] 1.9 Core's view of a `Once`'s or a trigger's state (§1.6). Decision (Neil, as Claude suggested): one `IGameState.ISwitchState`, with `IsOn`, found by id with `GetEntity<T>`, as every other entity's state is, whose only writer is its runner; on the game's side, a state with no name, since a control entity isn't a character. Neil considered an `ITriggerState` and an `IOnceState`, which can come if they are needed.
- [x] 1.10 Names under lazy filling (§1.2). Decision (Neil, as Claude suggested): each build works out the names of its world's entities again, and checks that they are unique within that world only, until names come from LDtk (§1.5).

## 2. Death and save points

- [ ] 2.1 What a save point is. Options: (a) every `Respawn`, which the player reaches by coming near, so frequent by construction; (b) a kind of its own. Claude: (a) for now.
- [ ] 2.2 What a death restores. Under the issue's idea: the world is built again from its data and the game state, at the current respawn, so blocks and platforms go back to where the data puts them, and world changes stay, since they are in the state as they happen. Nothing is snapshotted.
- [x] 2.3 A beat that ends with something moved, such as a door opened, is put back by its state, not by replaying how it got there: a door moves straight to the stop of its `LastAction` (`063 §1.7`). How controllers take part: a step in their life cycle, such as one run when the world is built, after `Ready`. Options (Claude): (a) the door moves straight to that stop, by `MoveAndCollide`, in a new step of its controller's life cycle, run once the world is built, as `063 §1.7` sketched, so that something under it can still stop it; (b) it is built at that stop. Decision (Neil): (a). Controllers will generally need to do things of their own to make their entities agree with the game state, so the hook is built now.

## 3. Saving and loading

- [ ] 3.1 When it is written. Options: on quit; whenever the current respawn changes; whenever the state changes. Claude: on quit and when the current respawn changes, so a crash loses no more than a death would.
- [ ] 3.2 Format. Typed, validated JSON, by `System.Text.Json`'s source generator (architecture.md, conventions). `GameState` and its entity states are `RefCounted`, for GDScript and dialogue files, and the source generator would see Godot's own public members too, so: (a) serialize them, ignoring Godot's members; (b) a plain record type for the file, which `GameState` is made from and writes. Claude: (b), as the file's own type, and the game's only other shape of the state; it fails `063 §1.7`'s "single class first" in letter, though not in spirit, since the file is written from the state and never read by anything but its constructor.
- [ ] 3.3 A save that no longer fits: an entity id no longer in the world, an entity with no state in the save, a field renamed. Options: (a) an error, as for any data, and devs start a new game; (b) entities gone from the world are dropped and new ones start as their data says, as configurations are forgiving of what they leave out (`017 §2.2`); (c) a version number, and migrations once there are players. Claude: (b) for ids, (a) for anything else, and (c) once a build goes to someone outside.
- [ ] 3.4 Where: one file in `user://`, for now.

## 4. Debug restarts and new games

- [x] 4.1 `064 §2.4`, leaning (a): F5, `;` and `'` keep the game state. Then a key for a new game is needed, and, once there is a save, one to drop it. Decision (Neil): they keep it, which settles `064 §2.4` as (a), and F5, which reads the levels again, too, with what changed filled in lazily (§1.2). Ctrl+F5 starts a new game, as a browser's hard refresh does; it is mostly something for an in-game menu to have.
- [ ] 4.2 What the playtester's controls do with saves, so a playtest of a puzzle starts from a known state (`017`).

## 5. Built in the second PR

- [x] 5.1 Found while building (Claude), and built so:
  - A door is put back only when something has asked it to open or close since the game started. Built first (Claude): only when its `LastAction` differs from the action its place in the data counts as (`StartsClosed`). Found in play (Neil): the first door the player meets starts half open and must stay so until it is first asked, whichever stop it is nearer. Changed (Neil): `OpenableAction.None`, a door's `LastAction` until it is first asked, which supersedes `063 §1.2`'s start from the data's place. `StartsClosed` now only says which way a door's first toggle goes. `LastAction` says what was last asked, not whether the door is open, since something in the way may have stopped it. Dialogue and scripts have one more value to check, but learn more: whether a door has ever been asked.
  - `RestoreGameState` runs at the start of the world's first tick, on either path, played or paused, rather than when the world is built, since `MoveAndCollide` needs the world in the engine's space.
  - Ctrl+F5 does what F5 does, with a new game's state, which has no world yet, so it starts in the first world, `Main`, at its `PlayerStart`, as a new game does (`064 §2.5`).
  - A dialogue's speakers are found among the Voice and the entities of the world last filled in, since names are unique only in a world (§1.10).
  - `Reentry.Once` is gone (§1.6): a `Once` joins, as a trigger does, and its switch says whether it runs.
