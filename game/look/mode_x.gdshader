shader_type canvas_item;

// The colour pass: everything drawn so far, read back and written out again in the palette. Each pixel becomes
// the palette colour nearest to it in Oklab, after a dither offset (decision record 004). ColorPass.cs sets the
// uniforms, and Palette.cs makes the two palette textures.
// FRAGCOORD.xy is in pixels of the low-resolution image, and the colours read are already tone mapped sRGB.

uniform sampler2D screen : hint_screen_texture, filter_nearest;
uniform bool dither = true;

// The palette's colours in sRGB, and the same colours in the same places in Oklab.
uniform sampler2D palette : filter_nearest;
uniform sampler2D palette_lab : filter_nearest;

// How far the dither offsets reach from end to end, in Oklab: along lightness, and along each of a and b. On a
// lattice this would be one step of the lattice. The palette is not one, so these are about the distance
// between neighbouring colours: too small leaves bands, and too large mixes colours that are far apart.
uniform float lightness_spread;
uniform float chroma_spread;

// Returns one of [-0.375, -0.125, 0.125, 0.375], by the 2x2 Bayer pattern:
// [ 0 2 ]
// [ 3 1 ]
float bayer_offset(ivec2 pixel) {
    int x = pixel.x % 2;
    int y = pixel.y % 2;
    int index = y == 0 ? x * 2 : (1 - x) * 2 + 1;
    return (float(index) + 0.5) / 4.0 - 0.5;
}

// Undoes the sRGB transfer function, giving values proportional to light.
vec3 srgb_to_linear(vec3 c) {
    return mix(c / 12.92, pow((c + 0.055) / 1.055, vec3(2.4)), step(0.04045, c));
}

// Oklab from linear sRGB, with Ottosson's toe on lightness (the L_r of Okhsl), which is close to CIELAB's L*.
// Plain Oklab's cube root puts the darkest colours much too far from black. Palette.cs has the same function,
// and the two have to agree.
vec3 linear_to_oklab(vec3 c) {
    float l = pow(dot(c, vec3(0.4122214708, 0.5363325363, 0.0514459929)), 1.0 / 3.0);
    float m = pow(dot(c, vec3(0.2119034982, 0.6806995451, 0.1073969566)), 1.0 / 3.0);
    float s = pow(dot(c, vec3(0.0883024619, 0.2817188376, 0.6299787005)), 1.0 / 3.0);

    float lightness = 0.2104542553 * l + 0.7936177850 * m - 0.0040720468 * s;
    float a = 1.9779984951 * l - 2.4285922050 * m + 0.4505937099 * s;
    float b = 0.0259040371 * l + 0.7827717662 * m - 0.8086757660 * s;

    const float K1 = 0.206;
    const float K2 = 0.03;
    const float K3 = (1.0 + K1) / (1.0 + K2);
    float x = K3 * lightness - K1;
    return vec3(0.5 * (x + sqrt(x * x + 4.0 * K2 * K3 * lightness)), a, b);
}

void fragment() {
    vec4 source = clamp(texture(screen, SCREEN_UV), 0.0, 1.0);
    vec3 target = linear_to_oklab(srgb_to_linear(source.rgb));

    if (dither) {
        // The pattern is shifted by a pixel for a and by a row for b, so that the three offsets differ in
        // every pixel of a 2x2 block. Each sums to nothing over the block.
        ivec2 pixel = ivec2(FRAGCOORD.xy);
        target += vec3(
            lightness_spread * bayer_offset(pixel),
            chroma_spread * bayer_offset(pixel + ivec2(1, 0)),
            chroma_spread * bayer_offset(pixel + ivec2(0, 1)));
    }

    // Every pixel is compared with every colour, which is affordable at the low resolution and not much above it.
    ivec2 size = textureSize(palette_lab, 0);
    ivec2 nearest = ivec2(0);
    float nearest_distance = 1.0e10;
    for (int y = 0; y < size.y; y++) {
        for (int x = 0; x < size.x; x++) {
            vec3 difference = texelFetch(palette_lab, ivec2(x, y), 0).rgb - target;
            float distance_squared = dot(difference, difference);
            if (distance_squared < nearest_distance) {
                nearest = ivec2(x, y);
                nearest_distance = distance_squared;
            }
        }
    }

    COLOR = vec4(texelFetch(palette, nearest, 0).rgb, source.a);
}
