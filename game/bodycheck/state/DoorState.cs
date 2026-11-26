using BodyCheck.Core.Entities;

using Godot;

namespace BodyCheck.State;

/// <summary>What is saved of a door's state: its last action.</summary>
public sealed record DoorData : EntityData
{
    public OpenableAction LastAction { get; set; } = OpenableAction.None;
}

/// <summary>
/// A door's state, which only its controller sets, through Core's view; the game reads it only, as a string, since
/// GDScript and dialogue files can't use C# enums.
/// </summary>
public sealed partial class DoorState(string id, string kindName, DoorData data)
    : EntityState(id, kindName), IGameState.IDoorState
{
    internal override EntityData Saved => data;

    /// <inheritdoc cref="IGameState.IDoorState.LastAction"/>
    public string LastAction => data.LastAction.ToString();

    OpenableAction IGameState.IDoorState.LastAction
    {
        get => data.LastAction;
        set => data.LastAction = value;
    }
}
