using Godot;

namespace BodyCheck.Core;

/// <summary>Constants that more than one subsystem, or the game, needs.</summary>
public static class Constants
{
    /// <summary>
    /// The size of a tile's face, in pixels, on screen at 320x240 and in LDtk. A screen at 320x240 is then 20x15
    /// tiles. A tile is 1 unit in the game world.
    /// </summary>
    public const int TilePixels = 16;

    /// <summary>
    /// The strength of gravity on a body whose gravity scale is 1, in tiles per second squared: the project setting
    /// <c>physics/3d/default_gravity</c>, read when this is first used.
    /// </summary>
    public static readonly float Gravity = (float)ProjectSettings.GetSetting("physics/3d/default_gravity");
}
