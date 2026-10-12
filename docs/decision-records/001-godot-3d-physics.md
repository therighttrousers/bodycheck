# 001: Godot's 3D physics engine instead of our own simulation

## Context

Inciting event: `000 §1.1` left a work item to investigate running the game on Godot's 3D physics engine, with gameplay locked to one plane, instead of the 2D simulation in `BodyCheck.Core`.

Goal: decide whether to switch, by trying it.

For switching:
- We get the features, work and bug fixes that have gone into the engine. A new physics-based puzzle idea is less likely to need our own engine extended first.

Risks (Neil):
- We could lose too much control over tuning player movement, and might not recover the current feel using forces and impulses.
- Unit testing could become too slow or cumbersome, because the physics engine exists only inside a running Godot process.
- The 3D engine might be a poor fit for 2.5D even with axes and rotations locked.

State of our own simulation on 2026-10-11: about 350 lines (`World`, `Body`, `TileGrid`), boxes only. Known limits: bodies move one at a time in list order, so a pusher can trail by a tick; stacks are settled approximately, by repeating the traction pass.

Facts checked on 2026-10-11:
- Jolt is built into Godot 4.7.2 and is the default 3D engine for projects created in the editor since 4.6. `game/project.godot` was written by hand and didn't name an engine, and the seam probe (item 1.2) showed that it then runs the older Godot Physics. The spike branch sets `3d/physics_engine="Jolt Physics"`.
- Stock Godot can't step a physics space by hand. The pull request that adds it (godotengine/godot#76462) has been open since 2023 and the maintainers declined to merge it in December 2025; a smaller alternative (#116230) is also open.
- Jolt removes ghost collisions at seams between shapes of one body, and in concave meshes, but not between different bodies.
- Jolt's reported contact impulses are estimates, accurate only when the two bodies touch nothing else.

## 1. Body type

- [x] 1.1 Can the player be a `CharacterBody3D`? Decision: no. The spike uses `RigidBody3D`.
  - It is kinematic: it has no mass, we set its velocity directly, rigid bodies can't push it, and it pushes them as if it were immovable.
  - Its moving-platform support carries the rider kinematically, with no force on the platform. That is `000 §2.2.1` option B, which was rejected.
  - It conflicts with three principles: momentum is tracked and never edited, a force between two bodies pushes both, and enemies can push and stand on the player.
  - So the candidate is `RigidBody3D`: linear Z and all rotation locked, a box shape, and our controllers applying forces and impulses from `_IntegrateForces`. This is `000 §1.5` option A.
- [ ] 1.2 Tile colliders. A box sliding over a seam between two tile bodies can catch on it. Options: all tiles as shapes of one static body, relying on Jolt's internal edge removal; merge runs of tiles into larger boxes; one concave mesh for the level.
  - Seam probe, 2026-10-11 (`--probe=seams`): a player-sized box slid at 16 tiles per second over 25 tiles of flat floor, with no friction. On Jolt, with all tiles as shapes of one static body, nothing disturbs it, whether each tile is its own shape or runs are merged into larger boxes. On Godot Physics the same single-tile layout catches: the box lost 5.2 tiles per second in one tick and hopped 0.16 tiles. Merged boxes slide cleanly on both.
  - Wall seams (Neil, 2026-10-11): the player can land on a seam partway down a vertical wall. Not yet reproduced by a probe. The merge joins tiles along rows first, so every seam it leaves is horizontal and ends on a wall. Boxes can't fix this: splitting an L-shaped region into boxes always leaves a seam that ends on a flat face, and the merge order only chooses whether that face is a wall or a floor. The candidate fix is the third option above, one concave mesh made of the exposed tile faces. Not to be fixed yet.
  - The spike merges runs by default. Still to be tried: walls, ceilings, landing on a seam, and seams between tiles and other bodies.
