using Godot;

namespace BodyCheck.State;

/// <summary>
/// What every entity has, with a body or without: its id, and its name, the one its data gives it, or else its kind's.
/// A kind with state of its own extends it, and keeps that state in its <see cref="EntityData"/>, which it gives as
/// <see cref="Saved"/>. Every entity is a character, so that any can be made to speak.
/// </summary>
public partial class EntityState(string id, string kindName) : CharacterState(kindName)
{
    /// <summary>Its configuration's id, which Core finds its state by.</summary>
    public string Id => id;

    /// <summary>
    /// The name its data gives it, untranslated, taken from its data again each time its world is filled in; or null,
    /// if it has none, and is known by its kind's name.
    /// </summary>
    internal string? GivenName { get; set; }

    /// <summary>Its name, translated, such as <c>Bridge trigger</c>, or, if its data gives it none, its kind's.</summary>
    public override string Name =>
        GivenName is null ? base.Name : Translatable.Translate(GivenName, GameState.NameContext);

    /// <inheritdoc/>
    public override string UntranslatedName => GivenName ?? base.UntranslatedName;

    /// <summary>What a save holds of it, or null if it has no state of its own.</summary>
    internal virtual EntityData? Saved => null;
}
