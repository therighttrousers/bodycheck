# 000: Initial design

## Context

Neil wants to get back into C# by making a 2D metroidvania (code name: Body Check). The starting point is [initial-ideas.md](../designs/initial-ideas.md), which will be dissolved into proper design docs as items here settle.

Goals:
- Decide enough tech to start a graybox prototype: run and jump in a simple tile-based world, then rideable enemies, then boost.
- Tighten the core mechanics (boost, bash, lasers) where the initial ideas leave gaps.

Tensions:
- Neil is new to Godot and its ecosystem, so tech items need the options explained, not just named.
- Progression makes traversal harder, while failure is meant to be cheap. Frequent failure is acceptable as long as it is cheap and not too frequent. To be watched during playtesting.

Art is basic shapes until the mechanics are proven.

Names: this record uses the abilities' original names throughout. Item 4.4 renamed them: what is called "boost" here is now "phase", and what is called "bash" here is now "boost".

Closed on 2026-10-11. Outcomes and unresolved items are in the design docs: [high-level-design.md](../designs/high-level-design.md), [movement.md](../designs/movement.md) and [progression.md](../designs/progression.md).

## 1. Tech

- [x] 1.1 2D or 3D physics and scene architecture. Decision: B as built, for now. Neil will make a work item to investigate A with Godot's 3D physics engine.
  - Option A: fully 3D, gameplay locked to one plane. One world for collision, lighting and rendering. Tile authoring uses GridMap, which is cruder than the 2D tile editor.
  - Option B: 2D physics with a 3D render layer. Better tile editor, but a 2D collision world and a 3D visual world must be kept in sync at runtime.
  - Was leaning A. Then 1.5 put the simulation in the Core library, which is 2D, with Godot drawing it in 3D. That is B, and the sync it was rejected for turned out to be one line: each node copies its body's position every tick.
