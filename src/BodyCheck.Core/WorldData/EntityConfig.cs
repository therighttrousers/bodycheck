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
    public sealed record Player(Vector2 Start, Vector2 MapHalfSize, Color MapColor) : EntityConfig(Start, MapHalfSize, MapColor)
    {
        public override EntityKind Kind => EntityKind.Player;
    }

    public sealed record Robot(Vector2 Start, Vector2 MapHalfSize, Color MapColor) : EntityConfig(Start, MapHalfSize, MapColor)
    {
        public override EntityKind Kind => EntityKind.Robot;
    }

    public sealed record Platform(Vector2 Start, Vector2 MapHalfSize, Color MapColor) : EntityConfig(Start, MapHalfSize, MapColor)
    {
        public override EntityKind Kind => EntityKind.Platform;
    }

    public sealed record Block(Vector2 Start, Vector2 MapHalfSize, Color MapColor) : EntityConfig(Start, MapHalfSize, MapColor)
    {
        public override EntityKind Kind => EntityKind.Block;
    }

    public sealed record Respawn(Vector2 Start, Vector2 MapHalfSize, Color MapColor) : EntityConfig(Start, MapHalfSize, MapColor)
    {
        public override EntityKind Kind => EntityKind.Respawn;
    }
}
