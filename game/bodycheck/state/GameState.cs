using System;
using System.Collections.Generic;
using System.Linq;

using BodyCheck.Core;
using BodyCheck.Core.Entities;
using BodyCheck.Core.WorldData;

using Godot;

namespace BodyCheck.State;

/// <summary>
/// The game's state, which the game owns: everything the story, the dialogue and the game's rules read or change. Core
/// sees part of it, as <see cref="IGameState"/>, which is where the rules write what they own, such as a door's last
/// action; the game's own view of those parts lets it read them only, and change them by asking for an action.
/// <para>
/// It outlives worlds: a new game makes one, and every world built is given it, so that a restart, or a switch to
/// another world and back, keeps what has happened. It is filled in lazily: before a world is built, <see cref="Fill"/>
/// adds the states its entities lack, as its data says they start, and keeps those it has, so that one path serves a
/// new game, a world entered for the first time, a level edited and read again, and a save loaded.
/// </para>
/// <para>
/// A save holds only what the levels' data can't make again, as a <see cref="SaveData"/>. Each entity's state keeps
/// what is saved of it in a record of its own, an <see cref="EntityData"/>, which is all it holds besides its id and its
/// name, so that what is saved and what the game sees can't drift apart; a kind's state and its record are in one
/// file, named for the state. Loading a save fills in nothing itself:
/// <see cref="Fill"/> makes each state an entity lacks from the save's record for it, if there is one, and from the
/// data if not, and the records for worlds not filled in yet are kept, and written again as they are.
/// </para>
/// </summary>
public sealed partial class GameState : RefCounted, IGameState
{
    // Every entity that a world filled in has had, with a body or without, and the player, by id.
    private readonly Dictionary<string, EntityState> _entities = [];

    // Every entity with a name given in its data, and the player, by that name.
    private readonly Dictionary<string, EntityState> _entitiesByName = [];

    // Each world's current respawn's id, by the world's name.
    private readonly Dictionary<string, string> _respawnIds = [];

    // The loaded save's records of entities that no world filled in has had yet, by id.
    private readonly Dictionary<string, EntityData> _saved = [];

    /// <summary>A new game's state, with no world's entities in it yet.</summary>
    public GameState() : this(DateTimeOffset.Now, story: null, player: null) { }

    /// <summary>A new game's state, filled in for <paramref name="world"/>.</summary>
    /// <exception cref="ArgumentException">One of its entities is named as the player is.</exception>
    public GameState(World world) : this() => Fill(world);

    /// <summary>The state that <paramref name="save"/> holds, with no world's entities in it yet.</summary>
    public GameState(SaveData save)
        : this(save.Started, save.Story, save.Entities.GetValueOrDefault(Constants.PlayerId) as PlayerData)
    {
        WorldName = save.WorldName;
        foreach ((string worldName, string respawnId) in save.RespawnIds)
        {
            _respawnIds[worldName] = respawnId;
        }
        foreach ((string id, EntityData data) in save.Entities)
        {
            if (id != Constants.PlayerId)
            {
                _saved[id] = data;
            }
        }
    }

    private GameState(DateTimeOffset started, StoryState? story, PlayerData? player)
    {
        Started = started;
        Story = story ?? new();
        Player = new PlayerState(Constants.PlayerId, PlayerKindName, player ?? new());
        _entities[Player.Id] = Player;
        _entitiesByName[Player.UntranslatedName] = Player;
    }

    /// <summary>When the game was started.</summary>
    public DateTimeOffset Started { get; }

    /// <summary>
    /// What a save of the state holds, saved at <paramref name="saved"/>: what has happened in every world filled in,
    /// and what the loaded save, if any, held for the worlds not filled in yet. It shares its records with the state,
    /// so write it at once.
    /// </summary>
    public SaveData ToSave(DateTimeOffset saved)
    {
        var entities = new Dictionary<string, EntityData>(_saved);
        foreach (EntityState entity in _entities.Values)
        {
            if (entity.Saved is EntityData data)
            {
                entities[entity.Id] = data;
            }
        }
        return new SaveData
        {
            Version = SaveData.CurrentVersion,
            Started = Started,
            Saved = saved,
            WorldName = WorldName,
            Story = Story,
            RespawnIds = new Dictionary<string, string>(_respawnIds),
            Entities = entities,
        };
    }

    /// <summary>
    /// Fills in the states that <paramref name="world"/>'s entities lack, as the loaded save, if any, says they are, or
    /// else as its data says they start, and keeps those it has, before the world is built. A saved record of the wrong
    /// kind is ignored. Each entity's name is taken from its data again, since a level may have been edited since. No
    /// two entities in the data have the same name, so a name that another entity's state still has was that entity's
    /// before an edit, and goes to this one.
    /// </summary>
    /// <exception cref="ArgumentException">One of its entities is named as the player is.</exception>
    public void Fill(World world)
    {
        foreach (EntityConfig config in world.Bodies.Concat(world.Controls))
        {
            if (!_entities.TryGetValue(config.Id, out EntityState? entity))
            {
                var kindName = KindName(config.Kind);
                _saved.Remove(config.Id, out EntityData? saved);
                entity = config switch
                {
                    EntityConfig.VerticalDoor => new DoorState(config.Id, kindName, saved as DoorData ?? new()),
                    EntityConfig.Trigger trigger =>
                        new SwitchState(config.Id, kindName, saved as SwitchData ?? new() { IsOn = trigger.StartsOn }),
                    EntityConfig.Once => new SwitchState(config.Id, kindName, saved as SwitchData ?? new() { IsOn = true }),
                    _ => new EntityState(config.Id, kindName),
                };
                _entities[config.Id] = entity;
            }
            Rename(entity, config.Name);
        }
    }

