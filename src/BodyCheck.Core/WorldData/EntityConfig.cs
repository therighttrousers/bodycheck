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

public abstract record EntityConfig(Vector2 Start, Vector2 MapHalfSize, Color MapColor)
{
    // Only nested types can derive because of this:
    private EntityConfig(Vector2 start, Vector2 mapHalfSize, Color mapColor, bool _) : this(start, mapHalfSize, mapColor) { }

    public abstract EntityKind Kind { get; }

    /// <summary>The same configuration, with every place in it moved by <paramref name="offset"/>.</summary>
    public virtual EntityConfig MovedBy(Vector2 offset) => this with { Start = Start + offset };

    public sealed record Player(Vector2 Start, Vector2 MapHalfSize, Color MapColor) : EntityConfig(Start, MapHalfSize, MapColor)
    {
        public override EntityKind Kind => EntityKind.Player;
    }

    public sealed record Robot(Vector2 Start, Vector2 MapHalfSize, Color MapColor) : EntityConfig(Start, MapHalfSize, MapColor)
    {
        public override EntityKind Kind => EntityKind.Robot;
    }

    /// <summary>A hovering platform.</summary>
    /// <param name="RangeA">One end of the line that the middle of the platform's box moves along.</param>
    /// <param name="RangeB">The other end, which the platform moves toward first. Never the same as RangeA.</param>
    public sealed record Platform(Vector2 Start, Vector2 MapHalfSize, Color MapColor, Vector2 RangeA, Vector2 RangeB)
        : EntityConfig(Start, MapHalfSize, MapColor)
    {
        public override EntityKind Kind => EntityKind.Platform;

        public override EntityConfig MovedBy(Vector2 offset) =>
            this with { Start = Start + offset, RangeA = RangeA + offset, RangeB = RangeB + offset };
    }

    public sealed record Block(Vector2 Start, Vector2 MapHalfSize, Color MapColor) : EntityConfig(Start, MapHalfSize, MapColor)
    {
        public override EntityKind Kind => EntityKind.Block;
    }

    /// <summary>A place the player can start from, such as after a reload.</summary>
    /// <param name="Id">What identifies the respawn when its world is read again, after its map is edited.</param>
    public sealed record Respawn(Vector2 Start, Vector2 MapHalfSize, Color MapColor, string Id)
        : EntityConfig(Start, MapHalfSize, MapColor)
    {
        public override EntityKind Kind => EntityKind.Respawn;
    }
}
