#version 460
#extension GL_EXT_nonuniform_qualifier : require

// The sheets are their own descriptor array rather than more slots in the ground's: a ground surface
// is sampled with repeat addressing and a sheet must never repeat, since one cell's neighbour is a
// different pose. Addressing is the sampler's and therefore per slot, which is how the one tile in
// the set — the tread a wheel lays several times along its own roll — repeats while the rest clamp.
layout(set = 0, binding = 2) uniform sampler2D sheets[];

layout(location = 0) in vec2 inUv;
layout(location = 1) in vec4 inTint;
layout(location = 2) flat in uint inSheet;

layout(location = 0) out vec4 outColour;

void main() {
    vec4 texel = texture(sheets[nonuniformEXT(inSheet)], inUv);
    // The art is keyed, so most of every cell is nothing at all; discarding it keeps the blend from
    // laying a rectangle of near-zero alpha over whatever the sprite is standing on.
    if (texel.a < 0.004) discard;

    // The tint's fourth is the instance's own opacity — one for everything the town draws but a mark,
    // which is laid at whatever strength the tyre that made it earned.
    outColour = vec4(texel.rgb * inTint.rgb, texel.a * inTint.a);
}
