using Godot;

namespace BodyCheck.Core.WorldData;

/// <summary>The kinds of entity that can be in a room.</summary>
public enum EntityKind
{
    Player,
    Robot,
    Platform,
    Block,
    Respawn,
}

public abstract record EntityConfig
{
    private EntityConfig() { }

    public abstract EntityKind Kind { get; }
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

    /// <summary>A hovering platform.</summary>
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

    /// <summary>A place the player can start from, such as after a reload.</summary>
    public sealed record Respawn : EntityConfig
    {
        public override EntityKind Kind => EntityKind.Respawn;
        /// <summary>What identifies the respawn when its world is read again, after its map is edited.</summary>
        public required string Id { get; init; }
    }
}
