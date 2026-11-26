using Godot;

namespace BodyCheck.State;

/// <summary>
/// What has happened in the story, which the dialogue reads and sets, as <c>Story.TheCall.InnerDoorWontCloseReason</c>,
/// and a save holds as it is: a beat for each part of the story, each in a file of its own.
/// </summary>
public sealed partial class StoryState : RefCounted
{
    public TheCallBeat TheCall { get; set; } = new();
}
