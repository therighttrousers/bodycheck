using Godot;

namespace BodyCheck.Core.WorldData;

/// <summary>
/// A named world: rooms that don't overlap, with one player start among them. Its tiles are its rooms', and everywhere
/// outside a room is solid. A world comes from an LDtk project, one room per level, or from one text room.
/// </summary>
public sealed class World : ITileGrid
{
    private readonly TileGrid _tiles;

    /// <param name="rooms">
    /// The rooms, each with its bottom left tile at or above and to the right of the world's origin. There must be at
    /// least one, and exactly one player start among them.
    /// </param>
    public World(string name, IEnumerable<Room> rooms)
    {
        Name = name;
        Rooms = [.. rooms];
        Origin = new Vector2I(Rooms.Min(room => room.Origin.X), Rooms.Min(room => room.Origin.Y));
        Size = new Vector2I(Rooms.Max(room => room.Bounds.End.X), Rooms.Max(room => room.Bounds.End.Y)) - Origin;
        _tiles = CreateTiles(Size, Rooms);

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

    /// <summary>Builds a fresh grid containing all of the world's tiles.</summary>
    private static TileGrid CreateTiles(Vector2I size, IReadOnlyList<Room> rooms)
    {
        var tiles = new TileGrid(size.X, size.Y);
        foreach (Room room in rooms)
        {
            for (int y = 0; y < room.Size.Y; y++)
            {
                for (int x = 0; x < room.Size.X; x++)
                {
                    tiles.SetSolid(room.Origin.X + x, room.Origin.Y + y, room.IsSolid(x, y));
                }
            }
        }
        return tiles;
    }

    /// <summary>A world of one room, with the room's name.</summary>
    public World(Room room) : this(room.Name, [room]) { }

    public string Name { get; }

    public IReadOnlyList<Room> Rooms { get; }

    public Vector2I Origin { get; }
    public Vector2I Size { get; }
    public Rect2I Bounds => new(Origin, Size);
    public bool IsSolid(int x, int y) => _tiles.IsSolid(x, y);

    /// <summary>
    /// Every entity's configuration, in the world's coordinates: each room's, in the order of the rooms and then of the
    /// room's own.
    /// </summary>
    public IReadOnlyList<EntityConfig> EntityConfigs { get; }
}
