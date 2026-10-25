# 026: A carrier's drive doesn't grow with its load

Closed on 2026-10-24. Outcomes and unresolved items are in the design docs: [movement.md](../designs/movement.md).

## Context

Inciting event (2026-10-24): Neil, playing: with a block on its head, the player is strong enough to push a platform a long way.

Since `019 §6.4`, a walker's ground drive was multiplied by its load over its own weight, so that carrying slowed it less. The drive is one force, at the walker's feet. Its grip can't tell the share that speeds up the load from the share that goes into whatever the walker pushes, so the push grew with the load as well. With gravity at 28.125, the player's ground drive of 67.5 became 135 under a player block (mass 1) and 202.5 under a block (mass 2). A platform's drive along its line and its hold across it are each capped at half its weight, about 70 for a platform of mass 5 (`025 §2.2`). Unloaded, the player falls just short of that; loaded, it overpowers either by 2 to 3 times.

Worked out from the code and features while unifying the two controllers' runs (issue #71). Not measured in a run.

## 1. The drive

- [x] 1.1 What limits a walker's push, physically? Two things: its legs, whose force doesn't change with load; and its grip, friction, μ times the normal force, which grows with the load because the normal force is the whole stack's weight. A walker pushes with whichever is lower. Here the legs bind: the player's friction is several times its drive, unloaded or loaded. So a load that raises the grip changes nothing for how fast a carrier speeds up. `Entity`'s friction already grows with the load, and that stays. `019 §6.4` scaled the legs, which isn't physical, and its trial said so.
- [x] 1.2 Options: (a) undo `019 §6.4`: the drive is the legs' force, so carrying slows the carrier as its mass says, and if that's too slow, retune the drive; (b) give the extra force to the carried bodies instead of the feet, so that the walker's own push stays as it is unloaded. The steadying hand ties a carried block's speed to the walker's, so a blocked walker's block would push through the hand into it and on, up to `PlayerCarrySteadying`: less than now, but still more than the walker alone; (c) drop the extra force while the walker pushes something, from a contact in the direction it drives: a special case. Decision (Neil): (a), the physically honest one, and uniform. This supersedes `019 §6.4`, and with it the part of `019 §3.1` that says carrying costs less than physics says.
- [x] 1.3 As built (Neil): the line that scaled the drive is gone from the shared run. Measured on 2026-10-24: in `--probe=wobble`, the player carrying a block of mass 2 reaches 5.44 tiles per second in 20 ticks, against 10 with the scaling, which matches the 5.4 that `019 §6.4` measured before it. In `--probe=ride-robot`, a robot carrying the player patrols at 2.50 tiles per second, against 2.73 with the scaling.

## 2. Open

- [ ] 2.1 Not yet checked by playing: that the loaded player no longer shoves a platform, and how carrying feels at the speed physics gives it. If it is too slow, the drive is what to retune.
- [ ] 2.2 `PlayerCarrySteadying` 35 and `PlayerCarryTorque` 25 were retuned for the faster loaded player of `019 §6.4`, when running through a tunnel 2 tiles high started knocking the block off. Whether they still fit the slower one is to be seen by playing.
  - Seen by Neil, 2026-10-24: pressing against such a tunnel no longer knocks the block off, but running into it again and again does. Whether the player should be able to knock what it carries off by running into things is now an open question in the design docs.
