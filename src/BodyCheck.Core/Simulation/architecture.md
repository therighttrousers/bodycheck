# Simulation

Relevant decision records:
- [012: Subsystems in `BodyCheck.Core`](../../../docs/decision-records/012-core-subsystems.md)
- [013: The world as a container of rooms](../../../docs/decision-records/013-world-of-rooms.md)
- [020: Drawing rooms](../../../docs/decision-records/020-drawing-rooms.md)
- [022: Actions at the end of a tick](../../../docs/decision-records/022-end-of-tick-actions.md)
- [023: The player's spawned block](../../../docs/decision-records/023-spawned-block.md)
- [029: Tunable bodies](../../../docs/decision-records/029-tunable-bodies.md)
- [043: Triggers, actions and scripts](../../../docs/decision-records/043-triggers-and-scripts.md)
- [045: Control entities in the world](../../../docs/decision-records/045-control-entities-in-the-world.md)
- [046: Actions as async methods](../../../docs/decision-records/046-async-actions.md)
- [047: `Once`, and routing actions](../../../docs/decision-records/047-once-and-routing.md)
- [048: Triggers set off by touch](../../../docs/decision-records/048-touch.md)

## What it is

The world brought to life: its tiles turned into colliders and drawn room by room, its entities created where they start, each given its controller, and all of them stepped every physics tick. Tests use it as it is, so it has no camera, lights for the scene or input device of its own. The game gives it what to draw for each kind of entity and for tiles, and where the player's input comes from, so it knows no assets and no devices.

## What it is not for

Presentation: the camera, the scene's lighting, overlays, the rendering pipeline. Choosing or switching worlds. The rules of a single entity or controller.

## `WorldNode`

`WorldNode` is one world, simulated: tiles and entities, with no camera, lights or input of its own.

- It builds the colliders from the world's grid, as one static body (`020 §3.1`), and a `RoomNode` for each room, which draws the room's tiles as a few multimeshes (`020 §2.1`).
- It builds an entity from each of the world's `Bodies`, gives each its looks and its controller, and steps every one each physics tick, in that order, so that `EntitiesOf` finds them by their configurations. A vertical door is frozen where it starts, with no controller yet (`043 §6`). It runs the actions controllers defer, at the points [Core's doc](../architecture.md#how-a-tick-runs) describes (`022 §2.1`).
- It has a `TickScheduler`, which it gives as its `Tick` to what runs in it, and runs once in each played tick, after the entity steps' deferred actions, while controllers are enabled, with the tick's game time, so that waits count game time; paused ticks don't run it (`046 §1.2`).
- **Routes.** Once every entity has its controller, it builds a runner for each control entity that runs others or responds to actions, from its configuration, with each step resolved to what runs it: a control entity's runner, or a body's controller. Each runner is built after its targets, which the world's having no cycles allows. `Triggers` holds the triggers' runners by id; the others are reached through their routes (`045 §3.3`, `047 §2.3`). A target that can't run its action yet finishes at once, so that the levels load and play: a `Dialogue`, a `Respawn`, which becomes current by reach, and a vertical door, which has no controller (`047 §2.4`).
- **Touches.** It builds an `Area3D` for each trigger, of its box in LDtk, on the `Trigger` layer, masked to the player's. In each played tick while controllers are enabled, just before it runs its scheduler, it asks each trigger, in the order of the world's controls, whether its area holds the player, as of the last step, and sets it off if it does and didn't when last asked, so that what a touch starts runs on in that tick's run (`048 §1.1`, `§1.2`, `§1.4`). Only the player sets a trigger off, for now (`048 §1.3`). A trigger has held nothing before it is first asked, so a player that starts in one sets it off (`048 §1.5`). `TriggerTouched` is raised for each touch. An area finds what is in it at the end of each step, so a body is missed if it moves more in a tick than its width and the trigger's together (`048 §1.7`).
- It also builds the block the player spawns, which no configuration describes, disabled and hidden, with its own look from the asset library, and gives it to the player's controller, which enables and shows it to spawn it; it is the last of the world's entities, so `EntitiesOf` leaves it out (`023 §1.1`, `§1.4`).
- It skips an entity that can't process, so that a controller stops with whatever pauses or disables its body: the tree's pause, or the `ProcessMode` of the entity or an ancestor (`013 §1.2`).
- It is given an `IAssetLibrary`, which makes each kind of entity's visual and gives the tile's mesh, so Core doesn't know the game's assets (`012 §3.1`, `020 §2.2`).
- It puts the features the engine holds for each entity, such as its mass, into the engine at the start of the tick after the features change, before any entity steps. It listens to `Features.Changed` only while it is in the tree, so that the event doesn't keep it alive (`029 §2`, `§2.1`, `§2.5`).
- `WorldNode.MapMarkers` draws a box where each entity starts, of the size and colour of its box in LDtk, for comparing the world with its map and for seeing entities with no body, such as respawns and the other control entities. A trigger's marker is its area (`048 §1.8`).

## Open questions

- Who owns what crosses a room's edge, such as a block pushed through an opening or a robot riding a platform across one, waits for the design of respawn, caching and pausing. A node per room would let Godot's `ProcessMode` pause a room, but entities would then belong to a room's node, which needs that rule, and colliders built per room would stop tiles merging at rooms' edges (`013 §1.1`).
