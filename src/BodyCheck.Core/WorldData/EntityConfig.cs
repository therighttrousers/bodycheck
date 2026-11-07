using Godot;

namespace BodyCheck.Core.WorldData;

/// <summary>The kinds of entity that can be in a room.</summary>
public enum EntityKind
{
    Player,
    Robot,
    Platform,
    Block,
    VerticalDoor,
    Respawn,
    Trigger,
    Sequence,
    Fanout,
    Once,
    Dialogue,
    Script,
}

/// <summary>What each kind of entity is: whether it has a body, and which actions it responds to.</summary>
public static class EntityKinds
{
    private static readonly TriggerAction[] None = [];
    private static readonly TriggerAction[] Default = [TriggerAction.Default];
    private static readonly TriggerAction[] OnOff = [
        TriggerAction.ToggleOnOff,
        TriggerAction.TurnOn,
        TriggerAction.TurnOff
    ];
    private static readonly TriggerAction[] OpenClose = [
        TriggerAction.ToggleOpenClose,
        TriggerAction.Open,
        TriggerAction.Close
    ];

    extension(EntityKind kind)
    {
        /// <summary>
        /// Whether an entity of this kind has a body, which the world builds and steps. One that hasn't is a control
        /// entity: what it does is run actions, or respond to them.
        /// </summary>
        public bool HasBody => kind switch
        {
            EntityKind.Player or EntityKind.Robot or EntityKind.Platform or EntityKind.Block or EntityKind.VerticalDoor
                => true,
            EntityKind.Respawn or EntityKind.Trigger or EntityKind.Sequence or EntityKind.Fanout or EntityKind.Once
                or EntityKind.Dialogue or EntityKind.Script => false,
        };

        /// <summary>
        /// The actions an entity of this kind responds to: <see cref="TriggerAction.Default"/> alone for a kind that
        /// does one thing, and otherwise actions that don't include it, so that <c>Default</c> never stands for one of
        /// several.
        /// </summary>
        public IReadOnlyList<TriggerAction> Responses => kind switch
        {
            EntityKind.Player or EntityKind.Robot or EntityKind.Platform or EntityKind.Block => None,
            EntityKind.Trigger => OnOff,
            EntityKind.VerticalDoor => OpenClose,
            EntityKind.Respawn or EntityKind.Sequence or EntityKind.Fanout or EntityKind.Once or EntityKind.Dialogue
                or EntityKind.Script => Default,
        };
    }
}

/// <summary>What sets a trigger off, as LDtk's enum of the same name.</summary>
public enum TriggerSetOffBy
{
    /// <summary>The player coming into its area.</summary>
    Touch,
    /// <summary>The player pressing up in its area, on the ground, while playing.</summary>
    Interact,
}

/// <summary>One step of a route: an action, and the id of the entity to run it on.</summary>
public sealed record Step(string TargetId, TriggerAction Action);

public abstract record EntityConfig
{
    private EntityConfig() { }

    public abstract EntityKind Kind { get; }

    /// <summary>
    /// What identifies the entity in its world, and when its world is read again after its map is edited. Routes name
    /// their targets by it.
    /// </summary>
    public required string Id { get; init; }

    /// <summary>The lower-left tile in the map editor, in tiles. (0,0) is the lower-left tile in the level.</summary>
    public required Vector2I MapOrigin { get; init; }
    /// <summary>The size of the entity in the map editor, in tiles.</summary>
    public required Vector2I MapSize { get; init; }
    /// <summary>The color of the entity in the map editor.</summary>
    public required Color MapColor { get; init; }

    /// <summary>The middle of the entity's box in the map editor, in tiles.</summary>
    public Vector2 MapCenter => MapOrigin + (Vector2)MapSize / 2f;

    /// <summary>The same configuration, with every place in it moved by <paramref name="offset"/>.</summary>
    public virtual EntityConfig MovedBy(Vector2I offset) => this with { MapOrigin = MapOrigin + offset };

    public sealed record Player : EntityConfig
    {
        public override EntityKind Kind => EntityKind.Player;
    }

    public sealed record Robot : EntityConfig
    {
        public override EntityKind Kind => EntityKind.Robot;
    }

