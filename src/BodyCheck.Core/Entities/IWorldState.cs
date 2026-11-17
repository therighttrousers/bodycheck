namespace BodyCheck.Core.Entities;

public enum OpenableAction
{
    Open,
    Close,
}

public interface IWorldState
{
    IPlayerState Player { get; }

    IDoorState AddDoor(string id, string name, OpenableAction lastAction);
    IDoorState GetDoor(string nameOrId);

    public interface IPlayerState
    {
        bool CanPhase { get; }
        bool CanSpawn { get; }
    }

    public interface IDoorState
    {
        string Name { get; }
        OpenableAction LastAction { get; set; }
    }
}