    /// <summary>Gives <paramref name="entity"/> the name <paramref name="name"/>, or none, to be found by.</summary>
    private void Rename(EntityState entity, string? name)
    {
        if (entity.GivenName is string old && _entitiesByName.GetValueOrDefault(old) == entity)
        {
            _entitiesByName.Remove(old);
        }
        if (name is not null && _entitiesByName.GetValueOrDefault(name) == Player)
        {
            throw new ArgumentException($"Entity {entity.Id} is named '{name}', as the player is.", nameof(name));
        }
        entity.GivenName = name;
        if (name is not null)
        {
            _entitiesByName[name] = entity;
        }
    }

    /// <summary>The name of the world being played, which the game sets when it builds one, or null before any.</summary>
    public string? WorldName { get; set; }

    /// <summary>What has happened in the story, beat by beat, which the dialogue reads and sets.</summary>
    public StoryState Story { get; }

    /// <summary>
    /// The id of the current respawn of the world named <paramref name="worldName"/>, or null if it has none yet, so
    /// that the player starts at its <c>PlayerStart</c>.
    /// </summary>
    public string? GetRespawnId(string worldName) => _respawnIds.GetValueOrDefault(worldName);

    /// <summary>
    /// Makes the respawn whose id is <paramref name="respawnId"/> the current one of the world named
    /// <paramref name="worldName"/>.
    /// </summary>
    public void SetRespawnId(string worldName, string respawnId) => _respawnIds[worldName] = respawnId;

    /// <summary>
    /// The player's kind's name as a speaker, untranslated, which is its name too: <c>B-CON</c>, which in English
    /// sounds like "beacon".
    /// </summary>
    private static readonly string PlayerKindName = Translatable.Mark("B-CON", KindContext);

    /// <summary>
    /// The name of an entity's kind as a speaker, untranslated, which its avatar is named for, and which is its name if
    /// its data gives it none.
    /// </summary>
    private static string KindName(EntityKind kind) => kind switch
    {
        EntityKind.VerticalDoor => Translatable.Mark("Door", KindContext),
        _ => kind.ToString(),
    };

    /// <summary>The context of a kind's name, as a speaker, in translation.</summary>
    internal const string KindContext = "kind";

    /// <summary>The context of a name that an entity's data gives it, in translation.</summary>
    internal const string NameContext = "entity name";

    private static T CastEntity<T>(object entity, string id) => entity switch
    {
        T value => value,
        var value => throw new InvalidOperationException($"Entity {id} is a {value.GetType().Name}, not a {typeof(T).Name}."),
    };

    /// <summary>The player's state; e.g. its abilities.</summary>
    public PlayerState Player { get; }

    /// <summary>The Voice, which calls the player, and speaks with no body.</summary>
    public CharacterState Voice { get; } = new(Translatable.Mark("Voice", KindContext));

    /// <summary>The entity whose id is <paramref name="id"/>.</summary>
    /// <exception cref="KeyNotFoundException">There is none.</exception>
    public EntityState GetEntity(string id) => _entities[id];

    /// <summary>The entity whose id is <paramref name="id"/>, as the state of its kind.</summary>
    /// <exception cref="KeyNotFoundException">There is none.</exception>
    /// <exception cref="InvalidOperationException">Its state isn't a <typeparamref name="T"/>.</exception>
    public T GetEntity<T>(string id) where T : EntityState => CastEntity<T>(GetEntity(id), id);

    /// <summary>
    /// The entity whose name, untranslated, as its data gives it, is <paramref name="name"/>, such as
    /// <c>Bridge trigger</c>, in any world filled in so far; or the player, as <c>B-CON</c>. An entity whose data gives
    /// it no name can't be found by name.
    /// </summary>
    /// <exception cref="KeyNotFoundException">There is none.</exception>
    public EntityState GetEntityNamed(string name) => _entitiesByName[name];

    /// <summary>
    /// The character whose name, translated, is <paramref name="name"/>, as a dialogue line gives its speaker, or null
    /// if there is none: the Voice, or an entity in any world filled in so far. Entities that have no names of their
    /// own share their kind's, and so their avatar, so any one of them will do.
    /// </summary>
    public CharacterState? GetSpeaker(string name) =>
        Voice.Name == name ? Voice : _entities.Values.FirstOrDefault(entity => entity.Name == name);

    IGameState.IPlayerState IGameState.Player => Player;

    T IGameState.GetEntity<T>(string id) => CastEntity<T>(_entities[id], id);
}
