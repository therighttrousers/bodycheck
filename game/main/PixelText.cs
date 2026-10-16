using System.Collections.Generic;

using Godot;

namespace BodyCheck;

/// <summary>
/// Text in a 3x5 pixel font, drawn as rectangles on whole pixels, for UI that must stay readable at the
/// game's low resolution. It has capitals, digits, space and <c>+-.?</c>; lowercase is drawn as capitals,
/// and anything else as <c>?</c>.
/// </summary>
public partial class PixelText : Control
{
    private const int GlyphWidth = 3;
    private const int GlyphHeight = 5;
    private const int Advance = GlyphWidth + 1;
    private const int LineHeight = GlyphHeight + 1;

    // Each glyph is five rows of three pixels, top row first.
    private static readonly Dictionary<char, string> Glyphs = new()
    {
        [' '] = "000000000000000",
        ['+'] = "000010111010000",
        ['-'] = "000000111000000",
        ['.'] = "000000000000010",
        ['?'] = "111001010000010",
        ['0'] = "111101101101111",
        ['1'] = "010110010010111",
        ['2'] = "111001111100111",
        ['3'] = "111001011001111",
        ['4'] = "101101111001001",
        ['5'] = "111100111001111",
        ['6'] = "111100111101111",
        ['7'] = "111001001010010",
        ['8'] = "111101111101111",
        ['9'] = "111101111001111",
        ['A'] = "010101111101101",
        ['B'] = "110101110101110",
        ['C'] = "011100100100011",
        ['D'] = "110101101101110",
        ['E'] = "111100110100111",
        ['F'] = "111100110100100",
        ['G'] = "011100101101011",
        ['H'] = "101101111101101",
        ['I'] = "111010010010111",
        ['J'] = "001001001101010",
        ['K'] = "101101110101101",
        ['L'] = "100100100100111",
        ['M'] = "101111111101101",
        ['N'] = "110101101101101",
        ['O'] = "010101101101010",
        ['P'] = "110101110100100",
        ['Q'] = "010101101110011",
        ['R'] = "110101110101101",
        ['S'] = "011100010001110",
        ['T'] = "111010010010010",
        ['U'] = "101101101101111",
        ['V'] = "101101101101010",
        ['W'] = "101101111111101",
        ['X'] = "101101010101101",
        ['Y'] = "101101010010010",
        ['Z'] = "111001010100111",
    };

    private string[] _lines = [];

    public Color Color { get; set; } = Colors.White;

    public string Text
    {
        get => string.Join('\n', _lines);
        set
        {
            _lines = value.Split('\n');
            UpdateMinimumSize();
            QueueRedraw();
        }
    }

    public override Vector2 _GetMinimumSize()
    {
        int columns = 0;
        foreach (string line in _lines)
        {
            columns = Mathf.Max(columns, line.Length);
        }

        // No spacing after the last column or below the last line.
        return new Vector2(Mathf.Max(columns * Advance - 1, 0), Mathf.Max(_lines.Length * LineHeight - 1, 0));
    }

    public override void _Draw()
    {
        for (int row = 0; row < _lines.Length; row++)
        {
            string line = _lines[row];
            for (int column = 0; column < line.Length; column++)
            {
                if (!Glyphs.TryGetValue(char.ToUpperInvariant(line[column]), out string? glyph))
                {
                    glyph = Glyphs['?'];
                }

                var origin = new Vector2(column * Advance, row * LineHeight);
                for (int i = 0; i < glyph.Length; i++)
                {
                    if (glyph[i] == '1')
                    {
                        DrawRect(new Rect2(origin + new Vector2(i % GlyphWidth, i / GlyphWidth), Vector2.One), Color);
                    }
                }
            }
        }
    }
}
