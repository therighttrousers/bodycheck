namespace BodyCheck.Core;

/// <summary>Constants that more than one subsystem, or the game, needs.</summary>
public static class Constants
{
    /// <summary>
    /// The size of a tile's face, in pixels, on screen at 320x240 and in LDtk. A screen at 320x240 is then 20x15
    /// tiles. A tile is 1 unit in the game world.
    /// </summary>
    public const int TilePixels = 16;

    /// <summary>A level's width, in tiles.</summary>
    public const int LevelWidth = 20;

    /// <summary>A level's height, in tiles.</summary>
    public const int LevelHeight = 15;
}
