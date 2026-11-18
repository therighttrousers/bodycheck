using Godot;

namespace BodyCheck.State;

/// <summary>
/// Text the game writes in C# that players read, such as names, which is translated as dialogue files are. Godot's
/// POT generation doesn't read C#, so a string is written where it is used, as a literal, in a call of
/// <see cref="Mark"/> or <see cref="Translate"/>, which a translation parser plugin for C# files is to find. A comment
/// starting <c>TRANSLATORS:</c> just before it says what translators need to know, such as what a placeholder means.
/// </summary>
public static class Translatable
{
    /// <summary>
    /// Marks <paramref name="text"/> for translation where it is written, and returns it untranslated, as gettext's
    /// <c>N_()</c> does: for text kept untranslated, such as a kind's name, which avatars are named for, and translated
    /// with <see cref="Translate"/> with the same <paramref name="context"/> where it is shown.
    /// </summary>
    /// <param name="context">What the text is, so that the same English word in two roles can be translated apart.</param>
    public static string Mark(string text, string context) => text;

    /// <summary>
    /// <paramref name="text"/> in the current locale, or as it is if there is no translation. Text with parts filled
    /// in, such as a name, uses named placeholders, such as <c>{kind} {serial}</c>, filled in after translating, so
    /// that a translator can reorder them.
    /// </summary>
    /// <param name="context">What the text is, as for <see cref="Mark"/>.</param>
    public static string Translate(string text, string context) => TranslationServer.Translate(text, context).ToString();
}
