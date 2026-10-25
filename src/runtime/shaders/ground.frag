#version 460
#extension GL_GOOGLE_include_directive : require

#include "app/render/kernels/ground_patches.glsl"

// One binding per surface and a switch over the five, rather than one array indexed by the vertex's
// own number: an array of samplers indexed at run time is a Vulkan extension no browser has, and the
// five surfaces are different sizes, wrap-seamless and mipped, so one array texture for all of them would
// force them to one size and resample the ground the town stands on. Five bindings cost a branch that is
// uniform over every triangle and divergent only where two surfaces meet inside a wave. Each binding is
// that one surface's looks, a layer a look (GroundPatches), which are one size by construction.
layout(set = 0, binding = 4) uniform sampler2DArray grass;
layout(set = 0, binding = 5) uniform sampler2DArray tarmac;
layout(set = 0, binding = 6) uniform sampler2DArray pavement;
layout(set = 0, binding = 7) uniform sampler2DArray deck;
layout(set = 0, binding = 8) uniform sampler2DArray water;

// The track's strip, then the shore, each appended to the set's bindings so no binding before it moved when it came
// (TownRenderer.TrackBinding, ShoreBinding).
layout(set = 0, binding = 15) uniform sampler2D track;
layout(set = 0, binding = 17) uniform sampler2DArray shore;
// And the two kerbs' strips after them (TownRenderer.KerbBinding, EdgingBinding).
layout(set = 0, binding = 18) uniform sampler2D kerb;
layout(set = 0, binding = 19) uniform sampler2D edging;

layout(location = 0) in vec2 inUv;
layout(location = 1) in vec3 inTint;
layout(location = 2) flat in uint inSurface;
layout(location = 3) in vec2 inPatchAt;
layout(location = 4) flat in vec4 inPatchLook;
layout(location = 5) flat in vec2 inPatchField;
layout(location = 6) in float inCover;
layout(location = 7) in vec2 inPositionM;
layout(location = 8) flat in vec4 inScatter;
layout(location = 9) flat in vec4 inDrift;

layout(location = 0) out vec4 outColour;

// Not a surface: the tint alone, for a mark that is not ground.
const uint PAINT = 255u;

// The track's strip, laid along its line: what its picture leaves clear past the ballast is not drawn, and the ground
// laid before it shows — a cut-out, which a blend at half cover would leave as a halo.
const uint TRACK = 5u;

// The kerbs' strips (KerbLooks), drawn whole: a kerb covers its width edge to edge. Then the shadow a kerb casts, which
// is no picture: the ground before it darkened by the corner's cover.
const uint KERB = 7u;
const uint EDGING = 8u;
const uint SHADOW = 9u;

// Where each surface's hashes start past its own number, which is its patches' (GroundPatches): the lumps its fades are
// ragged by, then its two looks' scatter.
const uint RAGGED_SALT = 8u;
const uint SCATTER_SALT = 16u;

// A hex whose share of a place is below this reads nothing there: past the sharpening, a look a quarter of a hex away.
const float LEAST_SHARE = 1.0 / 256.0;

// A fill's cover is one at every corner, and what the interpolation leaves of it is not a fade.
const float WHOLE_COVER = 0.999;

// The gradients are the place's own and handed in: a hex reads its look from a place of its own, and the jump between
// two of them would be a seam a mip level wide if the sample took its own.
vec3 surface(uint of, vec3 at, vec2 ddx, vec2 ddy) {
    switch (of) {
        case 1u: return textureGrad(tarmac, at, ddx, ddy).rgb;
        case 2u: return textureGrad(pavement, at, ddx, ddy).rgb;
        case 3u: return textureGrad(deck, at, ddx, ddy).rgb;
        case 4u: return textureGrad(water, at, ddx, ddy).rgb;
        case 6u: return textureGrad(shore, at, ddx, ddy).rgb;
        default: return textureGrad(grass, at, ddx, ddy).rgb;
    }
}

// One look at `uv`, as the hexes there read it (scatterCorners).
vec3 scattered(uint of, vec2 uv, float look, uint salt, vec2 ddx, vec2 ddy) {
    ivec2 corners[3];
    vec3 shares;
    scatterCorners(uv * inScatter.x, inScatter.y, corners, shares);
    vec3 sum = vec3(0.0);
    float read = 0.0;
    for (int c = 0; c < 3; c++) {
        if (shares[c] < LEAST_SHARE) continue;
        sum += shares[c] * surface(of, vec3(uv + scatterFrom(corners[c], salt), look), ddx, ddy);
        read += shares[c];
    }

    return sum / read;
}

// A fade's cover, moved by lumps of its own at its middle and not at its ends, so turf meets what it fades over in tufts.
float ragged(float cover) {
    float lumps = patchLumps(inPositionM * inScatter.z, inPatchField, RAGGED_SALT + inSurface);
    return clamp(cover + inScatter.w * lumps * 4.0 * cover * (1.0 - cover), 0.0, 1.0);
}

// A kerb's strip, held half a texel inside its long edges: the picture repeats along the kerb and not across it, and a
// filtered sample at one edge would read the other.
vec3 kerbstone(vec2 ddx, vec2 ddy) {
    vec2 size = vec2(inSurface == KERB ? textureSize(kerb, 0) : textureSize(edging, 0));
    vec2 at = vec2(inUv.x, clamp(inUv.y, 0.5 / size.y, 1.0 - (0.5 / size.y)));
    return inSurface == KERB ? textureGrad(kerb, at, ddx, ddy).rgb : textureGrad(edging, at, ddx, ddy).rgb;
}

void main() {
    vec2 ddx = dFdx(inUv);
    vec2 ddy = dFdy(inUv);

    if (inSurface == KERB || inSurface == EDGING) {
        outColour = vec4(kerbstone(ddx, ddy) * patchTint(patchLumps(inPatchAt, inPatchField, inSurface), inPatchLook), 1.0);
        return;
    }

    if (inSurface == SHADOW) {
        outColour = vec4(0.0, 0.0, 0.0, inCover);
        return;
    }

    if (inSurface == TRACK) {
        vec4 strip = texture(track, inUv);
        if (strip.a < 0.5) discard;
        outColour = vec4(strip.rgb, 1.0);
        return;
    }

    if (inSurface == PAINT) {
        outColour = vec4(inTint, 1.0);
        return;
    }

    // An edge is the surface drawn darker, so it is the ground's own texture through a tint and the grain and the
    // patches come through it. A look the patches give no share of a place is not read there.
    float lumps = patchLumps(inPatchAt, inPatchField, inSurface);
    float second = patchLook(lumps, inPatchLook);
    vec3 ground = vec3(0.0);
    if (second < 1.0) ground += (1.0 - second) * scattered(inSurface, inUv - inDrift.xy, 0.0, SCATTER_SALT + 2u * inSurface, ddx, ddy);
    if (second > 0.0) ground += second * scattered(inSurface, inUv - inDrift.zw, 1.0, SCATTER_SALT + 2u * inSurface + 1u, ddx, ddy);
    // Blended, and opaque everywhere but where a surface fades out over the ground before it (GroundVertex.Fading).
    float cover = inCover < WHOLE_COVER ? ragged(inCover) : 1.0;
    outColour = vec4(ground * patchTint(lumps, inPatchLook) * inTint, cover);
}
