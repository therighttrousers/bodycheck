using System;
using System.Collections.Generic;
using System.Linq;

using BodyCheck.Core.Entities;
using BodyCheck.Core.WorldData;

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
public sealed class WorldState : IWorldState
{
    private readonly Dictionary<string, EntityState> _entities = [];
    private readonly Dictionary<string, EntityState> _entitiesByName = [];
    private readonly Dictionary<string, DoorState> _doors = [];

    /// <summary>The state of <paramref name="world"/>'s entities as its data says they start.</summary>
    /// <exception cref="ArgumentException">Two of its entities have the same name.</exception>
    public WorldState(World world)
    {
        foreach (EntityConfig config in world.Bodies)
        {
            var entity = new EntityState(config.Id, config.Kind, DefaultName(config, world.Bodies));
            if (!_entitiesByName.TryAdd(entity.Name, entity))
            {
                throw new ArgumentException(
                    $"In world '{world.Name}', the entities {_entitiesByName[entity.Name].Id} and {entity.Id} are both "
                        + $"named '{entity.Name}'.",
                    nameof(world));
            }
            _entities[entity.Id] = entity;

            if (config is EntityConfig.VerticalDoor door)
            {
                _doors[door.Id] = new DoorState(door.StartsClosed() ? OpenableAction.Close : OpenableAction.Open);
            }
        }
    }

    /// <summary>
    /// The name of an entity's kind as a speaker, which its avatar is named for, and which its name starts with if
    /// nothing gives it one: <c>B-CON</c> for the player, as the dialogue files name it.
    /// </summary>
    public static string KindName(EntityKind kind) => kind switch
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

    /// <summary>The player's abilities.</summary>
    public PlayerState Player { get; } = new();

    IWorldState.IPlayerState IWorldState.Player => Player;

    /// <summary>The entity with a body whose id is <paramref name="id"/>.</summary>
    /// <exception cref="KeyNotFoundException">There is none.</exception>
    public EntityState GetEntity(string id) => _entities[id];

    /// <summary>The entity with a body whose name is <paramref name="name"/>.</summary>
    /// <exception cref="KeyNotFoundException">There is none.</exception>
    public EntityState GetEntityNamed(string name) => _entitiesByName[name];

    /// <summary>The state of the door whose id is <paramref name="id"/>.</summary>
    /// <exception cref="KeyNotFoundException">There is none.</exception>
    public DoorState GetDoor(string id) => _doors[id];

    IWorldState.IDoorState IWorldState.GetDoor(string id) => GetDoor(id);

    public sealed class PlayerState : IWorldState.IPlayerState
    {
        public bool CanPhase { get; set; } = true;
        public bool CanSpawn { get; set; } = true;
    }

    /// <summary>What every entity with a body has: its id, its kind, and its name, for the story and the dialogue.</summary>
    public sealed record EntityState(string Id, EntityKind Kind, string Name);

    /// <summary>A door's state, which only its controller sets, through Core's view.</summary>
    public sealed class DoorState(OpenableAction lastAction) : IWorldState.IDoorState
    {
        /// <inheritdoc cref="IWorldState.IDoorState.LastAction"/>
        public OpenableAction LastAction { get; private set; } = lastAction;

        OpenableAction IWorldState.IDoorState.LastAction
        {
            get => LastAction;
            set => LastAction = value;
        }
    }
}
