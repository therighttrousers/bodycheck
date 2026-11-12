namespace BodyCheck.Core.WorldData;

/// <summary>
/// A room's environment: its lighting and its sky, which the game draws. The values are those of the LDtk project's
/// <c>Environment</c> enum, by name.
/// </summary>
public enum RoomEnvironment
{
    /// <summary>The environment of a level that doesn't choose one, and of every text room.</summary>
    Default,
    Testing,
    Outside,
}
