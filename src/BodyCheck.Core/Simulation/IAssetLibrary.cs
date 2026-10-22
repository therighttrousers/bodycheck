using BodyCheck.Core.WorldData;

using Godot;

namespace BodyCheck.Core.Simulation;

public interface IAssetLibrary
{
    Node3D GetEntityVisual(EntityKind kind, Vector2 halfSize);
    Node3D GetTileVisual();
}
