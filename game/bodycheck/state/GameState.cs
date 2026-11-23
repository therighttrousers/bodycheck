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
/// It is filled in from a world's data before the world is built, so that the world finds each entity's state there.
/// For now it lasts as long as the world it was filled in from: nothing is saved, and a world that is built again
/// starts again.
/// </para>
/// </summary>
public sealed partial class GameState : RefCounted, IGameState
{
    private readonly Dictionary<string, EntityState> _entities = [];
    private readonly Dictionary<string, EntityState> _entitiesByName = [];

    /// <summary>The state of <paramref name="world"/>'s entities as its data says they start.</summary>
    /// <exception cref="ArgumentException">Two of its entities have the same name.</exception>
    public GameState(World world)
    {
        Player = new PlayerState(Constants.PlayerId, PlayerKindName);
        _entitiesByName[Player.UntranslatedName] = Player;
        _entities[Player.Id] = Player;
        foreach (EntityConfig config in world.Bodies)
        {
            var kindName = KindName(config.Kind);
            var serial = DefaultSerial(config, world.Bodies);
            EntityState entity = config switch
            {
                EntityConfig.VerticalDoor door => new DoorState(config.Id, kindName, serial,
                    door.StartsClosed() ? OpenableAction.Close : OpenableAction.Open),
                _ => new EntityState(config.Id, kindName, serial),
            };
            if (!_entitiesByName.TryAdd(entity.UntranslatedName, entity))
            {
                throw new ArgumentException(
                    $"In world '{world.Name}', the entities {_entitiesByName[entity.UntranslatedName].Id} and {entity.Id} "
                        + $"are both named '{entity.UntranslatedName}'.",
                    nameof(world));
            }
            _entities[entity.Id] = entity;
        }
    }

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
    /// The serial number in an entity's name, after its kind's: the start of its id, such as <c>a6d1</c>, at least <see cref="ShortIdLength"/> characters, and as many more as tell
    /// it from every other id of its kind in <paramref name="bodies"/>, as git shortens a commit's hash, so that names
    /// are unique.
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

    /// <summary>The entity with a body whose untranslated name is <paramref name="name"/>, such as <c>Door a6d1</c>.</summary>
    /// <exception cref="KeyNotFoundException">There is none.</exception>
    public EntityState GetEntityNamed(string name) => _entitiesByName[name];

    /// <summary>
    /// The character whose name, translated, is <paramref name="name"/>, as a dialogue line gives its speaker, or null
    /// if there is none: the Voice, or an entity with a body.
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

        /// <summary>Its name before translation, which no two entities share.</summary>
        public virtual string UntranslatedName => KindName;
    }

    /// <summary>
    /// What every entity with a body has: its id, and its name, its kind's and a serial number, if it has one. A kind
    /// with state of its own extends it.
    /// </summary>
    public partial class EntityState(string id, string kindName, string? serial) : CharacterState(kindName)
    {
        /// <summary>Its configuration's id, which Core finds its state by.</summary>
        public string Id => id;

        /// <summary>Its kind's name, translated, then its serial number, such as <c>Door a6d1</c>, if it has one.</summary>
        public override string Name => serial is null
            ? base.Name
            // TRANSLATORS: An entity's name. {kind} is its kind's name, translated, such as "Door", and {serial} is
            // the start of its id, such as "a6d1", a serial number, which stays as it is.
            : Translatable.Translate("{kind} {serial}", "entity name")
                .Replace("{kind}", base.Name)
                .Replace("{serial}", serial);

        /// <inheritdoc/>
        public override string UntranslatedName => serial is null ? base.UntranslatedName : $"{KindName} {serial}";
    }

    /// <summary>The player's state, whose abilities the game owns and Core reads.</summary>
    public sealed partial class PlayerState(string id, string kindName)
        : EntityState(id, kindName, null), IGameState.IPlayerState
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
    public sealed partial class DoorState(string id, string kindName, string serial, OpenableAction lastAction)
        : EntityState(id, kindName, serial), IGameState.IDoorState
    {
        /// <inheritdoc cref="IGameState.IDoorState.LastAction"/>
        public string LastAction => ((IGameState.IDoorState)this).LastAction.ToString();

        OpenableAction IGameState.IDoorState.LastAction { get; set; } = lastAction;
    }
}