- [~] 1.3 Corners and ledges. Leaning: Jolt's collision margin set to zero, which the spike branch does. Landing on a ledge is still unreliable.
  - Report (Neil, 2026-10-11): after running into a two-tile wall and doing a full jump beside it, the player is sometimes knocked left by the wall's top corner on the way down.
  - Drop probe (`--probe=drop`): drops the player onto the wall's top corner with its edge overhanging the wall by 0.01 to 0.32 tiles, from six heights. With Jolt's defaults there are three outcomes: it lands; it is thrown left at 6 to 8 tiles per second; or it sinks past the corner and is eased out sideways with no speed.
  - Cause of the throw: Jolt rounds the corners of every box by its collision margin, 8% of the box's smallest side, which is 0.064 tiles for the player. With an overhang below about 0.08, a round corner lands on a round corner and slides off.
  - Cause of the sinking: collisions are tested once per tick, and the player falls about a quarter of a tile per tick. If it is already deeper in the wall vertically than sideways when the overlap is found, it is pushed out sideways.
  - A body pushed into a wall rests 0.02 tiles inside it, so a jump beside a wall always comes down with a small overhang.
  - Tried:
    - Collision margin 0 (sharp corners): no throw in any drop, and none in Neil's case. Sinking remains: overhangs of 0.08 and 0.16 land in half the drops, depending on where the ticks fall, smaller ones almost never, and a drop from 8 tiles misses even at 0.32. With one collider per tile, seams catch again (merged tiles stay clean).
    - Speculative contact distance raised from 0.02 to 0.3 or 1.0, with margin 0: every drop lands, and the wall penetration goes away. But a body falling flush beside the wall, not overlapping it, is thrown left at 3.4 tiles per second by the corner. Rejected for that.
    - Continuous collision detection on the player: no change.
  - By hand (Neil, 2026-10-11), with margin 0: corners are hit less often, and a hit ends in a landing instead of a throw. At 120 physics ticks per second, with the tick-count tunings doubled, corners are hit less often still. Neil could possibly live with this.
  - A capsule instead of a box (Neil): thrown off corners often, even with margin 0. Rejected.
  - Settings found by Neil, 2026-10-12, now on the spike branch: collision margin fraction 0.005, penetration slop 0, continuous collision detection on every `JoltBody`, and its maximum penetration 0. (Continuous detection made no difference on 2026-10-11 because that last setting was at its default.)
  - Probes under those settings: bodies rest exactly on floors and against walls, with no penetration. Seams are clean with merged tiles and with one collider per tile. Neil's wall case comes down cleanly. No drop is thrown. Drops from 4.5 and 8 tiles always land. Drops from 2.0 to 2.3 tiles with an overhang of 0.16 or less still sink past the corner in 12 of 20 cases.
  - Why the slower drops still sink: Jolt uses continuous detection only when a body moves more than a set fraction of its size in one tick, 0.75 by default. For the player that is about 18 tiles per second, which a fall of about 3 tiles reaches.
  - Tried: lowering that fraction to 0.1 or 0. Every drop lands, but a body falling flush beside the wall is thrown left at 2.9 tiles per second by the corner, and with one collider per tile a sliding body doesn't move at all. Rejected.
  - By hand (Neil), same settings: no ghost collisions seen; no snag on a shared edge even with one collider per tile; the player no longer falls into a one-tile gap at top speed; landings and wall strikes look more solid. Concerns to watch over longer testing: body stability and simulation time.
  - Core lands on any overhang, because it moves along x and then y and stops at the first face.
  - Still open: making ledge landings reliable. Not yet tried: more physics ticks per second; a cap on falling speed; our own downward sweep before the step. To be watched: Jolt's documentation ties the margin to the speed and robustness of its collision detection.
- [~] 1.4 Which bodies get which treatment. Proposed by Neil, 2026-10-12; Claude agrees, with the two additions below.
  - Walking entities get continuous collision detection and engine friction 0, and manage their own grip through traction.
  - Other bodies, such as stacks to topple or rocks to push onto platforms, use engine friction and ordinary collision detection.
  - Addition (Claude): no body is ever `Rough`. The engine takes the smaller of two bodies' friction unless one is rough, so a walker's zero then wins on every contact it has, and the wall and landing problems of item 2.1 can't come back through a rock.
  - Addition (Claude): a hovering platform isn't a walker. It uses no traction, and with friction 0 a rock resting on it would slide off as soon as it moves. Platforms should have engine friction.
  - Untested: all of the above. The spike has no body with engine friction yet.
  - Tiles stay merged along rows, for speed and so that a snag on a floor seam is impossible.

## 2. Movement feel

