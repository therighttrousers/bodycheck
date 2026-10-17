# 007: Running over one-tile gaps is not a design goal

## Context

Inciting event (2026-10-16): issue #28 retuned the game so that 1.0x plays like 0.75x did. Every speed is ×0.75, and gravity and forces are ×0.5625. After that, the test of the design goal "At top speed, the player runs over a 1-tile gap without falling in" failed at 2 of its 3 drags.

Measured with the new tuning, holding Right over six separate 1-tile gaps: the player falls into at least one gap at 8 of 13 drags from 0.20 to 0.80. The old tuning (top speed about 15 tiles per second) fell in at none of the 13.

Mechanism: when the player is wholly over a gap (left edge past the near tile, right edge short of the far one), it sinks slightly or arrives level with the tile tops. On the next tick the engine can resolve the far tile's corner against its side face rather than its top, which stops the player dead, and it falls in. Which tick lands where depends on speed, so the outcome looks random.

Closed on 2026-10-16. Outcomes are in [movement.md](../designs/movement.md), and so is the unresolved item.

## 1. Keep the goal?

- [x] 1.1 Should running over a 1-tile gap be consistent?
  - Options: a wider player (over 0.81 tiles, a guess from a first theory that 1.2 below disproves); a higher top speed; a physics fix at the far edge; dropping the goal.
  - Decision (Neil): drop it. Levels don't put 1-tile gaps on running paths, because crossing one is chancy.
  - Rationale:
    - The goal was always a "nice to have", in case a 1-tile gap exists.
    - Getting *up* through a 1-tile gap is hard anyway, because the player is 0.8 tiles wide. Allowing it would take more physics hacks.
    - Movement inspirations such as Hollow Knight and Prince of Persia: The Lost Crown avoid the issue by not having gaps one player wide. The one inspiration that has them, Animal Well, inspires the puzzles, not the movement.
  - Downside: a comb-shaped floor (1-tile gaps back to back) can't easily be a puzzle where the player realises it can be crossed.
- [x] 1.2 Does enough external speed make a comb crossable? Neil asked, since driving doesn't slow a player over top speed.
  - First theory: above 12 tiles per second a tick covers more than the 0.2 tiles of slack, so the player can never catch. Wrong.
  - Measured with the new tuning: the player is pushed to between 12 and 30 tiles per second in steps of 0.5, then holds Right over a comb of 7 one-tile gaps. It crosses in 19 of 36 runs and catches on a far edge in 17, with no threshold. It fails at 13, 22 and 30, and crosses at 12, 20 and 29.
  - So the far-edge catch happens at any speed, depending on where the ticks land.
- [ ] 1.3 Left until a level wants a comb that the player has to work out how to cross.
  - How long must a comb be for the chance of crossing it to fall below 1%?
  - What would make crossing reliable. Candidates: bevelled or rounded bottom corners on the player's shape, so that a corner contact pushes up; our own handling of the far edge.
