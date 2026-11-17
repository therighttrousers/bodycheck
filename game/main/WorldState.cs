using System.Collections.Generic;

using BodyCheck.Core.Entities;

namespace BodyCheck;

public class WorldState : IWorldState
{
    private readonly Dictionary<string, DoorState> _doors = [];
    private readonly Dictionary<string, string> _doorNameToId = [];

    /// <summary>The player's abilities.</summary>
    public PlayerState Player { get; } = new();

    IWorldState.IPlayerState IWorldState.Player => Player;

    public IWorldState.IDoorState AddDoor(string id, string name, OpenableAction lastAction)
    {
        _doors[id] = new DoorState(name) { LastAction = lastAction };
        _doorNameToId[name] = id;
        return _doors[id];
    }

    public IWorldState.IDoorState GetDoor(string nameOrId)
    {
        return _doorNameToId.TryGetValue(nameOrId, out string? id) ? _doors[id] : _doors[nameOrId];
    }

    public class PlayerState : IWorldState.IPlayerState
    {
        public bool CanPhase { get; set; } = true;
        public bool CanSpawn { get; set; } = true;
    }

    private class DoorState(string name) : IWorldState.IDoorState
    {
        public string Name => name;
        public OpenableAction LastAction { get; set; }
    }
}
