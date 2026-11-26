using Godot;

namespace BodyCheck.State;

/// <summary>
/// What every character that can speak has, with a body or without: its kind's name and its name, for the story and
/// the dialogue, which see it as it is. Every speaker is a character, so that every name is translated one way.
/// </summary>
public partial class CharacterState(string kindName) : RefCounted
{
    /// <summary>
    /// Its kind's name, untranslated, such as <c>Door</c>, which its avatar is named for, as <c>Door.png</c>.
    /// </summary>
    public string KindName => kindName;

    /// <summary>
    /// Its name in the current locale, translated when it is read, as a line in a dialogue file names its speaker,
    /// as <c>{{Voice.Name}}:</c>: its kind's name.
    /// </summary>
    public virtual string Name => Translatable.Translate(KindName, GameState.KindContext);

    /// <summary>Its name before translation.</summary>
    public virtual string UntranslatedName => KindName;
}
