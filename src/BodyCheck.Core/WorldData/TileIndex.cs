namespace BodyCheck.Core.WorldData;

/// <summary>
/// A tile's kind. The values are those of the LDtk project's <c>SolidGeometry</c> layer. Every kind but
/// <see cref="Empty"/> is solid.
/// </summary>
public enum TileIndex
{
    /// <summary>No tile.</summary>
    Empty = 0,
    /// <summary>The kind a text room's '#' is.</summary>
    Ground = 1,
    Grass = 2,
    /// <summary>The number of kinds, <see cref="Empty"/> included. It must stay last.</summary>
    Count,
}
