using System;

using Godot;

namespace BodyCheck.Look;

/// <summary>
/// The game's 256 colours, as images for <c>mode_x.gdshader</c>. They are generated here for now;
/// when the palette is final it becomes an asset (decision record 004).
/// </summary>
public static class Palette
{
    private const int Side = 16;

    /// <summary>
    /// The palette as a 16x16 image in sRGB. It is defined in HSV. The top row is gray, from black to white.
    /// Each row below it holds two ramps of 8 values, darkest first: 6 hues at a saturation of 1/3, then 12
    /// hues at 2/3, then 12 hues at 1.
    /// </summary>
    public static Image CreateImage()
    {
        // Bytes, rounded here, because Image.SetPixel rounds down.
        byte[] data = new byte[Side * Side * 3];
        int index = 0;
        void Add(Color color)
        {
            data[index++] = (byte)color.R8;
            data[index++] = (byte)color.G8;
            data[index++] = (byte)color.B8;
        }

        for (int value = 0; value < 16; value++)
        {
            Add(Color.FromHsv(0, 0, value / 15f));
        }
        foreach ((int saturation, int hues) in new[] { (1, 6), (2, 12), (3, 12) })
        {
            for (int hue = 0; hue < hues; hue++)
            {
                for (int value = 1; value <= 8; value++)
                {
                    Add(Color.FromHsv((float)hue / hues, saturation / 3f, value / 8f));
                }
            }
        }
        return Image.CreateFromData(Side, Side, false, Image.Format.Rgb8, data);
    }

    /// <summary>The same colours in the same places, in Oklab: lightness, a and b in red, green and blue.</summary>
    public static Image ToOklab(Image palette)
    {
        var image = Image.CreateEmpty(palette.GetWidth(), palette.GetHeight(), false, Image.Format.Rgbaf);
        for (int y = 0; y < palette.GetHeight(); y++)
        {
            for (int x = 0; x < palette.GetWidth(); x++)
            {
                image.SetPixel(x, y, LinearToOklab(palette.GetPixel(x, y).SrgbToLinear()));
            }
        }
        return image;
    }

    // Oklab with Ottosson's toe on lightness. The same function as linear_to_oklab in mode_x.gdshader,
    // and the two have to agree.
    private static Color LinearToOklab(Color c)
    {
        float l = MathF.Cbrt(0.4122214708f * c.R + 0.5363325363f * c.G + 0.0514459929f * c.B);
        float m = MathF.Cbrt(0.2119034982f * c.R + 0.6806995451f * c.G + 0.1073969566f * c.B);
        float s = MathF.Cbrt(0.0883024619f * c.R + 0.2817188376f * c.G + 0.6299787005f * c.B);

        float lightness = 0.2104542553f * l + 0.7936177850f * m - 0.0040720468f * s;
        float a = 1.9779984951f * l - 2.4285922050f * m + 0.4505937099f * s;
        float b = 0.0259040371f * l + 0.7827717662f * m - 0.8086757660f * s;

        const float k1 = 0.206f;
        const float k2 = 0.03f;
        const float k3 = (1 + k1) / (1 + k2);
        float x = k3 * lightness - k1;
        return new Color(0.5f * (x + MathF.Sqrt(x * x + 4 * k2 * k3 * lightness)), a, b);
    }
}
