using Godot;

namespace BodyCheck.Core.WorldData;

/// <summary>The kinds of entity that can be in a room.</summary>
public enum EntityKind
{
    Player,
    Robot,
    Platform,
    Block,
}

public abstract record EntityConfig(Vector2 Start)
{
    private EntityConfig(Vector2 start, bool _) : this(start) { }  // only nested types can derive

    public abstract EntityKind Kind { get; }

    public sealed record Player(Vector2 Start) : EntityConfig(Start)
    {
        public override EntityKind Kind => EntityKind.Player;
    }

    public sealed record Robot(Vector2 Start) : EntityConfig(Start)
    {
        public override EntityKind Kind => EntityKind.Robot;
    }

    public sealed record Platform(Vector2 Start) : EntityConfig(Start)
    {
        public override EntityKind Kind => EntityKind.Platform;
    }

    public sealed record Block(Vector2 Start) : EntityConfig(Start)
    {
        public override EntityKind Kind => EntityKind.Block;
    }
}
