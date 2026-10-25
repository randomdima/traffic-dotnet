#version 460
#extension GL_GOOGLE_include_directive : require

// The town's standing ground. The texture coordinate is the world position over the surface's period
// and nothing else, which is what anchors every surface to the world origin rather than to the shape
// being painted (New-Engine-Requirements/terrain.md, "Textures"); a corner carries its place and one
// word of shade and surface (GroundVertex), and the periods are the camera's.

#include "app/render/kernels/ground_camera.glsl"

// The town's own metres to clip. **The turn is applied here and nowhere else**: a sprite's heading and
// a band's direction are built in the town's own axes, so turning the whole offset turns them with it.
vec4 toClip(vec2 atM) {
    vec2 fromCentreM = atM - camera.centreM;
    vec2 turnedM = vec2(
        fromCentreM.x * camera.facing.x - fromCentreM.y * camera.facing.y,
        fromCentreM.x * camera.facing.y + fromCentreM.y * camera.facing.x);
    return vec4(turnedM * camera.clipPerM, 0.0, 1.0);
}

layout(location = 0) in vec2 inPositionM;
layout(location = 1) in uint inShade;

layout(location = 0) out vec2 outUv;
layout(location = 1) out vec3 outTint;
layout(location = 2) flat out uint outSurface;
// Where the corner stands in the patches' field, in patches across, how its surface takes them (patchLook, patchTint),
// and the field's mean and one over its spread.
layout(location = 3) out vec2 outPatchAt;
layout(location = 4) flat out vec4 outPatchLook;
layout(location = 5) flat out vec2 outPatchField;
// How much of the ground laid before it the corner covers: one but where a surface fades out over it.
layout(location = 6) out float outCover;
// The corner's place, which a fade's edge is ragged by; the hexes its looks are scattered in, as hexes a period, and
// their sharpness, then the ragging's lumps a metre and how far they move the cover; and how far each look has drifted,
// in periods.
layout(location = 7) out vec2 outPositionM;
layout(location = 8) flat out vec4 outScatter;
layout(location = 9) flat out vec4 outDrift;
// Each wave train's phase at the corner, linear in its place so the interpolation is exact. Nought off the water.
layout(location = 10) out vec3 outWaveRad;

// How a shade is filed, said again from GroundVertex: three channels of nine bits up to the brightest
// tint, and the surface in the five above them, paint filed as the last of those.
const float BRIGHTEST_TINT = 3.0;
const uint CHANNEL_STEPS = 511u;
const uint PAINT_FILED = 31u;
const uint PAINT = 255u;

// The track's strip files where on it a corner stands in place of a tint, said again from GroundVertex.OnTheTrack:
// pictures along in eighteen bits of 256 steps each, and across in the nine above them.
const uint TRACK = 5u;

// The shore, filed after the track's strip, and the row of the patches its figures stand in (GroundPatches.ShoreRow).
const uint SHORE = 6u;
const uint SHORE_ROW = 5u;
// The kerbs' strips, filed as the track's is, and the walk whose patches they wear.
const uint KERB = 7u;
const uint EDGING = 8u;
const uint PAVEMENT = 2u;
// A bridge's strips and fittings, filed as the track's is, each reading the layer of the bridge's sheet its number past
// the first names (Surface.BridgeWalkway onward, BridgeSheet).
const uint BRIDGE_FIRST = 10u;
const uint BRIDGE_LAST = 15u;
// A surface fading out over the ground before it files its number with this bit, and how much it covers in place of
// its tint (GroundVertex.Fading).
const uint FADING_FILED = 16u;
const float TRACK_STEPS_PER_PICTURE = 256.0;
const uint ALONG_STEPS = 262143u;
const float ACROSS_STEPS = 511.0;

void main() {
    // +y is down in the world and +y is down in Vulkan's clip space, so nothing is flipped anywhere.
    gl_Position = toClip(inPositionM);

    uint filed = inShade >> 27;
    outPatchAt = vec2(0.0);
    outPatchField = camera.patchField.xy;
    outCover = 1.0;
    outPositionM = inPositionM;
    outScatter = vec4(0.0);
    outDrift = vec4(0.0);
    outWaveRad = vec3(0.0);
    if (filed >= BRIDGE_FIRST && filed <= BRIDGE_LAST) {
        outUv = vec2(float(inShade & ALONG_STEPS) / TRACK_STEPS_PER_PICTURE, float((inShade >> 18) & CHANNEL_STEPS) / ACROSS_STEPS);
        outTint = vec3(1.0);
        outSurface = filed;
        outPatchLook = vec4(0.0);
        return;
    }

    if (filed == TRACK || filed == KERB || filed == EDGING) {
        vec2 strip = vec2(float(inShade & ALONG_STEPS) / TRACK_STEPS_PER_PICTURE, float((inShade >> 18) & CHANNEL_STEPS) / ACROSS_STEPS);
        // A kerb's picture has its low side at the bottom: the town's kerb drops to the road on the right of its line,
        // the walk's to the grass on the left.
        outUv = filed == EDGING ? vec2(strip.x, 1.0 - strip.y) : strip;
        outTint = vec3(1.0);
        outSurface = filed;
        outPatchLook = vec4(0.0);
        // A kerb weathers as the walk it bounds does, paler and warmer in patches, on a field of its own.
        if (filed != TRACK) {
            outPatchAt = inPositionM * camera.patchPerAcross[PAVEMENT];
            outPatchLook = camera.patchLook[PAVEMENT];
        }
        return;
    }

    outTint = vec3(uvec3(inShade, inShade >> 9, inShade >> 18) & uvec3(CHANNEL_STEPS)) * (BRIGHTEST_TINT / float(CHANNEL_STEPS));
    if (filed != PAINT_FILED && (filed & FADING_FILED) != 0u) {
        filed &= ~FADING_FILED;
        outCover = outTint.x / BRIGHTEST_TINT;
        outTint = vec3(1.0);
    }

    float period = filed < 4u ? camera.surfacePeriodsM[filed]
        : filed == 4u ? camera.waterPeriodM.x
        : filed == SHORE ? camera.waterPeriodM.y : 1.0;
    float perAcross = filed < 4u ? camera.patchPerAcross[filed]
        : filed == 4u ? camera.patchPerAcrossWet.x
        : filed == SHORE ? camera.patchPerAcrossWet.y : 0.0;
    outUv = inPositionM / period;
    outSurface = filed == PAINT_FILED ? PAINT : filed;
    outPatchLook = filed <= 4u ? camera.patchLook[filed] : filed == SHORE ? camera.patchLook[SHORE_ROW] : vec4(0.0);
    outPatchAt = inPositionM * perAcross;
    outScatter = vec4(period * camera.patchScatter.x, camera.patchScatter.y, camera.patchRagged.xy);
    if (filed == 4u) {
        outDrift = camera.waterDriftM / period;
        outWaveRad = camera.waveNumberXPerM.xyz * inPositionM.x + camera.waveNumberYPerM.xyz * inPositionM.y - camera.wavePhaseRad.xyz;
    }
}
