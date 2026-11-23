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
    // Every entity with a body that a world filled in has had, by id.
    private readonly Dictionary<string, EntityState> _entities = [];

    // Every trigger's and once's switch, by id.
    private readonly Dictionary<string, SwitchState> _switches = [];

    // Each world's current respawn's id, by the world's name.
    private readonly Dictionary<string, string> _respawnIds = [];

    // The entities with bodies of the world last filled in, by their names before translation, which are unique in it.
    private Dictionary<string, EntityState> _entitiesByName;

    /// <summary>A new game's state, with no world's entities in it yet.</summary>
    public GameState()
    {
        Player = new PlayerState(Constants.PlayerId, PlayerKindName);
        _entities[Player.Id] = Player;
        _entitiesByName = new() { [Player.UntranslatedName] = Player };
    }

    /// <summary>A new game's state, filled in for <paramref name="world"/>.</summary>
    /// <exception cref="ArgumentException">Two of its entities have the same name.</exception>
    public GameState(World world) : this() => Fill(world);

    /// <summary>
    /// Fills in the states that <paramref name="world"/>'s entities lack, as its data says they start, and keeps those
    /// it has, before the world is built. Names are worked out again from the world's data each time, since adding an
    /// entity to a level can lengthen another's, and must be unique in the world.
    /// </summary>
    /// <exception cref="ArgumentException">Two of its entities have the same name.</exception>
    public void Fill(World world)
    {
        var byName = new Dictionary<string, EntityState> { [Player.UntranslatedName] = Player };
        foreach (EntityConfig config in world.Bodies)
        {
            if (!_entities.TryGetValue(config.Id, out EntityState? entity))
            {
                var kindName = KindName(config.Kind);
                entity = config switch
                {
                    EntityConfig.VerticalDoor door => new DoorState(config.Id, kindName,
                        door.StartsClosed() ? OpenableAction.Close : OpenableAction.Open),
                    _ => new EntityState(config.Id, kindName),
                };
                _entities[config.Id] = entity;
            }
            entity.Serial = DefaultSerial(config, world.Bodies);
            if (!byName.TryAdd(entity.UntranslatedName, entity))
            {
                throw new ArgumentException(
                    $"In world '{world.Name}', the entities {byName[entity.UntranslatedName].Id} and {entity.Id} "
                        + $"are both named '{entity.UntranslatedName}'.",
                    nameof(world));
            }
        }
        foreach (EntityConfig config in world.Controls)
        {
            bool? startsOn = config switch
            {
                EntityConfig.Trigger trigger => trigger.StartsOn,
                EntityConfig.Once => true,
                _ => null,
            };
            if (startsOn is bool isOn)
            {
                _switches.TryAdd(config.Id, new SwitchState(config.Id, isOn));
            }
        }
        _entitiesByName = byName;
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
    /// The name of an entity's kind as a speaker, untranslated, which its avatar is named for, and which its name
    /// starts with.
    /// </summary>
    private static string KindName(EntityKind kind) => kind switch
    {
        EntityKind.VerticalDoor => Translatable.Mark("Door", KindContext),
        _ => kind.ToString(),
    };

    /// <summary>The context of a kind's name, as a speaker, in translation.</summary>
    private const string KindContext = "kind";

    /// <summary>
    /// The serial number in an entity's name, after its kind's: the start of its id, such as <c>a6d1</c>, at least
    /// <see cref="ShortIdLength"/> characters, and as many more as tell it from every other id of its kind in
    /// <paramref name="bodies"/>, as git shortens a commit's hash, so that names are unique in a world.
    /// </summary>
    private static string DefaultSerial(EntityConfig config, IEnumerable<EntityConfig> bodies)
    {
        int length = bodies
            .Where(other => other.Kind == config.Kind && other != config)
            .Select(other => CommonPrefixLength(config.Id, other.Id) + 1)
            .Append(ShortIdLength)
            .Max();
        return config.Id[..Math.Min(length, config.Id.Length)];
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

    /// <summary>The Voice, which calls the player, and speaks with no body.</summary>
    public CharacterState Voice { get; } = new(Translatable.Mark("Voice", KindContext));

    /// <summary>The entity with a body whose id is <paramref name="id"/>.</summary>
    /// <exception cref="KeyNotFoundException">There is none.</exception>
    public EntityState GetEntity(string id) => _entities[id];

    /// <summary>The entity with a body whose id is <paramref name="id"/>, as the state of its kind.</summary>
    /// <exception cref="KeyNotFoundException">There is none.</exception>
    /// <exception cref="InvalidOperationException">Its state isn't a <typeparamref name="T"/>.</exception>
    public T GetEntity<T>(string id) where T : EntityState => CastEntity<T>(GetEntity(id), id);

    /// <summary>
    /// The entity with a body in the world last filled in whose untranslated name is <paramref name="name"/>, such as
    /// <c>Door a6d1</c>.
    /// </summary>
    /// <exception cref="KeyNotFoundException">There is none.</exception>
    public EntityState GetEntityNamed(string name) => _entitiesByName[name];

    /// <summary>
    /// The character whose name, translated, is <paramref name="name"/>, as a dialogue line gives its speaker, or null
    /// if there is none: the Voice, or an entity with a body in the world last filled in.
    /// </summary>
    public CharacterState? GetSpeaker(string name) =>
        Voice.Name == name ? Voice : _entitiesByName.Values.FirstOrDefault(entity => entity.Name == name);

    /// <summary>The switch of the trigger or once whose id is <paramref name="id"/>.</summary>
    /// <exception cref="KeyNotFoundException">There is none.</exception>
    public SwitchState GetSwitch(string id) => _switches[id];

    IGameState.IPlayerState IGameState.Player => Player;

    T IGameState.GetEntity<T>(string id) => _entities.TryGetValue(id, out EntityState? entity)
        ? CastEntity<T>(entity, id)
        : CastEntity<T>(_switches[id], id);

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

        /// <summary>Its name before translation, which no two entities in a world share.</summary>
        public virtual string UntranslatedName => KindName;
    }

    /// <summary>
    /// What every entity with a body has: its id, and its name, its kind's and a serial number, if it has one. A kind
    /// with state of its own extends it.
    /// </summary>
    public partial class EntityState(string id, string kindName) : CharacterState(kindName)
    {
        /// <summary>Its configuration's id, which Core finds its state by.</summary>
        public string Id => id;

        /// <summary>
        /// The serial number in its name, worked out from its world's data each time the world is filled in, or null
        /// for the player, whose name is its kind's.
        /// </summary>
        internal string? Serial { get; set; }

        /// <summary>Its kind's name, translated, then its serial number, such as <c>Door a6d1</c>, if it has one.</summary>
        public override string Name => Serial is null
            ? base.Name
            // TRANSLATORS: An entity's name. {kind} is its kind's name, translated, such as "Door", and {serial} is
            // the start of its id, such as "a6d1", a serial number, which stays as it is.
            : Translatable.Translate("{kind} {serial}", "entity name")
                .Replace("{kind}", base.Name)
                .Replace("{serial}", Serial);

        /// <inheritdoc/>
        public override string UntranslatedName => Serial is null ? base.UntranslatedName : $"{KindName} {Serial}";
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
    public sealed partial class DoorState(string id, string kindName, OpenableAction lastAction)
        : EntityState(id, kindName), IGameState.IDoorState
    {
        /// <inheritdoc cref="IGameState.IDoorState.LastAction"/>
        public string LastAction => ((IGameState.IDoorState)this).LastAction.ToString();

        OpenableAction IGameState.IDoorState.LastAction { get; set; } = lastAction;
    }

    /// <summary>
    /// The switch of a trigger or a once, which says whether it is on, and has no name, since a control entity isn't a
    /// character. What runs it is its only writer, through Core's view; the game reads it only.
    /// </summary>
    public sealed partial class SwitchState(string id, bool isOn) : RefCounted, IGameState.ISwitchState
    {
        /// <summary>Its configuration's id, which Core finds its state by.</summary>
        public string Id => id;

        /// <inheritdoc cref="IGameState.ISwitchState.IsOn"/>
        public bool IsOn => ((IGameState.ISwitchState)this).IsOn;

        bool IGameState.ISwitchState.IsOn { get; set; } = isOn;
    }
}
