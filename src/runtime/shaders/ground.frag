#version 460
#extension GL_GOOGLE_include_directive : require

#include "app/render/kernels/ground_camera.glsl"
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
// And a bridge's art, a layer a strip and one of its fittings (TownRenderer.BridgeBinding, BridgeSheet).
layout(set = 0, binding = 20) uniform sampler2DArray bridge;

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
layout(location = 10) in vec3 inWaveRad;

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

// A bridge's strips and fittings: each the layer its number past the first names, cut out where its picture is clear.
const uint BRIDGE_FIRST = 10u;
const uint BRIDGE_LAST = 15u;

// Where each surface's hashes start past its own number, which is its patches' (GroundPatches): the lumps its fades are
// ragged by, then its two looks' scatter.
const uint RAGGED_SALT = 8u;
const uint SCATTER_SALT = 16u;

// A hex whose share of a place is below this reads nothing there: past the sharpening, a look a quarter of a hex away.
const float LEAST_SHARE = 1.0 / 256.0;

// A fill's cover is one at every corner, and what the interpolation leaves of it is not a fade.
const float WHOLE_COVER = 0.999;

const uint WATER = 4u;
const float WAVE_TRAINS = 3.0;
const float TAU = 6.28318531;

// Where the hashes of the lumps each train's crests are bent by start: past the scatter's, the shore's second look last.
const uint WAVE_BEND_SALT = 32u;

// The waves' height at this pixel, about minus one under every trough and one under every crest (WaterWaves). Each
// train's crests are bent off their lines by lumps of its own, and all of them run higher where the water's patches
// stand at `lumps` above their mean, so neither the crossing nor the swell repeats. A train narrower on screen than twice
// the least pixels it is drawn across fades out, and is gone at them; `widthRad` is how far each phase moves across the
// pixel.
float waveHeight(vec3 widthRad, float lumps) {
    vec4 look = camera.waveLook;
    vec3 shown = clamp(TAU / (widthRad * look.y) - 1.0, 0.0, 1.0);
    vec2 bendAt = inPositionM * camera.waveBendRad.w;
    vec3 bend = 2.0 * vec3(
        patchValue(bendAt, WAVE_BEND_SALT), patchValue(bendAt, WAVE_BEND_SALT + 1u), patchValue(bendAt, WAVE_BEND_SALT + 2u)) - 1.0;
    float swell = max(0.0, 1.0 + look.z * lumps);
    return swell * dot(sin(inWaveRad + bend * camera.waveBendRad.xyz), shown) / WAVE_TRAINS;
}

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
    vec3 waveWidthRad = fwidth(inWaveRad);

    if (inSurface == KERB || inSurface == EDGING) {
        outColour = vec4(kerbstone(ddx, ddy) * patchTint(patchLumps(inPatchAt, inPatchField, inSurface), inPatchLook), 1.0);
        return;
    }

    if (inSurface >= BRIDGE_FIRST && inSurface <= BRIDGE_LAST) {
        // Held half a texel inside its long edges, as a kerb's is: a strip repeats along its line and not across it.
        float halfTexel = 0.5 / float(textureSize(bridge, 0).y);
        vec4 piece = textureGrad(
            bridge, vec3(inUv.x, clamp(inUv.y, halfTexel, 1.0 - halfTexel), float(inSurface - BRIDGE_FIRST)), ddx, ddy);
        if (piece.a < 0.5) discard;
        outColour = vec4(piece.rgb, 1.0);
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
    float wave = 0.0;
    vec2 uv = inUv;
    if (inSurface == WATER) {
        wave = waveHeight(waveWidthRad, lumps);
        uv += wave * vec2(camera.waveNumberXPerM.w, camera.waveNumberYPerM.w) / camera.waterPeriodM.x;
    }
    vec3 ground = vec3(0.0);
    if (second < 1.0) ground += (1.0 - second) * scattered(inSurface, uv - inDrift.xy, 0.0, SCATTER_SALT + 2u * inSurface, ddx, ddy);
    if (second > 0.0) ground += second * scattered(inSurface, uv - inDrift.zw, 1.0, SCATTER_SALT + 2u * inSurface + 1u, ddx, ddy);
    // Blended, and opaque everywhere but where a surface fades out over the ground before it (GroundVertex.Fading).
    vec3 colour = ground * patchTint(lumps, inPatchLook) * inTint * (1.0 + wave * camera.waveLook.x);
    float cover = inCover < WHOLE_COVER ? ragged(inCover) : 1.0;
    // The water's fade onto the shore laps: up it under a crest and back under a trough, its ends where they are laid;
    // and foam rides its middle wherever the swash has moved it, whitest as a crest runs up and half as white between.
    if (inSurface == WATER && inCover < WHOLE_COVER) {
        cover = clamp(cover + camera.waveShore.x * wave * 4.0 * cover * (1.0 - cover), 0.0, 1.0);
        float foam = camera.waveShore.y * 4.0 * cover * (1.0 - cover) * clamp(0.5 + wave, 0.0, 1.0);
        colour = mix(colour, vec3(1.0), foam);
    }
    outColour = vec4(colour, cover);
}
