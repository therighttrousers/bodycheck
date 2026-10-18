# 015: The phase ability

## Context

Inciting event (2026-10-18): work on issue #5, "Design/implement the phase ability", began.

What the design says ([progression.md](../designs/progression.md#ability-phase)): the player teleports to the other side of an object. Its momentum is replaced: a fixed magnitude, in one of eight directions chosen by input. Its position is replaced: a fixed distance from the object's center, in the direction of the new momentum. A movable object is thrown the opposite way, and lighter objects farther. [characters.md](../designs/characters.md) says every NPC can be phased through.

Open in progression.md: what momentum the target gets (to be decided by trying both options), what happens when the destination is blocked, what phasing straight down through a grounded target is for, whether heavy or immovable targets launch the player faster, whether dead NPCs can be phased through, and whether platforms can.

Goals:
- A phase the player can use for traversal and to throw robots, blocks and platforms, in every test room and the LDtk world.
- Both options for the target's momentum, to try.
- A test room for trying it.

Anti-goals: visual or sound effects, animation, damage, stun, turrets, dead NPCs. NPCs that phase.

## 1. Using it

- [x] 1.1 Which input phases? Decision (Neil): as leaning. A new action, `phase`, acting on a press only, like jump: holding it doesn't phase again.
  - Leaning (Claude): Left Shift and J on the keyboard, X (the west face button) on a gamepad.
  - Changed (Neil): Left Shift brought up Windows' offer to turn on Sticky Keys, after it was pressed many times in a row. Jump, phase and boost are now on three keys under each hand: J, K and L for the right, with the arrow keys, and C, X and Z for the left, with W, A, S and D. On a gamepad: A, X and the right trigger.
- [x] 1.2 Which entity is the target? Decision (Neil): (b).
  - (a) Only one the player touches.
  - (b) The nearest one whose box is within a gap of the player's box, e.g. 1 tile.
  - (c) The nearest one in the chosen direction, within a cone.
  - Leaning (Claude): (b). The destination doesn't depend on where the player is, only on the target and the direction, so the direction needn't pick the target too. A gap between boxes, rather than a distance between centers, treats a wide platform like a robot.
- [~] 1.3 Which kinds of entity can be phased through? Leaning (Claude): every kind, as characters.md says, platforms included. Phasing through a platform throws it and leaves its riders behind, which is puzzle material.

## 2. Where the player goes

- [x] 2.1 The direction. Decision (Neil): (a). One of eight, from the horizontal and vertical inputs, the stick's direction snapped to the nearest 45 degrees. With no direction held:
  - (a) The line from the player through the target's center, snapped to eight. This is "the other side" literally.
  - (b) The way the player faces.
  - (c) No phase.
  - Leaning (Claude): (a).
  - Superseded by §5.6: with no direction held, a phase is cancelled.
- [x] 2.2 The distance. Fixed, from the target's center, as progression.md says: 2.5 tiles to start with. That clears the player's box from every entity there is, in every direction: a robot needs 1.8 vertically, a platform 1.8 horizontally.
  - Superseded by §5.1: the player and the target are placed as a pair, at a separation that depends on their sizes.
- [x] 2.3 When is the destination blocked, and what then? Decision (Neil): (a). Blocked: the player's box at the destination overlaps tiles or any other entity, or the line from the target's center to the destination crosses a tile, so that a phase never passes through a wall.
  - (a) Nothing happens: no teleport, no change in momentum, no throw.
  - (b) The player goes to the nearest place that is free.
  - (c) The player goes as far along the line as is free.
  - Leaning (Claude): (a). It is predictable, and (b) and (c) can put the player somewhere it didn't aim, or back inside the target. Telling the player that a phase failed waits for effects.
  - Superseded by §5.2 and §5.3: the pair slides to fit, and a phase still never crosses tiles.

## 3. Momentum

- [~] 3.1 The player's new speed: fixed, 16 tiles per second to start with. Without drag, rising from that speed would climb about 4.5 tiles, as high as the highest jump, from 2.5 tiles above the target's center. Measured (2026-10-18, `--probe=phase`): with drag, and drifting left, the feet rise about 3.6 tiles after the teleport. That reaches a ledge 6 tiles above the floor from a robot standing on it, as the "Phase" room has. The change is one impulse, the player's mass times the difference between the new velocity and the old, so momentum is still tracked.
- [ ] 3.2 What momentum the target gets. To be decided by trying both, so both are built, chosen by the player's tuning:
  - (A) The opposite of the player's new momentum. Every phase throws the same.
  - (B) The opposite of the player's change in momentum. Conserved; the throw depends on how the player was moving.
- [x] 3.3 A phase ends a jump's thrust and coyote time. Expected: contacts would be a step behind the teleport, so that for the tick after a phase the player would have to count as in the air, or brake against the ground it left. Measured (2026-10-18): they aren't. On the tick after a phase up from the floor, the player is already off the ground, so nothing more is needed.

## 4. Building it

- [~] 4.1 Where the code goes. The controller decides whether and where to phase; the entity offers what a body can do: find the entities near it, say whether its box fits at a place, and teleport. The physics queries read the world as of the engine's last step, like everything else a controller sees.
- [~] 4.2 When the teleport happens. Setting a body's position moves it at once, so a controller stepped later in the same tick would see the player where it went, unlike forces and impulses (`005 §3.4`). Leaning (Claude): the entity holds the destination until every entity has been stepped, and sets it then, before the engine's step, so that what controllers decide still doesn't depend on the order they run in.
  - As built: the entity sets its position in its own `_PhysicsProcess`, which Godot runs after the room's, since the room is its parent. It also resets physics interpolation, so the player isn't drawn sliding from where it was to where it went.
  - Not tried: setting the transform from `_IntegrateForces`. Interpolation would have to be reset after the engine's step, which nothing calls back for.

## 5. After playing it

Neil played the first version on 2026-10-18. Kept: phasing through everything, though platforms add a lot of height and are to be watched in tuning; the speed; horizontal phases through robots, which give them a big kick; the teleport deferred until every entity is stepped; the teleport itself, since phase is to get the player past things it couldn't pass before, such as an immovable entity in its way. Reconsidered: what happens when there is no room for the player, which kept phasing down through a robot from throwing it up, or diagonally down from throwing it into a pit; where the target goes; how the distance is chosen; the jarring cut of the teleport; and that the input choosing the direction means something else just before the phase. Ori's bash, which inspired phase, pauses the game while its button is held and acts on release, showing an arrow, so the player has as long as it likes to choose a direction or cancel.

- [x] 5.1 Where the player and the target go. Decision (Neil): they teleport as a pair, keeping their center of mass. Afterwards they are on the line through their center of mass in the phase's direction, the player ahead of it and the target behind, so each moves in proportion to the other's mass: a target twice the player's mass moves a third of the way, and the player two thirds. Each also moves across that line, to meet it, unless the player was head on.
  - Considered: keeping each body's position across the line, and moving only along it. Also keeps the center of mass, but then the result depends on where in reach the player was, and there is no one line to draw an arrow on.
  - The separation along the line is what clears the two boxes in that direction, plus a gap, but at least a minimum. With a minimum of 2.5 tiles, every entity there is gets the separation that §2.2 had; the size matters for larger ones, such as turrets.
- [x] 5.2 When the pair doesn't fit. Decision (Neil): slide it along the line, the nearest first, as far as the separation either way. The center of mass then moves, by the slide. Phasing down through a robot on the floor then swaps them: the player lands where the robot stood, and the robot is put above it, and thrown up. Only if nowhere in that range fits does nothing happen.
- [x] 5.3 What blocks. Decision (Neil): a phase never crosses tiles. Each body's box must fit where it goes, and the line from where it is to where it goes must cross no tiles; other entities don't block the lines.
  - For now, other entities block where a body goes, as tiles do. Letting them overlap and be pushed apart by the engine may do instead, once measured: overlapping bodies, and walkers with continuous collision detection in particular, could be pushed hard.
- [x] 5.4 The cut. Decision (Neil): the teleport stays instant. Continuity is for what is drawn, in a later change: an effect from start to end, or the model following the body over a few frames.
- [x] 5.5 Aiming. Decision (Neil): pressing phase with a target in reach fixes the target, and pauses the game; releasing it fixes the direction, unpauses and phases. How the pause works: (a).
  - (a) The tree's pause, as the menu and the map use, with a new `GameMode`, `Phasing`, in `GameModeManager`. Whatever cares which pause it is asks the manager. Everything that moves stops, animations included. The player's controller isn't stepped then, so something that processes while paused reads the input for it, and the controller doesn't set the pause itself.
  - (b) A freeze of the world's own: it stops stepping entities and the physics space, and keeps stepping the player's controller. Animations keep running.
  - Leaning (Neil and Claude): (a). Decision (Neil): (a).
  - As built: the phase is made on the first tick of play after the release, since nothing moves while paused. The pause starts on the tick after the press, so the world takes one more step first. With nothing pausing the game, as in tests and probes, the player aims while the world runs.
  - Neil's playtest of §5.1 to §5.3 (2026-10-18): works as decided. Phase can pick up a block, by phasing up through it, which may replace a starter ability to pick up blocks. Robots can be juggled, but the second phase's direction is hard to get right while jumping to meet the robot; aiming should help.
- [x] 5.10 How the player's controller runs during the pause for aiming. Under §5.5 (a) it isn't stepped, and the game would have to reach into it for aiming alone.
  - (a) `GameModeManager` drives the controller's aiming directly, through methods for it.
  - (b) Neil: a second path for stepping, beside the normal one, which the manager drives while the world is held: `GameModeManager._PhysicsProcess` → `WorldNode.StepHeld` → each `Entity.StepHeld` → the controller's own method for it. A controller asks to hold the world through its entity, the world says whether anything holds it, and the manager pauses for that and resumes when nothing does. Nothing on the path knows about phase.
  - Decision (Neil): (b), named after the normal path: `WorldNode.StepPaused`, `Entity.StepPaused`, `ApplyPaused`, with `RequestsPause` and `PauseRequested`. "Held" was rejected, since `PlayerInput` uses it for buttons, and "frozen", since a `RigidBody3D` already has `Freeze`, which means something else.
  - Leaning (Claude): (b). It mirrors how controllers run in play, keeps everything about aiming in the controller, and the game has one call and one question to ask of the world. The method can be a default interface method that does nothing, so only the player's controller has one. Other uses later: aiming boost, conversations.
- [x] 5.6 Cancelling. Decision (Neil): releasing phase with no direction held cancels it.
- [x] 5.7 What the player sees while aiming. Decision (Neil): a placeholder arrow and a mark on the target, with the arrow centered on the pair's center of mass, to start with. Real effects are for a later change.
- [x] 5.8 Targeting. It is too easy to get a target in reach in some places and too hard in others. Decision (Neil): put off until the rest of this section is done.
- [x] 5.9 Order: §5.1 to §5.3, then aiming (§5.5 to §5.7) with the placeholder, in this change. Effects and targeting in later ones.
- [ ] 5.11 Found while trying the pause (2026-10-18): reach ignores tiles, so a robot on the other side of a 1-tile bump can be the target, though every phase with it would cross the bump and fails. For targeting (§5.8).
- [x] 5.12 Easing into the pause. Neil: a sudden stop in time is jarring; the game should slow to the pause, over no more than a quarter of a second. Decision (Neil): `GameModeManager` ramps the engine's time scale in a straight line from 1 to 0 over 0.25 real seconds, in a mode of its own, `SlowingForPhase`, with the tree running and the player aiming. Then it pauses as before. Releasing phase, or opening the menu or the map, returns to full speed at once, as Neil believes Ori does.
  - Measured (2026-10-18): the time scale doesn't make physics ticks less frequent. There are still 60 a second, each shorter by the scale, down to a step of 0 at the end, which the engine accepts. So input is read every tick throughout. Over the ramp the world advances about 0.125 seconds of game time.
  - Changed (Neil): the slowdown is 0.5 seconds, and the time scale falls to 0.05, not 0. At 0, now and then a cancelled phase flung something: the player, a robot, or a platform, some of them out of the world. Neil saw no break in continuity when the ramp stopped at 0.1, and halved that. He would rather avoid very small steps in general.
  - Found after (2026-10-18): the flinging was the grab of §5.14, which divided its drift by the step's length. Cancelling at a time scale of 0.0001 left a platform moving at 498 tiles per second. The grab now divides by at least a full tick's length; after the same cancel the platform moves at 2. The floor of 0.05 stays.
  - To watch: whatever counts ticks rather than time, such as coyote time and jump thrust, runs faster in game time while slowed. While aiming the player neither runs nor jumps, so only a phase cancelled during the slowdown sees it.
- [x] 5.13 The pair drifts apart while the game slows down. Found by Neil: with a 0.5-second slowdown, the player phased through platforms about 3 tiles away by the time the game paused. Options:
  - (a) Leave it. Rejected.
  - (b) Freeze the player and the target during the slowdown. It stops the two bodies being watched most closely as suddenly as the pause the slowdown is there to soften.
  - (c) A spring between them during the slowdown. It would draw them together, which would also shorten the teleport.
  - (d) A grab: on the press, both get the velocity of their center of mass, by an impulse each, as in a perfectly inelastic collision. Momentum is conserved, nothing is left to remove afterwards, and it is the limit of a stiff, damped spring. A body that something blocks, such as a target on the floor with the player falling onto it, still closes in a little. A cancelled phase leaves the pair moving together, a small tug on the target.
  - (e) Cancel when the target has left reach by the release. As a backstop only: fast juggling drifts out of reach often.
  - Decision (Neil): (d) to start. A spring that draws them together is still wanted if the slowdown becomes 0.5 seconds, which is likely; the slowdown is 0.25 seconds for now.
  - Later (Neil): the slowdown is 0.5 seconds (§5.12), and the grab holds every tick (§5.14).
- [x] 5.14 The grab doesn't hold. Found by Neil: jumping up to a platform and aiming at it, the player drops while the game slows down, since gravity still pulls on it and the platform holds its height: about a quarter of a tile with a 0.25-second slowdown, and a tile with 0.5 seconds. Anything else could push one of the pair after the grab too, such as a flying block. Decision (Neil): adjust the velocities every tick, inside `PlayerController`, so that there is no spring or joint whose lifetime must be managed.
  - As built: on every tick of play while a phase is aimed, the pair get the velocity of their center of mass, plus equal and opposite impulses that take back, over the step, how far they have drifted from where they were from each other at the press. Over the step, but never over less than a tick's length at full speed: see §5.12. Momentum is conserved. Matching velocities alone left the player 0.27 tiles low after 30 ticks beside a platform, from what forces do within each step; with the drift taken back, it stays within 0.05.
  - A pull that draws them together, if the slowdown grows (§5.13), would change the held offset over time.
- [x] 5.15 Grabbing while held (Neil): holding phase and then coming within reach of an entity grabs it, as in Ori. Unlike Ori, with no limit on how long phase may be held first.
  - As built (Neil): a phase is aimed whenever phase is held, a target is in reach, and a cooldown of 0.5 seconds since the last aim began is over. Without the cooldown, mashing phase while falling beside a platform let the player ride it across the screen. The cooldown counts game time, so it runs slowly while the game slows down, and not at all while paused.
  - Reach is 0.25 tiles, down from 1 (Neil): with aiming paused and grabbing while held, targeting is much easier, and a nearer target is more often the intended one.
  - Holding phase stops looking up and down (Neil), as moving and jumping do (`014 §1.1`).
