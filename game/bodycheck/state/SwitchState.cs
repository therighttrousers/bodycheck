using BodyCheck.Core.Entities;

using Godot;

namespace BodyCheck.State;

/// <summary>What is saved of a trigger's or a once's state: whether it is on.</summary>
public sealed record SwitchData : EntityData
{
    /// <summary>Whether it is on. A trigger starts as its data says, and a once on, so it has no default.</summary>
    public required bool IsOn { get; set; }
}

/// <summary>
/// The state of a trigger or a once, which says whether it is on. What runs it is its only writer, through Core's
/// view; the game reads it only.
/// </summary>
public sealed partial class SwitchState(string id, string kindName, SwitchData data)
    : EntityState(id, kindName), IGameState.ISwitchState
{
    internal override EntityData Saved => data;

    /// <inheritdoc cref="IGameState.ISwitchState.IsOn"/>
    public bool IsOn => data.IsOn;

    bool IGameState.ISwitchState.IsOn { get => data.IsOn; set => data.IsOn = value; }
}
