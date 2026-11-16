using BodyCheck.Core.Maths;
using BodyCheck.Core.Simulation;
using BodyCheck.Core.WorldData;

using Godot;

namespace BodyCheck.Decoration;

/// <summary>
/// Makes something that an environment adds to each of its rooms, such as a back wall, from the room alone. An
/// environment's scene lists its decorators in a <see cref="RoomDecorations"/>, and each decorator's exported properties
/// are its settings, for tuning in the editor.
/// <para>
/// A decorator is pure and deterministic: what it makes depends only on the room, its own settings and the asset
/// library's meshes, and never on the scene tree or the game's state, so that it can one day run in the editor too, to
/// preview a room. What it makes is in the room's coordinates, with its origin at the room's bottom-left tile, and is
/// built of the engine's own nodes. See <c>docs/designs/rendering.md</c>.
/// </para>
/// </summary>
[GlobalClass]
public abstract partial class RoomDecorator : Resource
{
    /// <summary>Makes what this decorator adds to <paramref name="room"/>, or null if it adds nothing to it.</summary>
    public abstract Node3D? Decorate(Room room, IAssetLibrary assets);

    /// <summary>
    /// A random number from 0 to 1 for a tile of <paramref name="room"/>, in the room's coordinates, the same on every
    /// run. It is seeded by the room's name rather than where it is in the world, so that moving a room keeps its
    /// decorations, and anything placed by hand around them.
    /// </summary>
    public static float RandomForTile(Room room, Vector2I coord, string salt) =>
        new Rand($"{room.Name},{coord.X},{coord.Y},{salt}").NextFloat();
}
