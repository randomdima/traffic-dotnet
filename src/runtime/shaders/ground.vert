#version 460

// The town's standing ground. The texture coordinate arrives already computed, from the world
// position alone, which is what anchors every surface to the world origin rather than to the shape
// being painted (New-Engine-Requirements/terrain.md, "Textures").

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
layout(location = 1) in vec2 inUv;
layout(location = 2) in vec3 inTint;
layout(location = 3) in uint inSurface;

layout(location = 0) out vec2 outUv;
layout(location = 1) out vec3 outTint;
layout(location = 2) flat out uint outSurface;

void main() {
    // +y is down in the world and +y is down in Vulkan's clip space, so nothing is flipped anywhere.
    gl_Position = toClip(inPositionM);
    outUv = inUv;
    outTint = inTint;
    outSurface = inSurface;
}
