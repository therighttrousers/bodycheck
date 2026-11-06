using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace BodyCheck;

/// <summary>
/// The garble of the Voice's broken lines: a fraction of a line's letters and digits replaced with <see cref="Tofu"/>,
/// chosen from a seed, so that a line garbles the same way every time it is shown. A line is garbled only if its
/// <see cref="Tag"/> says so.
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
    /// whole glyph, with <see cref="Tofu"/>, chosen by <paramref name="seed"/>. Spaces and punctuation stay, so lines
    /// wrap and pause as written, and so do BBCode tags. Each glyph is replaced by one codepoint, so a label's character
    /// indices, by which Dialogue Manager places inline mutations, waits and speed changes, are the same in the garbled
    /// text.
    /// </summary>
    public static string Apply(string text, float fraction, string seed)
    {
        Rune[] runes = text.EnumerateRunes().ToArray();
        int[] eligible = Eligible(runes);
        int count = (int)MathF.Round(fraction * eligible.Length);
        if (count == 0)
        {
            return text;
        }
        // Each glyph gets a rank from the seed and its place, and the lowest ranked are garbled. A more garbled line
        // garbles the same glyphs as a less garbled one, and more.
        ulong seedHash = Fnv1a(seed);
        foreach (int i in eligible.OrderBy(i => Mix(seedHash ^ (ulong)i)).Take(count))
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

    // .NET's string hashes differ from run to run, and its seeded Random isn't promised to stay the same across
    // versions, so the garble uses hashes of its own: FNV-1a for the seed, and SplitMix64's finalizer for the ranks.

    private static ulong Fnv1a(string s)
    {
        ulong hash = 14695981039346656037;
        foreach (char c in s)
        {
            hash = (hash ^ c) * 1099511628211;
        }
        return hash;
    }

    private static ulong Mix(ulong x)
    {
        x = (x ^ (x >> 30)) * 0xBF58476D1CE4E5B9;
        x = (x ^ (x >> 27)) * 0x94D049BB133111EB;
        return x ^ (x >> 31);
    }
}
