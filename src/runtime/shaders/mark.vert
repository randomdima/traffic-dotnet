#version 460
#extension GL_GOOGLE_include_directive : require

// What the traffic has written on the ground, one knot of a stroke an instance, read straight out of the ground's ring of
// knots (drift_marks.glsl) — the device's own where the town is stepped there, the frame's copy of the host's where it
// is not (TownRenderer). Instance i is the i-th oldest knot: the ring's oldest once it has wrapped, place nought before.
// A knot draws the curve from the knot before it as a strip of DriftKnotPieces pieces: a cubic leaving that knot and
// meeting this one along each's way of travel (Hermite, each end's pull the chord), as wide and as dark as the two
// knots between them; a stroke's first knot, or one whose knot before the ring has given up, draws nothing. How each kind
// is drawn is MarkSprites', the look laid once (MarkLook). town.wgsl's markVertex is this, said again.
//
// The ring is drawn once a level (TER-7b): the ground's marks under the level above and its own over its decks, each draw
// drawing the knots of its level alone (DriftKnot.Raised).

#include "app/render/kernels/quad.glsl"
#include "world/terrain/kernels/drift_mark_rows.glsl"

// Whether this draw is the level above's (TownRenderer.CreateMarkPipelines).
layout(constant_id = 0) const bool DrawsRaised = false;

layout(set = 0, binding = 9) uniform MarkLook {
    vec4 soil;
    uint rubberSheet;
    uint soilSheet;
    float rubberBleed;
    float soilBleed;
    float rubberAlpha;
    float soilAlpha;
} look;

layout(std430, set = 0, binding = 10) readonly buffer MarkRows { DriftKnot rows[]; };
layout(std430, set = 0, binding = 11) readonly buffer MarkRing { DriftMarkRing ring; };

void nothing() {
    gl_Position = vec4(0.0, 0.0, 2.0, 1.0);
    outUv = vec3(0.0);
    outTint = vec4(0.0);
    outLight = 0.0;
    outFans = 0u;
    outLocal = vec2(0.0);
    outTurns = 0.0;
    outLook = 0.0;
    outRingCell = vec4(0.0);
    outRing = vec4(0.0);
    outRingHue = vec4(0.0);
    outEdgeHue = vec4(0.0);
}

void main() {
    uint oldest = ring.laid == ring.capacity ? ring.total % ring.capacity : 0u;
    DriftKnot to = rows[(oldest + uint(gl_InstanceIndex)) % ring.capacity];
    if (to.previous == DriftNoKnot || ring.total - to.previous > ring.laid || (to.widthM < 0.0) != DrawsRaised) {
        nothing();
        return;
    }

    DriftKnot from = rows[to.previous % ring.capacity];
    float t = float(uint(gl_VertexIndex) >> 1u) / float(DriftKnotPieces);
    float side = float(gl_VertexIndex & 1) * 2.0 - 1.0;

    vec2 chordM = to.atM - from.atM;
    float pullM = length(chordM);
    vec2 leaving = vec2(cos(from.headingRad), sin(from.headingRad)) * pullM;
    vec2 meeting = vec2(cos(to.headingRad), sin(to.headingRad)) * pullM;
    float tt = t * t;
    float ttt = tt * t;
    vec2 atM = (2.0 * ttt - 3.0 * tt + 1.0) * from.atM + (ttt - 2.0 * tt + t) * leaving
               + (-2.0 * ttt + 3.0 * tt) * to.atM + (ttt - tt) * meeting;
    vec2 alongM = (6.0 * tt - 6.0 * t) * from.atM + (3.0 * tt - 4.0 * t + 1.0) * leaving
                  + (6.0 * t - 6.0 * tt) * to.atM + (3.0 * tt - 2.0 * t) * meeting;
    vec2 along = dot(alongM, alongM) > 1e-12 ? normalize(alongM) : vec2(cos(to.headingRad), sin(to.headingRad));

    // Rubber is the road stained darker, soil is the turf turned over: each its own colour, opacity and bleed.
    bool ploughed = to.strength < 0.0;
    float strength = mix(abs(from.strength), abs(to.strength), t);
    float halfWidthM = mix(abs(from.widthM), abs(to.widthM), t) * (ploughed ? look.soilBleed : look.rubberBleed) * 0.5;
    uint sheet = ploughed ? look.soilSheet : look.rubberSheet;
    SheetPlace place = sheets.place[sheet];
    vec2 uv = vec2(t, side * 0.5 + 0.5);

    gl_Position = toClip(atM + vec2(-along.y, along.x) * (side * halfWidthM));
    outUv = vec3(place.originScale.xy + uv * place.originScale.zw, place.layerSize.x);
    outTint = vec4(ploughed ? look.soil.rgb : vec3(0.0), strength * (ploughed ? look.soilAlpha : look.rubberAlpha));
    outLight = place.layerSize.w;
    outFans = 0u;
    outLocal = uv;
    outTurns = 0.0;
    outLook = 0.0;
    outRingCell = vec4(0.0);
    outRing = vec4(0.0);
    outRingHue = vec4(0.0);
    outEdgeHue = vec4(0.0);
}