- [x] 1.2 Tile authoring. Level design will become the biggest cost, so it should get the best tools available. Decision: C, with LDtk. Neil tried it on 2026-10-11, likes it well enough to keep going, and committed a one-level example map.
  - Option A: GridMap by hand. No autotiling, no per-tile data, weak painting tools.
  - Option B: our own autotiler on top of GridMap.
  - Option C: author in 2D and bake to 3D. The 2D source is either Godot's own tile editor or an external editor such as LDtk.
  - Where the bake runs: a Godot import plugin was the suggestion before 1.5. Now the simulation needs the level data too, so Claude suggested reading the LDtk file in the Core library into a `Room`, and building the 3D scene from the `Room` at load as the game does today.
  - [x] Decision: load the LDtk JSON at runtime at first. Move to an asset pipeline if the loading code turns out too slow, too complex or too fragile (e.g. full of magic numbers and hacks). No loader is to be written yet; the map files are in the repo for now.
  - Editor status, checked 2026-10-06:
    - LDtk: last release 1.5.3, January 2025. The repository still gets commits, but in 2026 they are build and CI fixes only. Feature-complete and stable rather than evolving.
    - Tiled: actively developed. 1.12.0 in March 2026, 1.12.2 in May 2026.
    - Godot importers exist for both (LDtk Importer, GDScript; YATI for Tiled, GDScript and C#). Both produce 2D tile scenes, so the 3D bake is ours to write either way.
- [x] 1.3 Godot version and C# project layout. Neil confirmed the library split (he values unit testing) and will use VS Code.
  - Godot 4.7.2 (current stable, August 2026), .NET build, Forward+ renderer. Desktop only.
  - .NET 10 SDK (current LTS). Godot needs .NET 8 or later, and .NET 8 leaves support in November 2026. All three projects target `net10.0`, and Godot 4.7.2 builds and runs the game on it.
  - Godot project in `game/`, so Godot doesn't scan `docs/` and other non-game files. Scenes and their scripts sit together by feature.
  - Game rules (movement integration, collision response, boost and bash maths, the level baker's tile logic) in a plain class library with no Godot dependency, with a unit test project beside it. Godot node scripts stay thin and call into it. Cost: the library can't use Godot's vector types, so values are converted at the boundary.
  - How much lives in the library depends on 1.5.
  - The engine itself (editor and runtime) is installed separately from the NuGet packages, which hold only the C# bindings and build support. The `Godot.NET.Sdk` version in the project file must match the installed engine version.
- [x] 1.4 Camera tooling for one seamless map (e.g. Phantom Camera, or our own). Decision: Phantom Camera for now; Neil strongly prefers out-of-the-box tools. Revisit if it doesn't work out.
  - Checked 2026-10-10: actively maintained (v0.11.0.3, with a fix for a Godot 4.7.1 change). To be checked: use from C#, and camera limits in 3D. Its limit, lookahead and newer framed-follow features are described for the 2D camera only, and the game draws in 3D.
- [x] 1.5 Movement model: who integrates velocity (us or the physics engine), and what "Newtonian" means in code. Decision: option C. We choose every number and every exception, contacts are exact, and the rules are unit-testable.
  - Option A: engine rigid bodies. The engine integrates and resolves everything. Least code, least control over platformer feel.
  - Option B: engine character bodies. We compute velocity; the engine moves the body and reports what it hit. Body-against-body response (riding, pushing, knockback by mass) is ours to add on top, and is the awkward part.
  - Option C: our own simulation in the Core library. Axis-aligned boxes against the tile grid and each other, on a fixed tick. Godot only draws the result. Fully unit-testable and deterministic; no slopes or odd shapes unless we write them.
  - "Newtonian" in code: forces (gravity, run drive, friction, drag) accumulate into acceleration each tick; top speed emerges from drive against drag; boost and bash are impulses.
- [x] 1.7 Momentum is properly tracked as characters move. Feel cheats must be formulated so that this stays true.
  - [x] Rule: a cheat is always a force or an impulse, never a direct edit to velocity. A body's momentum is then always the sum of the impulses it has received. Neil: velocity edits are bad, and nothing has needed one yet.
- [x] 1.8 Variable jump height. Neil wants it; the mechanism is to be found by trying. As built: the first mechanism below, with thrust at 0.9 of weight for up to 12 ticks, and the impulse sized so that coyote jumps reach the same height.
  - First to try: an impulse upward, then an invisible thrust equal to gravity for as long as the jump button is held, up to a maximum time. The player rises at constant speed while holding, then follows a normal arc.
  - Variant: make the thrust a fraction of gravity (0 to 1), so that holding slows the deceleration instead of cancelling it. One parameter covers both.
  - [x] Does the jump impulse push back on what the player stands on? Under Newton's third law, jumping off a robot pushes the robot down. Decision: yes, see 2.2.2.
- [~] 1.6 Low-res rendering approach (e.g. small viewport, nearest-neighbour upscale).
  - Neil's target look: Star Fox era SNES 3D, but with modern features such as anti-aliasing, shadows, transparency, ambient occlusion, refraction, reflection and subsurface scattering. Something that would have astonished him as a teenager in 1993 while still looking like somewhat believable 1993 technology.
  - To try: render at twice the target size (e.g. 640x480), average 2x2 blocks down to the target (e.g. 320x240), quantize to a fixed palette or a fixed number of bits per color channel, then upscale with nearest-neighbour. Hardware anti-aliasing may be a better way to get the first two steps.

## 2. Prototyping order

- [x] 2.1 First prototype is the player running and jumping in a simple tile-based world. Solid, rideable enemies come next, before any art, because they are the riskiest engineering item.
- [x] 2.2 Rideable, pushable enemies: scope of the prototype and what counts as success. Neil accepted the criteria and 2.2.1 to 2.2.4 as proposed, noting it is more elaborate than he had in mind.
  - Success criteria:
    - Standing idle on a patrolling robot, the player is carried without drifting or jittering, including when the robot turns round.
    - Running and jumping on a robot feel the same as on the ground, measured relative to the robot.
    - Jumping or walking off a robot keeps the robot's horizontal velocity.
    - Player and robot never overlap, whichever of them moves into the other.
  - [x] 2.2.1 How is the rider carried? Decision: A.
    - Option A: friction relative to the support. The player's ground friction and drive act on velocity relative to whatever the player stands on (zero for tiles). Fits 1.7: the rider gains the robot's velocity through impulses.
    - Option B: kinematic carry. The rider's position is moved along with the robot each tick. The usual platformer approach; simpler and rigid, but it edits position outside the force model and the rider has no momentum of its own until it leaves.
  - [x] 2.2.2 Do the rider's forces push back on the robot? Decision: yes, and tune the robot's grip. With equal masses, running on a robot would shove it backwards unless its grip on the floor resists. Under A this comes for free by applying each friction, drive and jump impulse to both bodies. Also settles the open sub-item of 1.8.
  - [x] 2.2.3 What happens when the player runs into a robot's side, or a robot walks into the player? Decision: no bounce; momentum is shared, so each can push the other, and the robot's grip and drive decide how easily. Bash adds the billiard-style bounce later.
  - [x] 2.2.4 What happens when a robot pushes the player against a wall? Decision for now: the robot is blocked and turns round, as it does at a wall.
- [x] 2.3 A library of named test rooms, which design docs and tests can refer to by name. The room from prototype 1 is the first ("Playground").
- [x] 2.4 Horizontally moving platforms (Neil's idea). Decision: B, written by Neil as `PlatformController` with a PID controller on height. Probe on 2026-10-10: a player-mass rider makes it rest 0.25 tiles low, a landing from 3 tiles dips it 0.5 tiles, and it overshoots by about 0.1 tiles on the way back. Original framing: a patrolling robot and a moving platform are nearly the same thing, except that a platform has no floor under it.
  - Option A: pin the platform's y coordinate. Adds an "immovable along this axis" concept to Body and World. The platform takes a rider's weight and landings without reacting, like a tile.
  - Option B: the platform is an ordinary body whose controller uses thrust to hold its height (a PD controller, which Neil would like to write). World needs nothing new: a rider's weight already reaches the platform through contact. The platform sags under load and dips on landing unless the controller compensates.
  - Claude recommends B: it keeps one kind of body, and a platform with finite mass can be blocked, so it can't crush the player against a wall.
- [x] 2.5 Where friction lives, and how it is modelled. Decision: a traction stage in `World.Step`, described at the end of this item. Neil's first version has each controller call `Body.ApplySupportFriction` before the step, with separate tile and support friction coefficients per character.
  - Observed (2026-10-10 probe): an idle rider on a robot travelling in a straight line slides off within 10 seconds; a tap jump from a robot reaches 1.5 tiles against 2.4 from tiles; running on a robot gains under half the speed it does on tiles in the same time.
  - Causes: friction is computed before the tick's other forces are known, so it lags steady forces such as drag; it acts on the full relative velocity, including the vertical part; it opposes the drive on robots but not on tiles; and a body resting on another keeps a small downward velocity (a limit of the collision code).
  - Option A (Claude recommends): a contact stage in `World.Step`, after forces are integrated. For each body on a support or on tiles, apply the horizontal impulse that would cancel the relative velocity, limited by coefficient × normal force × dt. Sticking (static friction) falls out when the needed impulse is within the limit. Tiles are a support with zero velocity and infinite mass, so one mechanism covers both. A driving character opts out for that tick, as tile friction does today.
  - Option B: keep friction in the controllers and patch the causes one by one.
  - Coefficients: per-material static and kinetic values with combining rules are the general solution. Claude suggests one grip value per body and one for tiles until a surface needs more.
  - Rejected (Neil's suggestion): apply ground friction always, whatever the drive, and retune to compensate. It unifies tiles and bodies, but the tuning values stop meaning what they say, and a low-friction surface would make a character faster instead of giving it less grip.
  - As built: A, with the opt-out replaced by a target. Each body has a `Traction`: a target speed relative to its ground and a maximum force. The stage applies the horizontal impulse that takes the body towards its target, limited by that force, with the opposite impulse on its support. Braking is a target of zero; running is a target of top speed with the drive force as the limit, so drive and friction are one mechanism. The limit comes from the controller, not from the load on the contact. Stacks are settled by repeating the pass, so bodies below the top of a stack are matched only approximately.
  - Result (2026-10-10 probe): an idle rider is exactly where it started after 30 seconds on a robot; running on tiles is unchanged to three decimals; a tap jump from a robot matches one from tiles.

## 3. Boost

- [x] 3.1 Boost throws the target backwards, opposite to the player's exit direction. This is intended. Example: jump near a spike pit and boost diagonally down through an enemy to lob it up and into the pit.
- [ ] 3.2 Boosting straight down through a grounded target: the player ends up on the floor and the target goes up. What is it good for? Idea: a short fall stuns the target.
- [ ] 3.3 Should the player's boost be greater off heavy or immovable targets?
- [ ] 3.5 What momentum does the target receive? Boost replaces the player's momentum with a fixed one.
  - Option A (initial ideas): the target gets the opposite of the player's new momentum. Predictable: every boost throws the same. Total momentum is not conserved.
  - Option B: the target gets the opposite of the player's change in momentum. Conserved, and consistent with 1.7. The throw then depends on how the player was moving: boosting upward out of a fast fall slams the target down hard; boosting the way you were already going barely moves it.
- [ ] 3.6 The teleport: what happens when the destination is blocked?
- [ ] 3.4 Should dead enemies remain as objects that can be boosted off and bashed?

## 4. Bash

- [x] 4.1 No ability refreshes (e.g. "one air bash, refreshed on landing") as the way to stop bash becoming flight.
- [ ] 4.2 How to limit flight, then. Options from the initial ideas: a long enough cooldown; require a designated target.
- [~] 4.3 Bash doubles as swimming, if water is like air with higher resistance and some buoyancy.
- [x] 4.4 Naming: rename "boost" to something else and "bash" to "boost"? Decision: boost becomes "phase", and bash becomes "boost".
  - The rename came from the swimming idea in 4.3: "bash" names the impact, and a swim stroke has none, while "boost" names the burst of speed. The old "boost" was the more misleading name anyway, since its defining feature is passing through something.
  - Rejected for bash: "dash", which nowadays commonly implies a refresh on touching the ground (see 4.1).
- [ ] 4.5 I-frames for bashing spiky enemies: always, never, or a later upgrade.

## 5. Lasers and water

- [x] 5.1 The player never gets a laser.
- [x] 5.2 Robots can be baited into breaking blocks with their lasers. Good for a puzzle or two before the player gets bash.
- [x] 5.3 Switches that only robot lasers can activate. Some are unreachable until the water is on.
- [x] 5.4 Water bends lasers, for refraction-based laser puzzles. Attenuation is possible but probably unimportant.
- [ ] 5.5 What else does water do for traversal?

## 6. World and structure

- [x] 6.1 The whole game is one seamless map, with no screen transitions.
  - [ ] When world and enemy scripts run, and the enemy respawn policy.
- [ ] 6.2 Save points, and saves between stages of multi-stage challenges.
- [ ] 6.3 Fall damage: whether it exists, and whether it applies to enemies (see 3.2).
- [ ] 6.4 Gating beyond abilities and world changes: doors and keys, quests.
- [ ] 6.5 The one extra ability with the most interesting interactions.

## 7. Story

- [~] 7.1 Existential themes: purpose and agency. The guardian robots are intelligent.
- [ ] 7.2 Do the guardians have to shoot the player, or do they want to? What do they think about it, and how does the player find out?
- [ ] 7.3 Why does the player keep activating defenses that hurt it? What is the facility defending against?
