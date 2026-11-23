using Godot;

namespace BodyCheck.Core;

/// <summary>Constants that more than one subsystem, or the game, needs.</summary>
public static class Constants
{
    public static readonly Vector2I Resolution = new(320, 240);

    /// <summary>
    /// The size of a tile's face, in pixels, on screen at 320x240 and in LDtk. A screen at 320x240 is then 20x15
    /// tiles. A tile is 1 unit in the game world.
    /// </summary>
    public const int TilePixels = 16;

    /// <summary>
    /// The player's id, the same in every world, since the player is no entity's configuration and is the same
    /// player wherever it is.
    /// </summary>
    public const string PlayerId = "player";

    /// <summary>The id of the block the player spawns, which is no entity's configuration either.</summary>
    public const string PlayerBlockId = "player-block";

    /// <summary>
    /// The strength of gravity on a body whose gravity scale is 1, in tiles per second squared: the project setting
    /// <c>physics/3d/default_gravity</c>, read when this is first used.
    /// </summary>
    public static readonly float Gravity = (float)ProjectSettings.GetSetting("physics/3d/default_gravity");

    /// <summary>
    /// How near two bodies must be, in tiles, at the start of the engine's step, for it to give them a contact in that
    /// step: the project setting <c>physics/jolt_physics_3d/simulation/speculative_contact_distance</c>, read when this
    /// is first used. So a body can be stopped by a corner that it would only pass within this distance of, with a
    /// contact whose normal points from the corner to the body.
    /// </summary>
    public static readonly float SpeculativeContactDistance =
        (float)ProjectSettings.GetSetting("physics/jolt_physics_3d/simulation/speculative_contact_distance");
}
