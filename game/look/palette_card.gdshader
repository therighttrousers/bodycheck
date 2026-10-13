shader_type canvas_item;

// A test card for the colour pass: smooth sweeps through HSV, which the colour pass turns into its palette.
// - The top band is gray, from black on the left to white on the right.
// - Below it is a band for each of three saturations, the palest first. Hue runs from left to right, and
//   value from black at the bottom of the band to full at the top.

const float GRAY_BAND = 0.1;
const float SATURATIONS = 4.0;

vec3 hsv2rgb(vec3 c)
{
    vec4 K = vec4(1.0, 2.0 / 3.0, 1.0 / 3.0, 3.0);
    vec3 p = abs(fract(c.xxx + K.xyz) * 6.0 - K.www);
    return c.z * mix(K.xxx, clamp(p - K.xxx, 0.0, 1.0), c.y);
}

void fragment() {
    if (UV.y < GRAY_BAND) {
        COLOR = vec4(vec3(UV.x), 1.0);
    } else {
        // Which band, counting the gray one as 0, and how far down it.
        float y = (UV.y - GRAY_BAND) / (1.0 - GRAY_BAND) * (SATURATIONS - 1.0);
        float band = floor(y) + 1.0;
        // The middle of the band's range of saturations, so that dither doesn't carry it into the next.
        float saturation = (band + 0.5) / SATURATIONS;
        COLOR = vec4(hsv2rgb(vec3(UV.x, saturation, 1.0 - fract(y))), 1.0);
    }
}
