using BodyCheck.Core.Entities;

using Godot;

namespace BodyCheck.State;

/// <summary>What is saved of the player's state: its abilities.</summary>
public sealed record PlayerData : EntityData
{
    public bool CanPhase { get; set; } = true;
    public bool CanSpawn { get; set; } = true;
}

/// <summary>The player's state, whose abilities the game owns and Core reads.</summary>
public sealed partial class PlayerState(string id, string kindName, PlayerData data)
    : EntityState(id, kindName), IGameState.IPlayerState
{
    internal override EntityData Saved => data;

    public bool CanPhase { get => data.CanPhase; set => data.CanPhase = value; }
    public bool CanSpawn { get => data.CanSpawn; set => data.CanSpawn = value; }

    bool IGameState.IPlayerState.CanPhase => CanPhase;
    bool IGameState.IPlayerState.CanSpawn => CanSpawn;
}
