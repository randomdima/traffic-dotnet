#version 460

// A body on the ground: one upright quad per instance, built out of gl_VertexIndex so there is no
// vertex buffer to bind and nothing per-vertex to upload. Everything that changes between frames is
// the instance buffer, which is mapped memory the CPU writes straight into.

layout(set = 0, binding = 1) uniform Camera {
    vec2 centreM;
    vec2 clipPerM;
} camera;

layout(location = 0) in vec2 inCentreM;
layout(location = 1) in vec2 inHalfSizeM;
layout(location = 2) in vec2 inUvMin;
layout(location = 3) in vec2 inUvSize;
layout(location = 4) in vec4 inTint;
layout(location = 5) in uint inSheet;
layout(location = 6) in float inHeadingRad;

layout(location = 0) out vec2 outUv;
layout(location = 1) out vec4 outTint;
layout(location = 2) flat out uint outSheet;

void main() {
    // A triangle strip's four corners, in the order the strip wants them.
    vec2 corner = vec2(float(gl_VertexIndex & 1), float((gl_VertexIndex >> 1) & 1));
    vec2 fromCentreM = (corner * 2.0 - 1.0) * inHalfSizeM;

    // Upright is a rotation of nothing, so the walkers pay one sine and one cosine and no branch.
    float turn = inHeadingRad;
    vec2 along = vec2(cos(turn), sin(turn));
    vec2 across = vec2(-along.y, along.x);
    vec2 positionM = inCentreM + along * fromCentreM.x + across * fromCentreM.y;

    gl_Position = vec4((positionM - camera.centreM) * camera.clipPerM, 0.0, 1.0);
    outUv = inUvMin + corner * inUvSize;
    outTint = inTint;
    outSheet = inSheet;
}
