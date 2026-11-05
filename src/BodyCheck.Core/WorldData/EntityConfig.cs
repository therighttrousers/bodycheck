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
    Dialogue,
}

/// <summary>What each kind of entity is: whether it has a body, and which actions it responds to.</summary>
public static class EntityKinds
{
    private static readonly TriggerAction[] None = [];
    private static readonly TriggerAction[] Default = [TriggerAction.Default];
    private static readonly TriggerAction[] Close = [TriggerAction.Close];

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
            EntityKind.Respawn or EntityKind.Trigger or EntityKind.Sequence or EntityKind.Fanout or EntityKind.Dialogue
                => false,
        };

        /// <summary>
        /// The actions an entity of this kind responds to: <see cref="TriggerAction.Default"/> alone for a kind that
        /// does one thing, and otherwise actions that don't include it, so that <c>Default</c> never stands for one of
        /// several.
        /// </summary>
        public IReadOnlyList<TriggerAction> Responses => kind switch
        {
            EntityKind.Player or EntityKind.Robot or EntityKind.Platform or EntityKind.Block => None,
            EntityKind.VerticalDoor => Close,
            EntityKind.Respawn or EntityKind.Trigger or EntityKind.Sequence or EntityKind.Fanout or EntityKind.Dialogue
                => Default,
        };
    }
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

    /// <summary>A door that slides down to close.</summary>
    public sealed record VerticalDoor : EntityConfig
    {
        public override EntityKind Kind => EntityKind.VerticalDoor;
    }

    /// <summary>A place the player can start from, such as after a reload.</summary>
    public sealed record Respawn : EntityConfig
    {
        public override EntityKind Kind => EntityKind.Respawn;
    }

    /// <summary>An area that, when touched, runs its step.</summary>
    public sealed record Trigger : EntityConfig
    {
        public override EntityKind Kind => EntityKind.Trigger;
        public required Step Step { get; init; }
        /// <summary>Whether it runs only once.</summary>
        public required bool Once { get; init; }
    }

    /// <summary>Runs its steps in turn, each when the one before has finished.</summary>
    public sealed record Sequence : EntityConfig
    {
        public override EntityKind Kind => EntityKind.Sequence;
        /// <summary>The steps, at least one.</summary>
        public required IReadOnlyList<Step> Steps { get; init; }
        /// <summary>Whether it runs only once.</summary>
        public required bool Once { get; init; }
    }

    /// <summary>Runs its steps all at once.</summary>
    public sealed record Fanout : EntityConfig
    {
        public override EntityKind Kind => EntityKind.Fanout;
        /// <summary>The steps, at least one.</summary>
        public required IReadOnlyList<Step> Steps { get; init; }
        /// <summary>Whether it runs only once.</summary>
        public required bool Once { get; init; }
    }

    /// <summary>Presents a title of a dialogue file.</summary>
    public sealed record Dialogue : EntityConfig
    {
        public override EntityKind Kind => EntityKind.Dialogue;
        /// <summary>The dialogue file, by its name.</summary>
        public required string File { get; init; }
        /// <summary>The title in the file to present.</summary>
        public required string Title { get; init; }
        /// <summary>The fraction of the glyphs shown garbled, from 0 to 1.</summary>
        public required float Garble { get; init; }
    }
}
