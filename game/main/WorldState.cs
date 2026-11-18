using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

using BodyCheck.Core.Entities;
using BodyCheck.Core.WorldData;

using Godot;

namespace BodyCheck;

/// <summary>
/// The world's state, which the game owns: everything the story, the dialogue and the game's rules read or change. Core
/// sees part of it, as <see cref="IWorldState"/>, which is where the rules write what they own, such as a door's last
/// action; the game's own view of those parts lets it read them only, and change them by asking for an action.
/// <para>
/// It is filled in from a world's data before the world is built, so that the world finds each entity's state there.
/// For now it lasts as long as the world it was filled in from: nothing is saved, and a world that is built again
/// starts again.
/// </para>
/// </summary>
public sealed partial class WorldState : RefCounted, IWorldState
{
    private readonly Dictionary<string, EntityState> _entities = [];
    private readonly Dictionary<string, EntityState> _entitiesByName = [];

    /// <summary>The state of <paramref name="world"/>'s entities as its data says they start.</summary>
    /// <exception cref="ArgumentException">Two of its entities have the same name.</exception>
    public WorldState(World world)
    {
        foreach (EntityConfig config in world.Bodies)
        {
            var kindName = KindName(config.Kind);
            var name = DefaultName(config, world.Bodies);
            EntityState entity = config switch
            {
                EntityConfig.Player => Player = new PlayerState(config.Id, kindName, name),
                EntityConfig.VerticalDoor door => new DoorState(config.Id, kindName, name,
                    door.StartsClosed() ? OpenableAction.Close : OpenableAction.Open),
                _ => new EntityState(config.Id, kindName, name),
            };
            if (!_entitiesByName.TryAdd(entity.Name, entity))
            {
                throw new ArgumentException(
                    $"In world '{world.Name}', the entities {_entitiesByName[entity.Name].Id} and {entity.Id} are both "
                        + $"named '{entity.Name}'.",
                    nameof(world));
            }
            _entities[entity.Id] = entity;
        }

        Debug.Assert(Player is not null);
    }

    /// <summary>
    /// The name of an entity's kind as a speaker, which its avatar is named for, and which its name starts with if
    /// nothing gives it one: <c>B-CON</c> for the player, as the dialogue files name it.
    /// </summary>
    private static string KindName(EntityKind kind) => kind switch
    {
        EntityKind.Player => "B-CON",
        EntityKind.VerticalDoor => "Door",
        _ => kind.ToString(),
    };

    /// <summary>
    /// An entity's name: the player's is its kind's; any other's is its kind's and the start of its id, such as
    /// <c>Door a6d1</c>, as a machine's serial number. The start is at least <see cref="ShortIdLength"/> characters, and
    /// as many more as tell it from every other id of its kind in <paramref name="bodies"/>, as git shortens a commit's
    /// hash, so that names are unique.
    /// </summary>
    private static string DefaultName(EntityConfig config, IEnumerable<EntityConfig> bodies)
    {
        if (config.Kind == EntityKind.Player)
        {
            return KindName(config.Kind);
        }
        int length = bodies
            .Where(other => other.Kind == config.Kind && other != config)
            .Select(other => CommonPrefixLength(config.Id, other.Id) + 1)
            .Append(ShortIdLength)
            .Max();
        return $"{KindName(config.Kind)} {config.Id[..Math.Min(length, config.Id.Length)]}";
    }

    private const int ShortIdLength = 4;

    private static int CommonPrefixLength(string a, string b)
    {
        int i = 0;
        while (i < a.Length && i < b.Length && a[i] == b[i])
        {
            i++;
        }
        return i;
    }

    private static T CastEntity<T>(object entity, string id) => entity switch
    {
        T value => value,
        var value => throw new InvalidOperationException($"Entity {id} is a {value.GetType().Name}, not a {typeof(T).Name}."),
    };

    /// <summary>The player's state; e.g. its abilities.</summary>
    public PlayerState Player { get; }

    /// <summary>The entity with a body whose id is <paramref name="id"/>.</summary>
    /// <exception cref="KeyNotFoundException">There is none.</exception>
    public EntityState GetEntity(string id) => _entities[id];

    /// <summary>The entity with a body whose id is <paramref name="id"/>, as the state of its kind.</summary>
    /// <exception cref="KeyNotFoundException">There is none.</exception>
    /// <exception cref="InvalidOperationException">Its state isn't a <typeparamref name="T"/>.</exception>
    public T GetEntity<T>(string id) where T : EntityState => CastEntity<T>(GetEntity(id), id);

    /// <summary>The entity with a body whose name is <paramref name="name"/>.</summary>
    /// <exception cref="KeyNotFoundException">There is none.</exception>
    public EntityState GetEntityNamed(string name) => _entitiesByName[name];

    IWorldState.IPlayerState IWorldState.Player => Player;

    T IWorldState.GetEntity<T>(string id) => CastEntity<T>(_entities[id], id);

    /// <summary>
    /// What every entity with a body has: its id, its kind's name and its name, for the story and the dialogue, which
    /// see it as it is. A kind with state of its own extends it.
    /// </summary>
    public partial class EntityState(string id, string kindName, string name) : RefCounted
    {
        /// <summary>Its configuration's id, which Core finds its state by.</summary>
        public string Id => id;

        /// <summary>Its kind's name as a speaker, such as <c>Door</c>, which its avatar is named for.</summary>
        public string KindName => kindName;

        /// <summary>Its name, such as <c>Door a6d1</c>, as a line in a dialogue file can name its speaker.</summary>
        public string Name => name;
    }

    /// <summary>The player's state, whose abilities the game owns and Core reads.</summary>
    public sealed partial class PlayerState(string id, string kindName, string name)
        : EntityState(id, kindName, name), IWorldState.IPlayerState
    {
        public bool CanPhase { get; set; } = true;
        public bool CanSpawn { get; set; } = true;

        bool IWorldState.IPlayerState.CanPhase => CanPhase;
        bool IWorldState.IPlayerState.CanSpawn => CanSpawn;
    }

    /// <summary>
    /// A door's state, which only its controller sets, through Core's view; the game reads it only, as a string, since
    /// GDScript and dialogue files can't use C# enums.
    /// </summary>
    public sealed partial class DoorState(string id, string kindName, string name, OpenableAction lastAction)
        : EntityState(id, kindName, name), IWorldState.IDoorState
    {
        /// <inheritdoc cref="IWorldState.IDoorState.LastAction"/>
        public string LastAction => ((IWorldState.IDoorState)this).LastAction.ToString();

        OpenableAction IWorldState.IDoorState.LastAction { get; set; } = lastAction;
    }
}
