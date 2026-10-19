#version 460

// One binding per surface and a switch over the five, rather than one array indexed by the vertex's
// own number: an array of samplers indexed at run time is a Vulkan extension no browser has, and the
// five surfaces are different sizes, wrap-seamless and mipped, so an array texture would force them
// all to one size and resample the ground the town stands on. Five bindings cost a branch that is
// uniform over every triangle and divergent only where two surfaces meet inside a wave.
layout(set = 0, binding = 4) uniform sampler2D grass;
layout(set = 0, binding = 5) uniform sampler2D tarmac;
layout(set = 0, binding = 6) uniform sampler2D pavement;
layout(set = 0, binding = 7) uniform sampler2D deck;
layout(set = 0, binding = 8) uniform sampler2D water;

// The track's strip, last of the set's bindings so no binding before it moved when it came (TownRenderer.TrackBinding).
layout(set = 0, binding = 15) uniform sampler2D track;

layout(location = 0) in vec2 inUv;
layout(location = 1) in vec3 inTint;
layout(location = 2) flat in uint inSurface;

layout(location = 0) out vec4 outColour;

// Not a surface: the tint alone, for a mark that is not ground.
const uint PAINT = 255u;

// The track's strip, laid along its line: what its picture leaves clear past the ballast is not drawn, and the ground
// laid before it shows — the one cut-out in a pass with no blending.
const uint TRACK = 5u;

vec3 surface(uint of, vec2 uv) {
    switch (of) {
        case 1u: return texture(tarmac, uv).rgb;
        case 2u: return texture(pavement, uv).rgb;
        case 3u: return texture(deck, uv).rgb;
        case 4u: return texture(water, uv).rgb;
        default: return texture(grass, uv).rgb;
    }
}

void main() {
    if (inSurface == TRACK) {
        vec4 strip = texture(track, inUv);
        if (strip.a < 0.5) discard;
        outColour = vec4(strip.rgb, 1.0);
        return;
    }

    // An edge is the surface drawn darker and paint is the surface drawn brighter, so both are the
    // ground's own texture through a tint and the grain comes through both.
    outColour = inSurface == PAINT
        ? vec4(inTint, 1.0)
        : vec4(surface(inSurface, inUv) * inTint, 1.0);
}
