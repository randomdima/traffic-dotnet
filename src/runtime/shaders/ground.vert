#version 460

// The town's standing ground. The texture coordinate arrives already computed, from the world
// position alone, which is what anchors every surface to the world origin rather than to the shape
// being painted (New-Engine-Requirements/terrain.md, "Textures").

// The camera lives in a buffer the CPU writes into rather than in a push constant, because a push
// constant is recorded into the command buffer and this engine records its command buffers once.
layout(set = 0, binding = 0) uniform Camera {
    vec2 centreM;
    vec2 clipPerM;
} camera;

layout(location = 0) in vec2 inPositionM;
layout(location = 1) in vec2 inUv;
layout(location = 2) in vec3 inTint;
layout(location = 3) in uint inSurface;

layout(location = 0) out vec2 outUv;
layout(location = 1) out vec3 outTint;
layout(location = 2) flat out uint outSurface;

void main() {
    // +y is down in the world and +y is down in Vulkan's clip space, so nothing is flipped anywhere.
    gl_Position = vec4((inPositionM - camera.centreM) * camera.clipPerM, 0.0, 1.0);
    outUv = inUv;
    outTint = inTint;
    outSurface = inSurface;
}
