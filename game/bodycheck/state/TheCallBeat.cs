using Godot;

namespace BodyCheck.State;

/// <summary>What has happened in the call, the story's first beat.</summary>
public sealed partial class TheCallBeat : RefCounted
{
    /// <summary>Index of the next reason the inner door gives for not opening.</summary>
    public int InnerDoorWontCloseReason { get; set; }
}