- [~] 2.1 Traction (`000 §2.5`). Leaning B, as built on 2026-10-11; see the end of this item. Today it is a stage inside the step, after forces are integrated, and it cancels relative velocity exactly. An engine has no place to insert such a stage.
  - Option A: engine friction through physics materials. Grip then depends on load, which answers an open question in movement.md. It can't express a target speed, so running stays a separate force and can fight friction again.
  - Option B: keep our traction, applied from `_IntegrateForces` using the last step's contacts, with engine friction set to zero. To stay exact it must come after every other horizontal force, so we would apply drag ourselves instead of using the engine's damping. Impulses from the contact solver in the same tick would still be unaccounted for.
  - Tried on 2026-10-11 (Neil): A, with friction 4 on the player (`Rough`, so the larger coefficient wins), 0.5 on tiles, and a ground drive raised by friction × weight to cancel it. On open floor it feels very close to the Core version, and top speed is the same 15 tiles per second (drive against drag; `TopSpeed` is where the drive reaches zero).
  - Player probe on that version (`--probe=player`), same day:
    - Friction acts on every face of the box, walls included. A jump while pushing into a wall doesn't leave the ground. Neil saw the same when jumping onto platforms.
    - The friction correction uses the last step's floor contact, so it keeps pushing for two ticks after a jump, when there is no friction to cancel. A jump at top speed goes from 15 to 21.5 tiles per second.
    - Reversing brakes at about 650 force units against Core's 440, because the correction adds to friction instead of cancelling it.
  - Godot gives a body one physics material, so the floor and the walls can't have different friction.
  - Landing (Neil): with friction above zero, landing from a jump at an angle takes away almost all horizontal speed. Less so at 0.5, and not at all at 0. Claude's explanation: friction can deliver up to the coefficient times the landing impulse in the same tick, and at 4 that is more than the player's whole horizontal momentum.
  - Conclusion: engine friction has to be zero for the player.
  - As built: B, in `JoltWorld.Step`, which is Core's traction stage unchanged. Bodies collect their forces and impulses for the tick (`AddForce`, `AddImpulse`) instead of passing them to the engine. The step adds drag, works out the velocity those give, applies traction to that, and hands the engine one impulse per body. The engine adds gravity, resolves contacts and moves the bodies.
  - Player probe on B: a jump while pushing into a wall is the same as one without (apex 2.21 tiles from a tap); a jump at top speed keeps exactly 15 tiles per second; reversing brakes at Core's rate. Not yet tried: riding, where traction pushes back on a support, and stacks.
  - Riding probe, 2026-10-12 (`--probe=ride-robot`, `--probe=ride-platform`), with Neil's robot and platform controllers and the settings of item 1.3:
    - Robot: an idle rider moved 0.0000 tiles relative to the robot over 30 seconds and 100 tiles of travel, with no overlap and no tick unsupported. A tap jump from the robot reaches 2.209 tiles against 2.210 from tiles, and keeps the robot's speed. These are the `000 §2.2` criteria, except for pushing from the side, which is not measured yet.
    - Platform: settles 0.250 tiles low under the rider (Core: 0.25), dips 0.38 on a landing from 3.2 tiles (Core: 0.5 from 3), and doesn't overshoot (Core: about 0.1). The rider slides up to 0.19 tiles when the platform is stopped dead by a wall, and ends 30 seconds within 0.003 tiles of where it stood.
    - The platform controller first passed its thrust straight to the engine. Traction then couldn't see it: the rider lagged the platform by 0.03 tiles per second and slid 0.4 tiles at each turn. Using `AddForce` fixed both.
  - The floor contact that the engine reports lags by a tick more than Core's: it is still reported on the first tick after take-off. Traction aims at a target speed, so it can't overshoot because of this.
- [ ] 2.2 Exactness. Engine contacts are soft: resting bodies overlap slightly and are corrected over several ticks. "Never overlap" and "exactly where it started after 30 seconds" become tolerances. How loose is acceptable?
  - First data, from the seam probe: on Jolt a resting box sits at exactly the floor's height (to four decimals). On Godot Physics it sits 0.01 tiles inside the floor.
  - That exactness came from placing the box exactly on the floor. After landing from a jump, the box rests 0.02 tiles inside the floor on Jolt, and stays there. To be checked: whether this is Jolt's allowed penetration, and whether its setting can be lowered safely.
- [ ] 2.3 Do the player controller's numbers carry over? Run, jump and coyote logic are forces and impulses on a body already, so the controllers should port with few changes. To be measured: jump heights, top speed, the one-tile gap.

## 3. Testing

