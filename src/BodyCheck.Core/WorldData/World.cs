using Godot;

namespace BodyCheck.Core.WorldData;

/// <summary>
/// A named world: rooms that don't overlap, with one player start among them. Its tiles are the union of its rooms', and
/// everywhere outside a room is empty. A world comes from an LDtk project, one room per level, or from one text room.
/// </summary>
public sealed class World
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
        if (Rooms.Count == 0)
        {
            throw new ArgumentException($"World '{name}' has no rooms.", nameof(rooms));
        }
        for (int i = 0; i < Rooms.Count; i++)
        {
            if (Rooms[i].Origin.X < 0 || Rooms[i].Origin.Y < 0)
            {
                throw new ArgumentException(
                    $"In world '{name}', the origin of room '{Rooms[i].Name}' is {Rooms[i].Origin.X},{Rooms[i].Origin.Y}.", nameof(rooms));
            }
            for (int j = i + 1; j < Rooms.Count; j++)
            {
                if (Rooms[i].Bounds.Intersects(Rooms[j].Bounds))
                {
                    throw new ArgumentException(
                        $"In world '{name}', rooms '{Rooms[i].Name}' and '{Rooms[j].Name}' overlap.", nameof(rooms));
                }
            }
        }

        Size = new Vector2I(Rooms.Max(room => room.Bounds.End.X), Rooms.Max(room => room.Bounds.End.Y));
        _tiles = new TileGrid(Size.X, Size.Y);
        foreach (Room room in Rooms)
        {
            for (int y = 0; y < room.Size.Y; y++)
            {
                for (int x = 0; x < room.Size.X; x++)
                {
                    _tiles.SetSolid(x + room.Origin.X, y + room.Origin.Y, room.IsSolid(x, y));
                }
            }
        }

        EntityConfigs = [.. Rooms.SelectMany(room => room.EntityConfigs.Select(config => config with { Start = config.Start + room.Origin }))];
        Respawns = [.. EntityConfigs.OfType<EntityConfig.Respawn>()];

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

    public Vector2I Size { get; }
    public bool IsSolid(int x, int y) => _tiles.IsSolid(x, y);

    /// <summary>
    /// Every entity's configuration, in the world's coordinates: each room's, in the order of the rooms and then of the
    /// room's own.
    /// </summary>
    public IReadOnlyList<EntityConfig> EntityConfigs { get; }

    /// <summary>The respawns among <see cref="EntityConfigs"/>, in their order.</summary>
    public IReadOnlyList<EntityConfig.Respawn> Respawns { get; }
}
