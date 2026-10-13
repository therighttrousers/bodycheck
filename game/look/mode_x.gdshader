shader_type canvas_item;

// The colour pass: everything drawn so far, read back and written out again. ColorPass.cs sets the uniforms.
// FRAGCOORD.xy is in pixels of the low-resolution image, and the colours read are already tone mapped sRGB.

uniform sampler2D screen : hint_screen_texture, filter_nearest;
uniform bool dither = true;

// Returns a value from the 2x2 Bayer pattern:
// [ 0 2 ]
// [ 3 1 ]
int bayer2(ivec2 pixel) {
    int x = pixel.x % 2;
    int y = pixel.y % 2;
    return y == 0 ? x * 2 : (1 - x) * 2 + 1;
}

vec3 rgb2hsv(vec3 c)
{
    vec4 K = vec4(0.0, -1.0 / 3.0, 2.0 / 3.0, -1.0);
    vec4 p = mix(vec4(c.bg, K.wz), vec4(c.gb, K.xy), step(c.b, c.g));
    vec4 q = mix(vec4(p.xyw, c.r), vec4(c.r, p.yzx), step(p.x, c.r));

    float d = q.x - min(q.w, q.y);
    float e = 1.0e-10;
    return vec3(abs(q.z + (q.w - q.y) / (6.0 * d + e)), d / (q.x + e), q.x);
}

vec3 hsv2rgb(vec3 c)
{
    vec4 K = vec4(1.0, 2.0 / 3.0, 1.0 / 3.0, 3.0);
    vec3 p = abs(fract(c.xxx + K.xyz) * 6.0 - K.www);
    return c.z * mix(K.xxx, clamp(p - K.xxx, 0.0, 1.0), c.y);
}

void fragment() {
    vec4 source = clamp(texture(screen, SCREEN_UV), 0.0, 1.0);
    // If `dither` is true, `threshold` is in [-0.375, -0.125, 0.125, 0.375]
    float threshold = dither ? ((float(bayer2(ivec2(FRAGCOORD.xy))) + 0.5) / 4.0 - 0.5) : 0.0;

    // Convert to HSV color space
    vec3 hsv = rgb2hsv(source.rgb);

    // 4 saturation levels
    int s = clamp(int(hsv.y * 4.0 + threshold), 0, 3);

    // Number of hue levels depends on saturation:
    // - s = 0: color is gray, hue doesn't matter -> 1 level
    // - s = 1: color is washed out, hue differences are hard to see -> 6 levels (R, Y, G, C, B, M)
    // - s = 2 or 3: color is vivid, hue differences are easy to see -> 12 levels (R, Y, G, C, B, M, and the colors between)
    float h_levels = vec4(1.0, 6.0, 12.0, 12.0)[s];

    // 16 value levels for gray, 9 value levels for everything else
    float v_levels = vec4(16.0, 9.0, 9.0, 9.0)[s];

    // Note that value = 0 is black regardless of hue and saturation, so when s > 0, value has 8 *distinct* levels.
    // Therefore, the total number of colors is 1*16 + 6*8 + 12*8 + 12*8 = 256.

    // Quantize
    float quantized_h = fract(floor(hsv.x * h_levels + 0.5 + threshold) / h_levels);
    float quantized_s = float(s) / 3.0;
    float quantized_v = clamp(floor(hsv.z * v_levels + threshold) / (v_levels - 1.0), 0.0, 1.0);

    // Convert back to RGB
    vec3 rgb = hsv2rgb(vec3(quantized_h, quantized_s, quantized_v));

    COLOR = vec4(rgb, source.a);
}
