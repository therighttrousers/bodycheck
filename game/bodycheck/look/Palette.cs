using System;
using System.Collections.Generic;

using Godot;

namespace BodyCheck.Look;

/// <summary>
/// The game's 256 colours, as images for <c>mode_x.gdshader</c>. They are generated here for now;
/// when the palette is final it becomes an asset.
/// </summary>
public static class Palette
{
    private const int Width = 4; // Change this to increase or decrease the number of colors
    private const int Height = 32;

    // The values of the coloured ramps: evenly spaced lightness, by CIE L*, turned into the luminance that has
    // it. That puts more of a ramp's colours in the dark, where evenly spaced values left too few.
    private static float ValueRamp(float v)
    {
        float l = v * 100.0f;
        return l <= 8.0f ? l / 903.3f : MathF.Pow((l + 16.0f) / 116.0f, 3.0f);
    }

    /// <summary>
    /// The palette as an image in sRGB, 8 wide and 32 high. It is defined in HSV. The top two rows are gray,
    /// from black to white. Each row below is one ramp of 8 values, darkest first, so that a row has one hue
    /// and one saturation: 6 hues at the lowest saturation, then 12 at the middle one, then 12 at full.
    /// </summary>
    public static Image CreateImage()
    {
        // Bytes, rounded here, because Image.SetPixel rounds down.
        byte[] data = new byte[Width * Height * 3];
        int index = 0;
        void Add(Color color)
        {
            data[index++] = (byte)color.R8;
            data[index++] = (byte)color.G8;
            data[index++] = (byte)color.B8;
        }

        for (int value = 0; value < Width * 2; value++)
        {
            Add(Color.FromHsv(0, 0, (float)value / (Width * 2 - 1)));
        }
        foreach ((int saturation, int hues) in new[] { (1, 6), (2, 12), (3, 12) })
        {
            for (int hue = 0; hue < hues; hue++)
            {
                int maxValue = 2 + Width - 1;
                for (int value = 2; value <= maxValue; value++)
                {
                    Add(Color.FromHsv((float)hue / hues, MathF.Pow(saturation / 3f, 1.5f), ValueRamp((float)value / maxValue)));
                }
            }
        }
        return Image.CreateFromData(Width, Height, false, Image.Format.Rgb8, data);
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

    /// <summary>
    /// A lightness spread for the dither that suits a palette, given its Oklab image: three quarters of the
    /// mean step of lightness between a colour and its neighbour up or down the lightness axis. The neighbour
    /// is the colour nearest in lightness among those that differ less than half as much in a and b together
    /// as in lightness. The three quarters matches what Neil chose by eye for two palettes (0.07 and 0.12).
    /// </summary>
    public static float LightnessSpread(Image lab)
    {
        var colors = new List<Color>();
        for (int y = 0; y < lab.GetHeight(); y++)
        {
            for (int x = 0; x < lab.GetWidth(); x++)
            {
                colors.Add(lab.GetPixel(x, y));
            }
        }

        float sum = 0;
        int count = 0;
        foreach (Color color in colors)
        {
            float step = float.MaxValue;
            foreach (Color other in colors)
            {
                float lightness = Mathf.Abs(other.R - color.R);
                float chroma = new Vector2(other.G - color.G, other.B - color.B).Length();
                if (lightness > 0 && chroma < 0.5f * lightness)
                {
                    step = Mathf.Min(step, lightness);
                }
            }
            if (step < float.MaxValue)
            {
                sum += step;
                count++;
            }
        }
        return count > 0 ? 0.75f * sum / count : 0;
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
