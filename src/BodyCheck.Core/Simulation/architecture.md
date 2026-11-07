# Simulation

Relevant decision records:
- [012: Subsystems in `BodyCheck.Core`](../../../docs/decision-records/012-core-subsystems.md)
- [013: The world as a container of rooms](../../../docs/decision-records/013-world-of-rooms.md)
- [020: Drawing rooms](../../../docs/decision-records/020-drawing-rooms.md)
- [022: Actions at the end of a tick](../../../docs/decision-records/022-end-of-tick-actions.md)
- [023: The player's spawned block](../../../docs/decision-records/023-spawned-block.md)
- [029: Tunable bodies](../../../docs/decision-records/029-tunable-bodies.md)
- [043: Triggers, actions and scripts](../../../docs/decision-records/043-triggers-and-scripts.md)
- [044: Dialogue](../../../docs/decision-records/044-dialogue.md)
- [045: Control entities in the world](../../../docs/decision-records/045-control-entities-in-the-world.md)
- [046: Actions as async methods](../../../docs/decision-records/046-async-actions.md)
- [047: `Once`, and routing actions](../../../docs/decision-records/047-once-and-routing.md)
- [048: Triggers set off by touch](../../../docs/decision-records/048-touch.md)
- [049: Doors and their stops](../../../docs/decision-records/049-doors-and-stops.md)
- [050: The cutscene mode](../../../docs/decision-records/050-cutscene-mode.md)
- [051: The scripted run at the spawn](../../../docs/decision-records/051-scripted-run-at-the-spawn.md)
- [052: The talking door](../../../docs/decision-records/052-talking-door.md)
- [053: Cancelling tasks](../../../docs/decision-records/053-cancelling-tasks.md)
- [055: Unhandled task failures](../../../docs/decision-records/055-unhandled-task-failures.md)
- [056: Scripts for entities](../../../docs/decision-records/056-scripts-for-entities.md)

## What it is

The world brought to life: its tiles turned into colliders and drawn room by room, its entities created where they start, each given its controller, and all of them stepped every physics tick. Tests use it as it is, so it has no camera, lights for the scene or input device of its own. The game gives it what to draw for each kind of entity and for tiles, where the player's input comes from, and the game's mode, so it knows no assets and no devices.

## What it is not for

Presentation: the camera, the scene's lighting, overlays, the rendering pipeline. Choosing or switching worlds. The rules of a single entity or controller.

## `WorldNode`

`WorldNode` is one world, simulated: tiles and entities, with no camera, lights or input of its own.

- It builds the colliders from the world's grid, as one static body (`020 §3.1`), the doors' stops as another, on the `DoorStop` layer, which only doors collide with, since a door passes through tiles to hide in them (`049 §1.1`), and a `RoomNode` for each room, which draws the room's tiles as a few multimeshes (`020 §2.1`).
- It builds an entity from each of the world's `Bodies`, gives each its looks and its controller, and steps every one each physics tick, in that order, so that `EntitiesOf` finds them by their configurations. A vertical door is frozen where it starts, closed if it is against its close stop and otherwise open, and its controller unfreezes it to move it (`049`). It runs the actions controllers defer, at the points [Core's doc](../architecture.md#how-a-tick-runs) describes (`022 §2.1`).
- It has a `TickScheduler`, which it gives as its `Tick` to what runs in it, and runs once in each played tick but the world's first, after the entity steps' deferred actions, while controllers are enabled, with the tick's game time, so that waits count game time; paused ticks don't run it (`046 §1.2`). The engine steps the world after each tick, so in the first, no body has contacts and no area holds anything, and what a script saw then would be wrong; a world built during a physics tick may be stepped before its first, but not one built before the game's first, so neither runs it then (`056 §1.3`). `StopAndQueueFree` stops it, so that what runs in the world, such as scripts and dialogues, unwinds and cleans up while the world is still whole and in the tree, then stops the world processing and queues it to be freed. Whatever replaces a world calls it, rather than freeing the world (`053 §2.3`, `§3.3`). A failure that nothing in the world handles, in a tick or while stopping, goes to `GD.PushError`, with its stack trace, and the world plays on (`055 §2.2`).
- **Routes.** Once every entity has its controller, it builds a runner for each control entity that runs others or responds to actions, from its configuration, with each step resolved to what runs it: a control entity's runner, or a body's controller. Each runner is built after its targets, which the world's having no cycles allows; a route that switches a trigger counts as leading into the trigger's step, though switching never runs it (`052 §1.5`). `Triggers` holds the triggers' runners by id; the others are reached through their routes (`045 §3.3`, `047 §2.3`). A `Dialogue`'s runner presents the dialogue through the `IDialoguePresenter` the world is given, which a world needs only if it has dialogues, and a `Script`'s makes its script with the `IScriptLibrary` it is given, likewise, and runs it among the world's `RunningScripts`, with the world as its `IScriptWorld`. A world is made with `WorldNode.Build`, which first checks that each dialogue's file exists and holds its title, and that the script library holds each script, taking the entities it is given, of the classes that `EntityClassOf` says the world builds for their kinds, and fails if not; only then does it call the private constructor, so that a world that fails makes no nodes, which nothing would free. A wrong name then fails when the world is built, and a test that builds every world makes it a failure in CI (`044 §5.2`, `051 §2.3`). A target that can't run its action yet finishes at once, so that the levels load and play: a `Respawn`, which becomes current by reach (`047 §2.4`).
- **Setting triggers off.** It builds an `Area3D` for each trigger, of its box in LDtk, on the `Trigger` layer, masked to the player's. In each played tick that runs its scheduler, just before it does, it asks each trigger, in the order of the world's controls, whether its area holds the player, as of the last step, so that what a trigger starts runs on in that tick's run (`048 §1.1`, `§1.2`, `§1.4`). Only the player sets a trigger off, for now (`048 §1.3`), and only one that is on (`052 §1.1`). `TriggerSetOff` is raised each time one is.
  - **By touch:** when its area holds the player and didn't when last asked. A trigger has held nothing before it is first asked, so a player that starts in one sets it off (`048 §1.5`). It keeps track whether it is on or off, so one turned on with the player already in it waits for the player to come into it again (`052 §1.4`).
  - **At the start:** in the first tick that sets triggers off, if it is on then. One that is off then never is, even if it is turned on later (`056 §1.2`).
  - **By interacting:** when its area holds the player, which is on the ground, and `PlayerInput.InteractHeld`, up, is held and wasn't at the last tick, only while the game is playing (`052 §2.3`, `§2.4`). `InteractTrigger` is the trigger the player could set off so, as of the last tick of play, for the game's prompt: the first, if several could (`052 §3.1`). An area finds what is in it at the end of each step, so a body is missed if it moves more in a tick than its width and the trigger's together (`048 §1.7`).
- **What scripts see.** It is the `IScriptWorld` it gives scripts. It keeps the cutscene holds that scripts and dialogues' runners take, each once, even if two share a request, and `CutsceneRequest` joins their requests, or is null if none is held, which the game reads after the tick (`051 §3.1`, `§3.6`).
- **What controllers see.** It is the `IWorldView` it gives controllers, through each one's `Ready`, at the end of its constructor, once everything is built. The game supplies the player's input and the game's mode with functions, `PlayerInputSource` and `GameModeSource`, which it reads when asked, so it keeps no copy; with none, the player stands idle and the game is always playing, as in tests and probes. It passes the player's input on whatever the mode; the player's controller decides what a cutscene means for it (`050 §2.3`). Before it steps its controllers, on either path, it calls each one's `GameModeChanged` if the mode differs from the one it last stepped them in (`050 §4.3`).
- It also builds the block the player spawns, which no configuration describes, disabled and hidden, with its own look from the asset library, and gives it to the player's controller through `IWorldView`, which enables and shows it to spawn it; it is the last of the world's entities, so `EntitiesOf` leaves it out (`023 §1.1`, `§1.4`).
- It skips an entity that can't process, so that a controller stops with whatever pauses or disables its body: the tree's pause, or the `ProcessMode` of the entity or an ancestor (`013 §1.2`).
- It is given an `IAssetLibrary`, which makes each kind of entity's visual and gives the tile's mesh, so Core doesn't know the game's assets (`012 §3.1`, `020 §2.2`).
- It puts the features the engine holds for each entity, such as its mass, into the engine at the start of the tick after the features change, before any entity steps. It listens to `Features.Changed` only while it is in the tree, so that the event doesn't keep it alive (`029 §2`, `§2.1`, `§2.5`).
- `WorldNode.MapMarkers` draws a box where each entity starts, of the size and colour of its box in LDtk, for comparing the world with its map and for seeing entities with no body, such as respawns and the other control entities. A trigger's marker is its area (`048 §1.8`).

## Open questions

- A route that switches a trigger counts, in the check for cycles, as leading into the trigger's step, so a trigger whose route turns that same trigger off is refused. Allowing it needs each runner built before the steps it runs are resolved (`052 §1.5`).
- Who owns what crosses a room's edge, such as a block pushed through an opening or a robot riding a platform across one, waits for the design of respawn, caching and pausing. A node per room would let Godot's `ProcessMode` pause a room, but entities would then belong to a room's node, which needs that rule, and colliders built per room would stop tiles merging at rooms' edges (`013 §1.1`).
