# 051: The scripted run at the spawn

## Context

Inciting event (2026-11-06): issue #167, the last of the slice #142. At the spawn in `Outside`, the player starts up on the ledge and, in a cutscene, runs off it. This replaces the earlier plan, an `Impulse` control entity that pushed the player (`043 §1.11`): a run gives the eye time to find the player before it falls, which a push doesn't.

Decided in the issue:
- No impulse. The player spawns up on the ledge.
- During a cutscene, the player's controller acts on input from a script instead of the player's, and the script makes it run off the ledge. `PlayerController.GetInput` is where that input comes in: it gives none in a cutscene for now (`050 §1.5`). `GameModeChanged` clears the player's leniency timers and buffered presses only at a cutscene's edges, so a scripted run and jump keep their own (`050 §4.3`).

What this needs that doesn't exist yet:
- A way to start a cutscene with no dialogue. Only the game's `GameModeManager` enters `Cutscene`, when a dialogue waits in the box.
- Something that holds the script: the `Script` control entity of `043 §1.1` and `§5`, which the reader rejects as not yet supported (`045 §5.1`).
- A way for that script's input to reach the player's controller.

Goals:
- The run goes through the player's own controller and actions, as the player's input would, so it moves, animates and sounds as play does, with no second writer to the player's body (`046 §1.8`).
- The first use of scripts shapes them as little as it can: what a script can reach grows when a script needs more.

