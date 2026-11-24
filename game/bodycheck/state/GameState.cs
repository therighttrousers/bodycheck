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
/// new game, a world entered for the first time, and a level edited and read again. Nothing is saved yet.
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

    /// <summary>A new game's state, with no world's entities in it yet.</summary>
    public GameState()
    {
        Player = new PlayerState(Constants.PlayerId, PlayerKindName);
        _entities[Player.Id] = Player;
        _entitiesByName[Player.UntranslatedName] = Player;
    }

    /// <summary>A new game's state, filled in for <paramref name="world"/>.</summary>
    /// <exception cref="ArgumentException">One of its entities is named as the player is.</exception>
    public GameState(World world) : this() => Fill(world);

    /// <summary>
    /// Fills in the states that <paramref name="world"/>'s entities lack, as its data says they start, and keeps those
    /// it has, before the world is built. Each entity's name is taken from its data again, since a level may have been
    /// edited since. No two entities in the data have the same name, so a name that another entity's state still has
    /// was that entity's before an edit, and goes to this one.
    /// </summary>
    /// <exception cref="ArgumentException">One of its entities is named as the player is.</exception>
    public void Fill(World world)
    {
        foreach (EntityConfig config in world.Bodies.Concat(world.Controls))
        {
            if (!_entities.TryGetValue(config.Id, out EntityState? entity))
            {
                var kindName = KindName(config.Kind);
                entity = config switch
                {
                    EntityConfig.VerticalDoor => new DoorState(config.Id, kindName),
                    EntityConfig.Trigger trigger => new SwitchState(config.Id, kindName, trigger.StartsOn),
                    EntityConfig.Once => new SwitchState(config.Id, kindName, true),
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
    private const string KindContext = "kind";

    /// <summary>The context of a name that an entity's data gives it, in translation.</summary>
    private const string NameContext = "entity name";

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

    /// <summary>
    /// What every character that can speak has, with a body or without: its kind's name and its name, for the story and
    /// the dialogue, which see it as it is. Every speaker is a character, so that every name is translated one way.
    /// </summary>
    public partial class CharacterState(string kindName) : RefCounted
    {
        /// <summary>
        /// Its kind's name, untranslated, such as <c>Door</c>, which its avatar is named for, as <c>Door.png</c>.
        /// </summary>
        public string KindName => kindName;

        /// <summary>
        /// Its name in the current locale, translated when it is read, as a line in a dialogue file names its speaker,
        /// as <c>{{Voice.Name}}:</c>: its kind's name.
        /// </summary>
        public virtual string Name => Translatable.Translate(KindName, KindContext);

        /// <summary>Its name before translation.</summary>
        public virtual string UntranslatedName => KindName;
    }

    /// <summary>
    /// What every entity has, with a body or without: its id, and its name, the one its data gives it, or else its
    /// kind's. A kind with state of its own extends it. Every entity is a character, so that any can be made to speak.
    /// </summary>
    public partial class EntityState(string id, string kindName) : CharacterState(kindName)
    {
        /// <summary>Its configuration's id, which Core finds its state by.</summary>
        public string Id => id;

        /// <summary>
        /// The name its data gives it, untranslated, taken from its data again each time its world is filled in; or
        /// null, if it has none, and is known by its kind's name.
        /// </summary>
        internal string? GivenName { get; set; }

        /// <summary>Its name, translated, such as <c>Bridge trigger</c>, or, if its data gives it none, its kind's.</summary>
        public override string Name => GivenName is null ? base.Name : Translatable.Translate(GivenName, NameContext);

        /// <inheritdoc/>
        public override string UntranslatedName => GivenName ?? base.UntranslatedName;
    }

    /// <summary>The player's state, whose abilities the game owns and Core reads.</summary>
    public sealed partial class PlayerState(string id, string kindName)
        : EntityState(id, kindName), IGameState.IPlayerState
    {
        public bool CanPhase { get; set; } = true;
        public bool CanSpawn { get; set; } = true;

        bool IGameState.IPlayerState.CanPhase => CanPhase;
        bool IGameState.IPlayerState.CanSpawn => CanSpawn;
    }

    /// <summary>
    /// A door's state, which only its controller sets, through Core's view; the game reads it only, as a string, since
    /// GDScript and dialogue files can't use C# enums.
    /// </summary>
    public sealed partial class DoorState(string id, string kindName)
        : EntityState(id, kindName), IGameState.IDoorState
    {
        /// <inheritdoc cref="IGameState.IDoorState.LastAction"/>
        public string LastAction => ((IGameState.IDoorState)this).LastAction.ToString();

        OpenableAction IGameState.IDoorState.LastAction { get; set; } = OpenableAction.None;
    }

    /// <summary>
    /// The state of a trigger or a once, which says whether it is on. What runs it is its only writer, through Core's
    /// view; the game reads it only.
    /// </summary>
    public sealed partial class SwitchState(string id, string kindName, bool isOn)
        : EntityState(id, kindName), IGameState.ISwitchState
    {
        /// <inheritdoc cref="IGameState.ISwitchState.IsOn"/>
        public bool IsOn => ((IGameState.ISwitchState)this).IsOn;

        bool IGameState.ISwitchState.IsOn { get; set; } = isOn;
    }
}
