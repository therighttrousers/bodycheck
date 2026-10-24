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
    Node3D? GetEntityVisual(EntityConfig config);

    /// <summary>
    /// Makes what to draw for the block the player spawns, with a body of <paramref name="halfSize"/>, with its origin at
    /// the middle of its base. It is called once for each world.
    /// </summary>
    Node3D GetPlayerBlockVisual(Vector2 halfSize);

    /// <summary>
    /// Returns a box with the given size and color/alpha for debugging, or as a placeholder for an eventual model.
    /// </summary>
    MeshInstance3D GetDebugBoxVisual(Vector2 halfSize, Color color, float alpha = 0.5f);

    /// <summary>
    /// The mesh to draw a tile with, darkened by <paramref name="darkening"/> from 0 to 1, and the transform that fits it
    /// to a unit cube centred on the origin. Callers share the mesh, so they must not change it.
    /// </summary>
    (Mesh Mesh, Transform3D Transform) GetTileMesh(float darkening = 0);
}
