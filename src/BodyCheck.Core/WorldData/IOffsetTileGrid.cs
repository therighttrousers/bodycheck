using Godot;

namespace BodyCheck.Core.WorldData;

/// <summary>
/// Represents a grid of tiles and its bounds in world coordinates.
/// </summary>
public interface IOffsetTileGrid
{
    /// <summary>The location of the bottom-left tile in world coordinates.</summary>
    Vector2I Origin { get; }
    /// <summary>The size of this grid.</summary>
    Vector2I Size { get; }
    /// <summary>The bounds of this grid in world coordinates.</summary>
    Rect2I Bounds { get; }
    /// <summary>Returns true when the tile at a local coordinate is solid.</summary>
    bool IsSolid(int x, int y);
}
