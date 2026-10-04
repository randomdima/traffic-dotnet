#version 460

// The town's standing ground. The texture coordinate is the world position over the surface's period
// and nothing else, which is what anchors every surface to the world origin rather than to the shape
// being painted (New-Engine-Requirements/terrain.md, "Textures"); a corner carries its place and one
// word of shade and surface (GroundVertex), and the periods are the camera's.

// The camera lives in a buffer the CPU writes into rather than in a push constant, because a push
// constant is recorded into the command buffer and this engine records its command buffers once.
// The whole block is declared even where a stage reads two of it, because std140 lays a member at the
// offset its predecessors leave it at: a block that stopped at clipPerM would put facing where uiPx is.
layout(set = 0, binding = 0) uniform Camera {
    vec2 centreM;
    vec2 clipPerM;
    vec2 uiPx;
    // How far the town is turned on screen, as its cosine and its sine (OBS-1c). Upright is (1, 0).
    vec2 facing;
    // The period each surface's texture repeats over: grass, tarmac, pavement and deck, then the water.
    vec4 surfacePeriodsM;
    vec4 waterPeriodM;
} camera;

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

// How a shade is filed, said again from GroundVertex: three channels of nine bits up to the brightest
// tint, and the surface in the five above them, paint filed as the last of those.
const float BRIGHTEST_TINT = 3.0;
const uint CHANNEL_STEPS = 511u;
const uint PAINT_FILED = 31u;
const uint PAINT = 255u;

void main() {
    // +y is down in the world and +y is down in Vulkan's clip space, so nothing is flipped anywhere.
    gl_Position = toClip(inPositionM);

    uint filed = inShade >> 27;
    float period = filed < 4u ? camera.surfacePeriodsM[filed] : filed == 4u ? camera.waterPeriodM.x : 1.0;
    outUv = inPositionM / period;
    outTint = vec3(uvec3(inShade, inShade >> 9, inShade >> 18) & uvec3(CHANNEL_STEPS)) * (BRIGHTEST_TINT / float(CHANNEL_STEPS));
    outSurface = filed == PAINT_FILED ? PAINT : filed;
}
