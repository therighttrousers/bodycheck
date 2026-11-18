using System.Threading.Tasks;

using BodyCheck.Core;

using Godot;
using Godot.Collections;

namespace BodyCheck;

/// <summary>
/// What a dialogue file sees while it is presented, as Dialogue Manager's extra game state beside the world's
/// <see cref="GameState"/>: the states of the entities the dialogue is about, as its <c>Dialogue</c> gives them, and a
/// way to ask them for actions. Dialogue Manager finds a C# method only
/// on an object whose script is a C# file, so this is a script of its own, not a class nested in
/// <see cref="DialogueBox"/>.
/// </summary>
public sealed partial class DialogueState : RefCounted
{
    /// <summary>The box presenting the dialogue, which starts the actions asked for on the world's tick.</summary>
    internal DialogueBox Box { get; init; } = null!;

    /// <summary>The states of the entities the dialogue is about, in order, such as a door's, with its last action.</summary>
    public Array<GameState.EntityState> Entities { get; init; } = [];

    /// <summary>The first of <see cref="Entities"/>, the one that speaks, as in <c>{{Speaker.Name}}: Hello.</c></summary>
    public GameState.EntityState? Speaker => Entities.Count > 0 ? Entities[0] : null;

    /// <summary>
    /// Asks <paramref name="entity"/> for the action <paramref name="action"/>, a <see cref="TriggerAction"/> by name, as
    /// <c>do Act(Speaker, "Open")</c>, which waits until the action has finished; <c>do!</c> doesn't wait.
    /// </summary>
    public Task Act(GameState.EntityState entity, string action) => Box.Ask(entity, action);
}
