# Characters

All NPCs can be phased through.

## Class: turret

Larger and heavier than the player. If not benign, has red eyes.

- Types: Benign, laser, rocket.
- Movement: Immovable.
- Limitations: Line-of-sight targeting.

Benign turrets are useful for a traversal-with-turrets tutorial; the first turret type the player comes across.

## Class: robot

About the size and weight of the player. (The player might be one; TBD, depends on story.) If not benign, has glowing red eyes.

- Types: Benign, laser, rocket.
- Movement: Patrol slowly when on solid ground. Keep facing detected targets.
- Limitations: Line-of-sight targeting. Can't target anything in a 90-degree arc above or below. Must be facing the player to target them.

## Class: rocket

Travel in a straight line. Inflict splash damage to all characters.

## Type: benign

These NPCs can only damage the player incidentally; e.g. by falling on the player (if we decide that does damage).

## Type: laser

These NPCs fire a powerful directed beam at the player, and possibly critters.

Firing timeline, as tuned by playing so far, with each duration a feature (`RobotLaser*`):
- 0 ms: NPC stops moving (if applicable). A thin laser appears and follows the target. No damage yet.
- 1000 ms: The laser thickens and stops following the target. No damage yet.
- 1750 ms: The laser greatly thickens, and does per-tick damage to anything it passes through.
- 2250 ms: The laser disappears. Firing cooldown starts.
- 5250 ms: Cooldown done. NPC may fire again, resume regular patrol loop, etc.

If the NPC loses sight of the target for 100 ms while the laser follows it, it enters firing cooldown. Once the laser stops following, it fires wherever the target has gone. If the NPC is moved 2 tiles from where it began aiming, such as by being pushed, it enters firing cooldown at any point in the timeline.

The timeline should allow the player to avoid taking damage by jumping, with a generous timing window.

Built so far, for robots only, with no damage: every robot fires at the player. A robot sees the player's center from its eyes, 0.8 tiles above its own center, when the player is within 45 degrees of level and 15 tiles, with nothing in between, and ahead of it, or on either side while it faces neither way: in the air, or overheated. Whenever it sees the player, cooling down or not, it stands still and turns to face it, except once the laser has stopped following it, so that it faces the way it fires; afterwards it patrols the way it faces. The beam stops at the first tile or entity it meets.

## Type: rocket

These NPCs fire rockets at the player.

Timeline and targeting rules TBD.
