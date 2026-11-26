using System.Text.Json.Serialization;

namespace BodyCheck.State;

/// <summary>
/// What is saved of an entity's state: a record for each kind with state of its own, in its state's file, which the
/// state holds as all it has besides its id and its name, so that what is saved and what the game sees can't drift
/// apart. Each field's default is how the kind's entities start, if their data doesn't say, so that a save written
/// before a field was added reads. Each kind's record is listed here, with the name its save gives its kind, so that a
/// save can hold every kind in one map.
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "Kind")]
[JsonDerivedType(typeof(PlayerData), "Player")]
[JsonDerivedType(typeof(DoorData), "Door")]
[JsonDerivedType(typeof(SwitchData), "Switch")]
public abstract record EntityData;
