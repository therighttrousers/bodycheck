using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

using BodyCheck.Core.Maths;

namespace BodyCheck;

/// <summary>
/// The garble of the Voice's broken lines: a fraction of a line's letters and digits replaced with <see cref="Tofu"/>,
/// chosen from a seed and a step, so that a line garbles the same way every time it is shown. A line is garbled only if
/// its <see cref="Tag"/> says so. A label shifts the garble by showing the line at each step in turn: the fraction
/// garbled stays the same, but which glyphs are garbled changes, so that the whole line can be pieced together.
/// </summary>
public static class Garble
{
    /// <summary>
    /// The garbled glyph's codepoint, in the Private Use Area. Only the tofu font, the dialogue font's fallback, may
    /// have a glyph for it, so that it looks the same whatever the locale's font. Fonts do draw glyphs of their own in
    /// the Private Use Area, Fusion Pixel from U+E000 to U+E24F, so a locale's font must be checked for it.
    /// <para>
    /// It is in the Basic Multilingual Plane, one UTF-16 code unit, because Dialogue Manager's C# label indexes its
    /// text in code units, where Godot counts codepoints: past a codepoint of two, its punctuation pauses would land in
    /// the wrong place.
    /// </para>
    /// </summary>
    public static readonly Rune Tofu = new(0xEB00);

    /// <summary>The tag that garbles a line in a dialogue file, as <c>[#garble=0.25]</c>, with the fraction garbled.</summary>
    public const string Tag = "garble";

    /// <summary>
    /// The fraction a line with these tags is garbled: its <see cref="Tag"/>'s value, or 0 if it has none, so that only
    /// the lines marked are garbled, and never the player's.
    /// </summary>
    /// <exception cref="FormatException">
    /// When the tag has no value, or one that isn't a number from 0 to 1, or the line has it more than once.
    /// </exception>
    public static float FractionOf(IEnumerable<string> tags)
    {
        string[] values = [.. tags.Where(t => t == Tag || t.StartsWith(Tag + "=", StringComparison.Ordinal))];
        if (values.Length == 0)
        {
            return 0;
        }
        if (values.Length > 1)
        {
            throw new FormatException($"A line has more than one '{Tag}' tag.");
        }
        string value = values[0].Length > Tag.Length ? values[0][(Tag.Length + 1)..] : "";
        if (!float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out float fraction)
            || fraction is < 0 or > 1 or float.NaN)
        {
            throw new FormatException($"A line's '{Tag}' tag is '{values[0]}', not '{Tag}=' and a number from 0 to 1.");
        }
        return fraction;
    }

    /// <summary>
    /// Replaces <paramref name="fraction"/> of the letters and digits in <paramref name="text"/>, rounded to the nearest
    /// whole glyph, with <see cref="Tofu"/>, chosen by <paramref name="seed"/> and <paramref name="step"/>. Spaces and
    /// punctuation stay, so lines wrap and pause as written, and so do BBCode tags. Each glyph is replaced by one
    /// codepoint, so a label's character indices, by which Dialogue Manager places inline mutations, waits and speed
    /// changes, are the same in the garbled text, at every step.
    /// </summary>
    public static string Apply(string text, float fraction, string seed, int step)
    {
        Rune[] runes = text.EnumerateRunes().ToArray();
        int[] eligible = Eligible(runes);
        int count = (int)MathF.Round(fraction * eligible.Length);
        if (count == 0)
        {
            return text;
        }
        // Each glyph gets a rank from the seed, the step and its place, and the lowest ranked are garbled. At each step, a
        // more garbled line garbles the same glyphs as a less garbled one, and more.
        ulong stepHash = Rand.SplitMix64(Rand.Fnv1a(seed) ^ (ulong)step);
        foreach (int i in eligible.OrderBy(i => Rand.SplitMix64(stepHash ^ (ulong)i)).Take(count))
        {
            runes[i] = Tofu;
        }
        var garbled = new StringBuilder(text.Length);
        foreach (Rune rune in runes)
        {
            garbled.Append(rune.ToString());
        }
        return garbled.ToString();
    }

    /// <summary>The indices of the letters and digits in <paramref name="runes"/>, outside BBCode tags.</summary>
    private static int[] Eligible(Rune[] runes)
    {
        var eligible = new List<int>();
        bool inTag = false;
        for (int i = 0; i < runes.Length; i++)
        {
            Rune rune = runes[i];
            if (inTag)
            {
                inTag = rune.Value != ']';
            }
            else if (rune.Value == '[')
            {
                inTag = true;
            }
            else if (Rune.IsLetterOrDigit(rune))
            {
                eligible.Add(i);
            }
        }
        return [.. eligible];
    }
}
