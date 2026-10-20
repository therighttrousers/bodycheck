namespace BodyCheck.Core;

/// <summary>
/// The physics layers defined for this project. <b>These MUST be kept in sync with project.godot</b>, which names each
/// layer of one bit the same; a test in the game tests checks it.
/// </summary>
[Flags]
public enum PhysicsLayer : uint
{
    None = 0,
    /// <summary>Any static object with collision in the game world. Currently just tiles.</summary>
    Static = 1 << 0,
    /// <summary>Any liquid, including (if we ever have it) wind.</summary>
    Liquid = 1 << 1,
    /// <summary>Regions of space that *do something* when touched.</summary>
    Trigger = 1 << 2,
    /// <summary>Non-character entities like crates and rocks.</summary>
    Prop = 1 << 3,
    /// <summary>Character entities like the player, robots, drones, platforms and doors.</summary>
    Character = 1 << 4,
    /// <summary>Any Entity.</summary>
    EntityCollisionMask = Prop | Character,
    DefaultCollisionMask = Static | EntityCollisionMask,
}
