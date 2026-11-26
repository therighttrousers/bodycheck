using Godot;

namespace BodyCheck.State;

/// <summary>What is saved of the story: a record for each beat, in its beat's file.</summary>
public sealed record StoryData
{
    public TheCallData TheCall { get; set; } = new();
}

/// <summary>
/// What has happened in the story, which the dialogue reads and sets, as <c>Story.TheCall.InnerDoorWontCloseReason</c>:
/// a state for each beat, which keeps what is saved of it in its beat's record, as an entity's state does.
/// </summary>
public sealed partial class StoryState(StoryData data) : RefCounted
{
    internal StoryData Saved => data;

    public TheCallBeat TheCall { get; } = new(data.TheCall);
}
