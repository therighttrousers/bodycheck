using Godot;

namespace BodyCheck.Core.WorldData;

/// <summary>
/// A named world: rooms that don't overlap, with one player start among them. Its tiles are the union of its rooms', and
/// everywhere outside a room is empty; so is its camera boundary. Its entities are its rooms', with ids unique in the world, and their routes may
/// lead into any of its rooms. A world comes from an LDtk project, one room per level, or from one text room.
/// </summary>
public sealed class World
{
    private readonly TileGrid _tiles;
    private readonly bool[,] _cameraBoundary;

    /// <param name="rooms">
    /// The rooms, each with its bottom left tile at or above and to the right of the world's origin. There must be at
    /// least one, and exactly one player start among them. Every step of a route must name an entity in the world that
    /// responds to its action, and no route may lead back to where it started.
    /// </param>
    /// <exception cref="ArgumentException">When the rooms can't make a world, saying why.</exception>
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
        _cameraBoundary = new bool[Size.X, Size.Y];
        foreach (Room room in Rooms)
        {
            for (int y = 0; y < room.Size.Y; y++)
            {
                for (int x = 0; x < room.Size.X; x++)
                {
                    _tiles.SetTileIndex(x + room.Origin.X, y + room.Origin.Y, room.GetTileIndex(x, y));
                    _cameraBoundary[x + room.Origin.X, y + room.Origin.Y] = room.IsCameraBoundary(x, y);
                }
            }
        }

        var bodies = new List<EntityConfig>();
        var controls = new List<EntityConfig>();
        var byId = new Dictionary<string, EntityConfig>();
        // Which room each configuration is in, by its id, to name in errors.
        var roomNames = new Dictionary<string, string>();
        foreach (Room room in Rooms)
        {
            foreach (EntityConfig config in room.EntityConfigs)
            {
                EntityConfig moved = config.MovedBy(room.Origin);
                if (!byId.TryAdd(moved.Id, moved))
                {
                    throw new ArgumentException(
                        $"In world '{name}', a {moved.Kind} in room '{room.Name}' has the id '{moved.Id}' of a "
                        + $"{byId[moved.Id].Kind} in room '{roomNames[moved.Id]}'.", nameof(rooms));
                }
                roomNames[moved.Id] = room.Name;
                (moved.Kind.HasBody ? bodies : controls).Add(moved);
            }
        }
        Bodies = bodies;
        Controls = controls;
        ConfigsById = byId;

        int numPlayerStarts = Bodies.Count(c => c.Kind == EntityKind.Player);
        if (numPlayerStarts != 1)
        {
            throw new ArgumentException($"World '{name}' has {numPlayerStarts} player starts, not 1.", nameof(rooms));
        }

        string Named(EntityConfig config) => $"a {config.Kind} in room '{roomNames[config.Id]}'";
        foreach (EntityConfig config in Controls)
        {
            foreach (Step step in StepsOf(config))
            {
                if (!ConfigsById.TryGetValue(step.TargetId, out EntityConfig? target))
                {
                    throw new ArgumentException(
                        $"In world '{name}', {Named(config)} targets '{step.TargetId}', which isn't in the world.",
                        nameof(rooms));
                }
                if (!target.Kind.Responses.Contains(step.Action))
                {
                    throw new ArgumentException(
                        $"In world '{name}', {Named(config)} asks {Named(target)} to {step.Action}, which it doesn't "
                        + "respond to.", nameof(rooms));
                }
            }
        }

        foreach (EntityConfig.Script script in Controls.OfType<EntityConfig.Script>())
        {
            foreach (string id in script.Entities)
            {
                if (!ConfigsById.TryGetValue(id, out EntityConfig? entity))
                {
                    throw new ArgumentException(
                        $"In world '{name}', {Named(script)} is given '{id}', which isn't in the world.", nameof(rooms));
                }
                if (!entity.Kind.HasBody)
                {
                    throw new ArgumentException(
                        $"In world '{name}', {Named(script)} is given {Named(entity)}, which has no body.",
                        nameof(rooms));
                }
            }
        }

        if (FindCycle() is EntityConfig start)
        {
            throw new ArgumentException(
                $"In world '{name}', the routes from {Named(start)} lead back to it.", nameof(rooms));
        }
    }

    /// <summary>The steps a configuration runs, if it runs any.</summary>
    private static IReadOnlyList<Step> StepsOf(EntityConfig config) => config switch
    {
        EntityConfig.Trigger trigger => [trigger.Step],
        EntityConfig.Sequence sequence => sequence.Steps,
        EntityConfig.Fanout fanout => fanout.Steps,
        EntityConfig.Once once => [once.Step],
        _ => [],
    };

    /// <summary>
    /// A configuration whose routes lead back to it, or null if there is none. Every step's target must be in the world.
    /// </summary>
    private EntityConfig? FindCycle()
    {
        // A depth-first search, which finds a cycle when it reaches a configuration it is still searching from.
        var searching = new HashSet<string>();
        var searched = new HashSet<string>();

        EntityConfig? Search(EntityConfig config)
        {
            if (searched.Contains(config.Id))
            {
                return null;
            }
            if (!searching.Add(config.Id))
            {
                return config;
            }
            foreach (Step step in StepsOf(config))
            {
                if (Search(ConfigsById[step.TargetId]) is EntityConfig found)
                {
                    return found;
                }
            }
            searching.Remove(config.Id);
            searched.Add(config.Id);
            return null;
        }

        return Controls.Select(Search).FirstOrDefault(found => found is not null);
    }

    /// <summary>A world of one room, with the room's name.</summary>
    public World(Room room) : this(room.Name, [room]) { }

    public string Name { get; }

    public IReadOnlyList<Room> Rooms { get; }

    public Vector2I Size { get; }
    public TileIndex GetTileIndex(int x, int y) => _tiles.GetTileIndex(x, y);

    /// <summary>
    /// Whether a room marks a tile for the camera to keep out of view. False outside the rooms, though the camera keeps
    /// out of there too: that is the camera's to decide.
    /// </summary>
    public bool IsCameraBoundary(int x, int y) =>
        x >= 0 && y >= 0 && x < Size.X && y < Size.Y && _cameraBoundary[x, y];

    /// <summary>
    /// The configurations of the entities with bodies, in the world's coordinates: each room's, in the order of the rooms
    /// and then of the room's own, which is the order in which they are built and stepped.
    /// </summary>
    public IReadOnlyList<EntityConfig> Bodies { get; }

    /// <summary>
    /// The configurations of the control entities, which have no body, in the world's coordinates and in the same order
    /// as <see cref="Bodies"/>, so that whatever goes through them each tick does so in a known order.
    /// </summary>
    public IReadOnlyList<EntityConfig> Controls { get; }

    /// <summary>Every configuration, in <see cref="Bodies"/> and <see cref="Controls"/>, by its id.</summary>
    public IReadOnlyDictionary<string, EntityConfig> ConfigsById { get; }
}