- [~] 3.1 How are physics-dependent tests run? Leaning A, gdUnit4Net, which works on the spike branch as of 2026-10-12.
  - Option A: gdUnit4Net. Version 5 runs plain tests without Godot and starts the engine only for tests that ask for it. Works with `dotnet test`.
  - Option B: Chickensoft GoDotTest. Runs C# tests inside Godot. Not tried.
  - Option C: our own. A headless Godot run of a test scene that reports through its exit code. The spike's probes are this, without the exit code.
  - Speed: Godot's `--fixed-fps 60` flag makes a headless run tick as fast as the machine allows, with the same results. The riding probe's 2,520 ticks take 42 seconds without it and 0.74 seconds with it, engine start included.
  - First built for A with the gdUnit4Net packages in `game/BodyCheck.csproj` and the tests under `game/`. `.runsettings` passes `--headless --fixed-fps 60` to the engine and points at the engine that `dev-setup.ps1` installs, so plain `dotnet test` runs Core's tests and these together.
  - Result: `game/spike/tests/RidingTest.cs` holds Core's three riding tests, rewritten against the engine. They pass in about 0.45 seconds, and `dotnet test` for the whole solution takes about 6 seconds, most of it start-up. A failed assertion reports its message and source line.
  - A test loads the spike scene with a room and an input source, then waits for physics ticks one at a time. Test methods are `async`.
  - Moved out of `game/` on 2026-10-12, at Neil's request: `tests/BodyCheck.Game.Tests` is its own project, references the game, and holds the gdUnit4Net packages, so the game has no test code or test packages. gdUnit4Net starts Godot in the test project's directory, so that directory has to be a Godot project. Its `project.godot` is copied from the game's on every build, with the assembly name changed, and is ignored by Git. The tests therefore run under the game's physics settings.
  - Limit of the separate project: the game's scenes and assets can't be loaded by `res://` path from it. The spike scene is built in code, so nothing needs that yet. If tests come to need the game's files, the fallback is to compile test sources kept outside `game/` into the game's assembly, for non-export builds only.
  - Neil's position on the costs (2026-10-12): being outside the stated version support is acceptable for now; generated files are fine; a failed run when the engine is missing is the right behaviour.
  - Costs found:
    - The packages are prerelease in part (`gdUnit4.api` 5.1.0-rc5), and state support for .NET 8 and 9 and Godot 4.4 to 4.5. They work on .NET 10 and Godot 4.7.2 here.
    - Each run writes a runner scene to `gdunit4_testadapter_v5/` in the test project, ignored by Git and by the formatter.
    - If the engine isn't found, the tests are reported as "no test is available" and the run fails.
    - The test run starts the Godot editor to build, and the editor re-indents any C# file open in its script editor with tabs. Fixed for everyone by `[editor_overrides]` in `game/project.godot`, which sets the editor's indent type to spaces for this project.
  - Later (Neil): a dedicated scene for tests instead of the spike's main scene, which will force that scene to be split into smaller reusable parts.
  - To be checked: tests in VS Code's test explorer; the run in CI.
- [ ] 3.2 Which of today's tests survive? `WorldTests` and `BodyCollisionTests` test our engine and would go. The player, riding and traction tests state design goals and would become in-engine tests.

## 4. Alternatives to Godot 3D physics

- [ ] 4.1 Godot's 2D physics, drawn in 3D as now. Removes the 2.5D fit risk. It is a different engine from Jolt, and has the same testing cost. Held in reserve in case the 3D engine fits 2.5D poorly.
  - Untested belief (Claude): it is not cumbersome. Godot runs its 2D and 3D physics servers side by side, so 2D bodies can sit in the same scene as the 3D nodes that draw them, which copy their positions as they copy Core's today. Differences to absorb: y points down, and the defaults assume pixels as units.
- [x] 4.2 Jolt used directly from `BodyCheck.Core` through C# bindings, with no Godot. Keeps plain unit tests and stepping by hand. Loses Godot's physics nodes, editor support and debug drawing, and adds a native dependency. To be checked: the state of the bindings.
  - Decision: not unless Jolt's own interface turns out to give us something that Godot's doesn't.

## 5. The spike

- [x] 5.1 Scope and exit criteria. Decision: as proposed, on branch `spike/jolt-physics`, in `game/spike/`. Claude sets up the scene and tile colliders; Neil ports `PlayerController`. The proposal: a branch, with `BodyCheck.Core` left as it is, and the same rooms run on both engines. Measure, in this order:
  1. Player on tiles: jump heights (just over 2 and 4 tiles), top speed, catching on tile seams, the one-tile gap.
  2. Riding (`000 §2.2` criteria): drift of an idle rider over 30 seconds, jump height from a robot, robot velocity kept on leaving, pushing from either side.
  3. Hovering platform: sag under a rider, dip on landing (`000 §2.4` numbers).
  4. Test harness: one riding test under each of 3.1's options, with its run time.
