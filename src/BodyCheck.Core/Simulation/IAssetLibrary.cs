using BodyCheck.Core.WorldData;

using Godot;

namespace BodyCheck.Core.Simulation;

/// <summary>The game's looks for what a world holds, so that Core doesn't know the game's assets.</summary>
public interface IAssetLibrary
{
    /// <summary>
    /// Makes what to draw for an entity of <paramref name="kind"/>, with a body of <paramref name="halfSize"/>, with its
    /// origin at the middle of its base. It is called once for each entity.
    /// </summary>
    Node3D GetEntityVisual(EntityKind kind, Vector2 halfSize);

    /// <summary>
    /// The mesh to draw a tile with, darkened by <paramref name="darkening"/> from 0 to 1, and the transform that fits it
    /// to a unit cube centred on the origin. Callers share the mesh, so they must not change it.
    /// </summary>
    (Mesh Mesh, Transform3D Transform) GetTileMesh(float darkening = 0);
}
