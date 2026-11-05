namespace BodyCheck.Core;

/// <summary>
/// What a route asks of its target, as LDtk's enum of the same name. A kind of entity that does one thing responds to
/// <see cref="Default"/> and nothing else; any other responds only to the actions that name what it does.
/// </summary>
public enum TriggerAction
{
    ToggleOnOff,
    On,
    Off,
    ToggleOpenClose,
    Open,
    Close,
    Default,
}