Anti-goals:
- A `Force` control entity, such as for wind, which raises triggers that act on every tick rather than once per touch. For later.
- Input that owns its own presses across modes (#174).
- A scripted camera (`043 §6.4`).

Closed on 2026-11-06. Outcomes are in `dialogue.md`, and in the architecture docs of Core, `Controllers`, `Simulation`, `Scripts`, `game` and `game/main`. Item 1.5, helpers for writing scripts, is open in `Scripts`.

## 1. What runs the run

- [x] 1.1 What sets it off. A `Trigger` around the `PlayerStart`, which a player that starts in it sets off (`048 §1.5`), through a `Once`, to what scripts the run (Claude). Nothing new. Wired by Neil, to a `Script` naming `RunRightOffLedge` (§2.3).
- [x] 1.2 What scripts it. Options:
  - (a) A `Script` control entity, running a C# class (`043 §5`). The mechanism already planned for what wiring can't say, and the first script is a small one to shape it with. Costs the most infrastructure: naming and making the class, and what a script can reach (§2).
  - (b) A control entity for this one need, such as `PlayerRun`, with its direction and how long, or until the player leaves the ground, as fields in LDtk. Data only, no C#. But the language grows a verb for each scripted move, and "until the player leaves the ground" is already logic.
  - (c) An action on the player's controller, such as a new `TriggerAction`. Rejected (Claude): actions name what a kind of entity does, and a run in a direction for a while doesn't fit `TriggerAction`'s verbs.
  - Decision (Neil): (a). The control entities are a visual language for level designers, and keeping it minimal, uniform and compositional takes design and, above all, watching it used, which entities like `PlayerRun` would get ahead of. Cutscenes will need `Script` anyway, so this is the chance to build it. It may be that the language never needs to go beyond `Script`: Neil's kids can all code a little.
- [x] 1.3 What the run is (Claude, agreed by Neil): the script starts a cutscene, waits a moment so the eye finds the player, holds right until the player leaves the ground, lets go, waits until it is on the ground, and ends the cutscene. The details are for playing. Neil writes the script, to get a feel for writing them.
- [x] 1.4 Where the player starts: up on the ledge, in the trigger of §1.1 (Neil).
- [ ] 1.5 Helpers for writing scripts (Neil): maybe a `Promise`-like fluent interface, or a small async library over what scripts and async actions can see, such as `IWorldView` or `Entity`, so that a wait like "until the player is on the ground" is one line. It would have helped with the door's open and close. To be shaped by writing this script.

## 2. Scripts

- [x] 2.1 What a script is. Decision (Claude, built so with Neil's go-ahead): a plain class that implements `IScript`, with a constructor of no arguments, made for each run and dropped when it finishes (`043 §5.1`), with one method, `TickTask Run(IScriptWorld world, ITick tick)`, an `async` method given what it can reach (§2.4) and the world's tick, which is passed apart, as to an action (`046 §1.3`). A `Script` runs every `Default`, alongside any run in progress (`043 §1.7`).
- [x] 2.2 Where script classes live. Options:
  - (a) In Core, in `Scripts`, beside the runners.
  - (b) In the game's project.
  - Decision (Neil): (b), in `game/scripts/`, in the namespace `BodyCheck.Scripts`, so that level designers seldom need to dig into Core. Scripts straddle art and logic, so it isn't clear-cut. The tests are in the game's project (`031`), so they can run them.
- [x] 2.3 How the world finds the class. Core can't name the game's types, but it could find them by name at run time in the loaded assemblies. Options:
  - (a) The world looks the name up in every loaded assembly when it is built, and fails if it isn't a class that implements `IScript` with a constructor of no arguments.
  - (b) The game gives the world a way to make a script by name.
  - Decision (Neil): (b), an `IScriptLibrary`, a third of the pattern of `IDialoguePresenter` and `IAssetLibrary`: what a world needs from the game is given to it, so that a test's dependencies are plain when it has to give them. The world checks each `Script`'s name against it when it is built, as it checks dialogues (`044 §5.2`), so that a wrong name fails in CI.
  - For ease of writing levels, the field is `ClassName`, not `FullyQualifiedClassName` (`043 §5.2`), and holds the name without the namespace: `RunRightOffLedge`. The game's library adds `BodyCheck.Scripts.` (Neil).
- [x] 2.4 What a script can reach (`043 §5.3`): an interface given to it, so that it can't reach past it and a test can give it a fake. For the run it needs the player's controller, for its input (§3.3), whether the player is on the ground, and the cutscene (§3.2). Neil: `IWorldView` could move into `Controllers`, where all of its users are but the simulation, which implements it and already uses `Controllers`, and give the player's controller. Claude: one user is outside, `IController.Ready(IWorldView)`, which is in `Entities`, since an entity holds its controller (`034 §1.5`). Options:
  - (a) Move `IWorldView` into `Controllers`, with `Ready` off `IController`, onto an interface in `Controllers` that the world tests each controller for, and give it the player's controller. Scripts get `IWorldView`, with the cutscene beside it. One view of the world for everything that runs in it. But every controller could then drive the player, and `Ready` becomes a type test.
  - (b) Keep `IWorldView` in `Entities`. Scripts get a view of their own, `IScriptWorld`, in `Scripts`, which extends `IWorldView` with the player's controller and holding a cutscene, and which the simulation implements. `Scripts` then uses `Controllers`, a new arrow with no cycle, since `Controllers` uses only `Entities`. Controllers don't see the player's controller.
  - Decision (Neil, as Claude suggested): (b).

## 3. The cutscene and the scripted input

- [x] 3.1 How Core asks for a cutscene. Options:
  - (a) A request the game polls, as it does `WorldNode.PauseRequested` for a phase: `WorldNode.CutsceneRequested`, true while any script holds one.
  - (b) An interface the game implements, as `IDialoguePresenter`, through which a script starts and ends a cutscene.
  - Neil: controllers won't ask for cutscenes, but scripts usually will, since they usually control the player, though a script for a rogue robot running around wouldn't. The world could keep the scripts that are running, as it keeps its entities. Claude: that separates running a script from holding a cutscene; the world asks for a cutscene while any script it runs holds one.
  - Decision (Neil): (a), with the world keeping its running scripts. `GameModeManager` enters `Cutscene` while the world asks or a dialogue waits, and leaves only when neither, so that a dialogue that ends partway through a script's cutscene doesn't end the cutscene.
- [x] 3.2 How a script holds the cutscene: a handle, which the script disposes, with `using`, so that the cutscene ends when the script does, even if it throws (Claude). Core keeps the handles held, not `GameModeManager` (Neil): the game's mode state is for what Core doesn't read, such as, one day, what gives a cutscene its look, like a sepia filter for one set in the past.
- [x] 3.3 How the script's input reaches the player's controller. Options:
  - (a) The world holds it: the script sets it on what it can reach, and the player's controller reads it through `IWorldView` in a cutscene.
  - (b) (Neil) The script calls public methods on `PlayerController`.
  - (c) Scripts drive the player's actions directly, such as `RunAction`. Rejected: two writers to one body (`046 §1.8`), and the run wouldn't feel like the player's.
  - What the methods take. Input, such as `SetScriptedInput(new PlayerInput(XAxis: 1))`, goes through all of the player's logic, its leniency, edge detection and actions, so a scripted run is one the player could make, but is low-level: a jump is a press held for some ticks. Intent, such as `Jump()` returning a `TickTask`, is easier to write with, but is either a second writer to the body or an action recorded for `Apply` that the script waits on, which `046 §1.8` leaves until something needs it.
  - Decision (Neil, as Claude suggested): (b), with methods that take input. The scripted input lives in the controller, its only reader, which clears it when a cutscene ends, in `GameModeChanged`, beside what it already does there. A library of helpers, written on it as plain async code, keeps most scripts short while leaving them full control when they need it (§1.5); the tests may suggest its shape.
- [x] 3.4 Scripted input during a dialogue: the player's controller acts on it, since a dialogue is a cutscene, and it is none unless a script sets it (Claude).

## 4. `Impulse`

- [x] 4.1 `043 §1.11`'s `Impulse` is dropped (issue #167). A `Force`, for wind and the like, is for later.
