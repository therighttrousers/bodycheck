using Godot;

namespace BodyCheck.State;

/// <summary>What is saved of the call, the story's first beat.</summary>
public sealed record TheCallData
{
    public int InnerDoorWontCloseReason { get; set; }
}

/// <summary>What has happened in the call, the story's first beat.</summary>
public sealed partial class TheCallBeat(TheCallData data) : RefCounted
{
    public int InnerDoorWontCloseReason
    {
        get => data.InnerDoorWontCloseReason;
        set => data.InnerDoorWontCloseReason = value;
    }
}
