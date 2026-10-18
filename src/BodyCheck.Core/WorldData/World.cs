using Godot;

namespace BodyCheck.Core.WorldData;

/// <summary>
/// A named world: rooms that don't overlap, with one player start among them. Its tiles are its rooms', and everywhere
/// outside a room is solid. A world comes from an LDtk project, one room per level, or from one text room.
/// </summary>
public sealed class World
{
    /// <param name="rooms">
    /// The rooms, each with its bottom left tile at or above and to the right of the world's origin. There must be at
    /// least one, and exactly one player start among them.
    /// </param>
    public World(string name, IEnumerable<Room> rooms)
    {
        Name = name;
        Rooms = [.. rooms];

        if (Rooms.Count == 0)
        {
            throw new ArgumentException($"World '{name}' has no rooms.", nameof(rooms));
        }
        foreach (Room room in Rooms)
        {
            if (room.Origin.X < 0 || room.Origin.Y < 0)
            {
                throw new ArgumentException($"In world '{name}', room '{room.Name}' is left of or below the origin.", nameof(rooms));
            }
        }
        for (int i = 0; i < Rooms.Count; i++)
        {
            for (int j = i + 1; j < Rooms.Count; j++)
            {
                if (Rooms[i].Bounds.Intersects(Rooms[j].Bounds))
                {
                    throw new ArgumentException(
                        $"In world '{name}', rooms '{Rooms[i].Name}' and '{Rooms[j].Name}' overlap.", nameof(rooms));
                }
            }
        }

        EntityConfigs = [.. Rooms.SelectMany(room => room.EntityConfigs.Select(config => config with { Start = config.Start + room.Origin }))];

        int numPlayerStarts = EntityConfigs.Count(c => c.Kind == EntityKind.Player);
        if (numPlayerStarts != 1)
        {
            throw new ArgumentException($"World '{name}' has {numPlayerStarts} player starts, not 1.", nameof(rooms));
        }
    }

    /// <summary>A world of one room, with the room's name.</summary>
    public World(Room room) : this(room.Name, [room]) { }

    public string Name { get; }

    public IReadOnlyList<Room> Rooms { get; }

    /// <summary>
    /// Every entity's configuration, in the world's coordinates: each room's, in the order of the rooms and then of the
    /// room's own.
    /// </summary>
    public IReadOnlyList<EntityConfig> EntityConfigs { get; }

    /// <summary>
    /// Builds a fresh grid of the world's tiles, over its rooms' bounding box from the origin, so that changes to the
    /// tiles while it plays don't reach the world. Tiles outside every room are solid.
    /// </summary>
    public TileGrid CreateTiles()
    {
        var tiles = new TileGrid(Rooms.Max(room => room.Bounds.End.X), Rooms.Max(room => room.Bounds.End.Y));
        for (int y = 0; y < tiles.Height; y++)
        {
            for (int x = 0; x < tiles.Width; x++)
            {
                tiles.SetSolid(x, y, true);
            }
        }
        foreach (Room room in Rooms)
        {
            for (int y = 0; y < room.Bounds.Size.Y; y++)
            {
                for (int x = 0; x < room.Bounds.Size.X; x++)
                {
                    tiles.SetSolid(room.Origin.X + x, room.Origin.Y + y, room.IsSolid(x, y));
                }
            }
        }
        return tiles;
    }
}
