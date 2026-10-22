namespace BodyCheck.Core;

/// <summary>
/// Something that happened to an entity, for the scene that draws it to react to: for now, by playing the sound named
/// after it, if it has one. A controller raises it on its entity (decision record 021).
/// </summary>
public enum EntityCue
{
    /// <summary>The player jumped.</summary>
    Jumped,

    /// <summary>The player began aiming a phase.</summary>
    PhaseBegan,

    /// <summary>The player phased: released it with a direction held.</summary>
    PhaseThrown,

    /// <summary>The player cancelled a phase: released it with no direction held.</summary>
    PhaseCancelled,
}
