#version 460
#extension GL_GOOGLE_include_directive : require

#include "app/render/kernels/fans.glsl"

// Every sheet in the town on one array texture, packed by SheetAtlas: which picture an instance is
// drawn with is a layer and a rectangle rather than a descriptor, so nothing here is indexed by a
// number the driver has to scalarise. Clamped and un-mipped, because a page is sheets side by side.
layout(set = 0, binding = 2) uniform sampler2DArray sheets;

layout(location = 0) in vec3 inUv;
layout(location = 1) in vec4 inTint;
layout(location = 2) flat in float inLight;
layout(location = 3) flat in uint inFans;
layout(location = 4) in vec2 inLocal;
layout(location = 6) flat in float inTurns;
layout(location = 7) flat in float inLook;

layout(location = 0) out vec4 outColour;

void main() {
    vec4 texel = texture(sheets, inUv);
    // A picture that draws fans has its look laid over each one, turned (OBJ-6, TRN-13).
    vec3 lookUv;
    if (inFans != 0u && fansLook(inLocal, inFans, inTurns, inLook, lookUv)) {
        vec4 fan = texture(sheets, lookUv);
        texel = vec4(mix(texel.rgb, fan.rgb, fan.a), fan.a + texel.a * (1.0 - fan.a));
    }

    // The art is keyed, so most of every cell is nothing at all; discarding it keeps the blend from
    // laying a rectangle of near-zero alpha over whatever the sprite is standing on.
    if (texel.a < 0.004) discard;

    // The tint's fourth is the instance's own opacity — one for everything the town draws but a mark,
    // which is laid at whatever strength the tyre that made it earned.
    float alpha = texel.a * inTint.a;

    // Premultiplied, blended One over OneMinusSrcAlpha: laid over at its alpha, or — a sheet of light, its
    // coverage nought — summed onto what is under it.
    outColour = vec4(texel.rgb * inTint.rgb * alpha, alpha * (1.0 - inLight));
}
