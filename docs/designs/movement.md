# Player movement

TODO: Clean up, elaborate

Note: As we develop, we will create test rooms and refer to them here; e.g. "player should be able to traverse {room} with {outcome}".

- Minimum jump height is just over 2 tiles.
- Maximum jump height is just over 4 tiles.
- Full-horizontal-speed jumps, on the first few frames, have velocity with ~45 degrees. (May be more or less depending on friction, drag, top speed, jump speed.)
- Air drive is weaker than ground drive.
- Drive in reverse is faster than drive forward (mostly for precision landings).
- Moving off an edge (not jumping) maintains ground control for a short time; i.e. coyote frames.
- Moving over a 1-tile gap at top speed consistently skips over it or falls in it.
- Jumping up through a 1-tile gap doesn't require perfect alignment.

# Enemy movement

TODO
