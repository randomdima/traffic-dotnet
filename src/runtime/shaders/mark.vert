#version 460
#extension GL_GOOGLE_include_directive : require

// What the traffic has written on the ground, one quad an instance, read straight out of the ground's ring of marks
// (drift_marks.glsl) — the device's own where the town is stepped there, the frame's copy of the host's where it is not
// (TownRenderer). Instance i is the i-th oldest mark: the ring's head once it has wrapped, place nought before. How each
// kind is drawn is MarkSprites', the look laid once (MarkLook).

#include "app/render/kernels/quad.glsl"
#include "world/terrain/kernels/drift_mark_rows.glsl"

layout(set = 0, binding = 9) uniform MarkLook {
    vec4 soil;
    uint rubberSheet;
    uint soilSheet;
    float rubberBleed;
    float soilBleed;
    float rubberAlpha;
    float soilAlpha;
} look;

layout(std430, set = 0, binding = 10) readonly buffer MarkRows { DriftMarkRow rows[]; };
layout(std430, set = 0, binding = 11) readonly buffer MarkRing { DriftMarkRing ring; };

void main() {
    uint oldest = ring.laid == ring.capacity ? ring.head : 0u;
    DriftMarkRow mark = rows[(oldest + uint(gl_InstanceIndex)) % ring.capacity];

    // Rubber is the road stained darker, soil is the turf turned over: each its own colour, opacity and bleed.
    bool ploughed = mark.strength < 0.0;
    vec2 halfSizeM = vec2(mark.lengthM, mark.widthM * (ploughed ? look.soilBleed : look.rubberBleed)) * 0.5;
    vec4 tint = vec4(ploughed ? look.soil.rgb : vec3(0.0), abs(mark.strength) * (ploughed ? look.soilAlpha : look.rubberAlpha));
    quadEmit(mark.centreM, halfSizeM, mark.headingRad, vec2(0.0), vec2(1.0), tint, ploughed ? look.soilSheet : look.rubberSheet);
}