    /// <summary>A hovering platform, one tile high.</summary>
    public sealed record Platform : EntityConfig
    {
        public override EntityKind Kind => EntityKind.Platform;
        /// <summary>The cell whose middle is one end of the line that the middle of the platform's box moves along.</summary>
        public required Vector2I RangeA { get; init; }
        /// <summary>The cell at the other end, which the platform moves toward first. Never the same as RangeA.</summary>
        public required Vector2I RangeB { get; init; }

        public override EntityConfig MovedBy(Vector2I offset) =>
            this with { MapOrigin = MapOrigin + offset, RangeA = RangeA + offset, RangeB = RangeB + offset };
    }

    public sealed record Block : EntityConfig
    {
        public override EntityKind Kind => EntityKind.Block;
    }

    /// <summary>
    /// A door that slides down to close and up to open, between its stops. It may start anywhere between them.
    /// </summary>
    public sealed record VerticalDoor : EntityConfig
    {
        public override EntityKind Kind => EntityKind.VerticalDoor;
        /// <summary>The cell above the door that it stops against when it opens, in one of its columns.</summary>
        public required Vector2I OpenStop { get; init; }
        /// <summary>The cell below the door that it stops against when it closes, in one of its columns.</summary>
        public required Vector2I CloseStop { get; init; }

        public override EntityConfig MovedBy(Vector2I offset) =>
            this with { MapOrigin = MapOrigin + offset, OpenStop = OpenStop + offset, CloseStop = CloseStop + offset };

        /// <summary>Whether the door starts closed: whether its center is closer to the close stop than the open stop.</summary>
        public bool StartsClosed()
        {
            var half = new Vector2(0.5f, 0.5f);
            var center = MapOrigin + (Vector2)MapSize / 2;
            return (CloseStop + half - center).LengthSquared() < (OpenStop + half - center).LengthSquared();
        }
    }

    /// <summary>A place the player can start from, such as after a reload.</summary>
    public sealed record Respawn : EntityConfig
    {
        public override EntityKind Kind => EntityKind.Respawn;
    }

    /// <summary>
    /// An area that, when the player sets it off, runs its step, if it is on. It responds to being turned on and off.
    /// </summary>
    public sealed record Trigger : EntityConfig
    {
        public override EntityKind Kind => EntityKind.Trigger;
        public required Step Step { get; init; }
        /// <summary>Whether it starts on. One that is off isn't set off.</summary>
        public required bool StartsOn { get; init; }
        public required TriggerSetOffBy SetOffBy { get; init; }
    }

    /// <summary>Runs its steps in turn, each when the one before has finished.</summary>
    public sealed record Sequence : EntityConfig
    {
        public override EntityKind Kind => EntityKind.Sequence;
        /// <summary>The steps, at least one.</summary>
        public required IReadOnlyList<Step> Steps { get; init; }
    }

    /// <summary>Runs its steps all at once.</summary>
    public sealed record Fanout : EntityConfig
    {
        public override EntityKind Kind => EntityKind.Fanout;
        /// <summary>The steps, at least one.</summary>
        public required IReadOnlyList<Step> Steps { get; init; }
    }

    /// <summary>
    /// Runs its step the first time it is asked, and gives every later request that first run: one made while it runs
    /// finishes when it does, and one made after finishes at once.
    /// </summary>
    public sealed record Once : EntityConfig
    {
        public override EntityKind Kind => EntityKind.Once;
        public required Step Step { get; init; }
    }

    /// <summary>Presents a title of a dialogue file.</summary>
    public sealed record Dialogue : EntityConfig
    {
        public override EntityKind Kind => EntityKind.Dialogue;
        /// <summary>The dialogue file, by its name.</summary>
        public required string File { get; init; }
        /// <summary>The title in the file to present.</summary>
        public required string Title { get; init; }
    }

    /// <summary>Runs a script, a new one for each run.</summary>
    public sealed record Script : EntityConfig
    {
        public override EntityKind Kind => EntityKind.Script;
        /// <summary>The script's class, by the name the game's script library knows it by.</summary>
        public required string ClassName { get; init; }
    }
}
