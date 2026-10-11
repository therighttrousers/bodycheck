# Enemies

All enemies can be phased through.

## Class: turret

Larger and heavier than the player. If not benign, has a single glowing red eye.

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

Fired by rocket enemies. Travel in a straight line. Inflict splash damage to enemies and player.

## Type: benign

These enemies can only damage the player incidentally; e.g. by falling on the player (if we decide that does damage).

## Type: laser

These enemies fire a powerful directed beam at the player, and possibly critters.

Possible firing timeline:
- 0 ms: Enemy stops moving (if applicable). A thin laser appears and follows the target. No damage yet.
- 500 ms: The laser thickens and stops following the target. No damage yet.
- 750 ms: The laser greatly thickens, and does per-tick damage to anything it passes through.
- 1000 ms: The laser disappears. Firing cooldown starts.
- 2000 ms: Cooldown done. Enemy may fire again, resume regular patrol loop, etc.

If the enemy loses targeting for a short time (100 ms?), they enter firing cooldown.

The timeline should be adjusted to allow the player to avoid taking damage by jumping, with a generous timing window.

## Type: rocket

These enemies fire rockets at the player.

Timeline and targeting rules TBD.
