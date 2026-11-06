using System.Linq;

using GdUnit4;

using static GdUnit4.Assertions;

namespace BodyCheck.Tests;

[TestSuite]
public class GarbleTest
{
    private const string Line = "[color=red]Bring[/color] us 5 batteries, now.";
    private static readonly string TofuText = Garble.Tofu.ToString();

    [TestCase]
    public void Replaces_the_fraction_of_letters_and_digits_one_codepoint_for_one()
    {
        string garbled = Garble.Apply(Line, 0.5f, "seed");

        // 20 letters and digits, outside the tags.
        AssertThat(garbled.EnumerateRunes().Count(r => r == Garble.Tofu)).IsEqual(10);
        AssertThat(garbled.EnumerateRunes().Count()).IsEqual(Line.EnumerateRunes().Count());
        // Tags, spaces and punctuation are as written.
        string kept = garbled.Replace(TofuText, "_");
        AssertThat(kept).StartsWith("[color=red]");
        AssertThat(kept).Contains("[/color] ");
        AssertThat(kept).EndsWith(".");
        AssertThat(kept.Count(c => c == ' ')).IsEqual(Line.Count(c => c == ' '));
        AssertThat(kept).Contains(",");
    }

    [TestCase]
    public void Garbles_only_lines_with_a_garble_tag_and_rejects_one_that_is_not_a_fraction()
    {
        AssertThat(Garble.FractionOf([])).IsEqual(0);
        AssertThat(Garble.FractionOf(["voice=res://a.ogg"])).IsEqual(0);
        AssertThat(Garble.FractionOf(["voice=res://a.ogg", "garble=0.25"])).IsEqual(0.25f);
        string[][] bad = [["garble"], ["garble="], ["garble=lots"], ["garble=1.5"], ["garble=0.1", "garble=0.2"]];
        foreach (string[] tags in bad)
        {
            AssertThrown(() => Garble.FractionOf(tags)).IsInstanceOf<System.FormatException>();
        }
    }

    [TestCase]
    public void Garbles_the_same_way_every_time_and_more_garble_garbles_the_same_glyphs_and_more()
    {
        string less = Garble.Apply(Line, 0.25f, "seed");
        string more = Garble.Apply(Line, 0.5f, "seed");

        AssertThat(Garble.Apply(Line, 0.25f, "seed")).IsEqual(less);
        AssertThat(Garble.Apply(Line, 0.25f, "other seed")).IsNotEqual(less);
        AssertThat(Garble.Apply(Line, 0, "seed")).IsEqual(Line);
        string[] lessRunes = [.. less.EnumerateRunes().Select(r => r.ToString())];
        string[] moreRunes = [.. more.EnumerateRunes().Select(r => r.ToString())];
        for (int i = 0; i < lessRunes.Length; i++)
        {
            if (lessRunes[i] == TofuText)
            {
                AssertThat(moreRunes[i]).IsEqual(TofuText);
            }
        }
    }
}
