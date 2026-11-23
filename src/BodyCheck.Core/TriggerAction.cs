namespace BodyCheck.Core;

/// <summary>
/// What a route asks of its target, as LDtk's enum of the same name. Each names what it asks for, so that a route
/// says what it does, and a kind of entity responds only to the actions that name what it does.
/// </summary>
public enum TriggerAction
{
    ToggleOnOff,
    TurnOn,
    TurnOff,
    ToggleOpenClose,
    Open,
    Close,

    /// <summary>Runs a script, or the steps of a sequence, a fanout or a once.</summary>
    Run,

    /// <summary>Presents a dialogue.</summary>
    Present,
}
