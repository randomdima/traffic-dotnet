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
layout(location = 5) flat in vec4 inRingCell;
layout(location = 8) flat in vec4 inRing;
layout(location = 9) flat in vec4 inRingHue;
layout(location = 10) flat in vec4 inEdgeHue;

layout(location = 0) out vec4 outColour;

// Solid is most of opaque, as SheetOutline takes it: a soft fringe is not what the ring follows.
const float FAINT_ALPHA = 0.5;
const float SOLID_ALPHA = 0.9;
const vec2 WAYS[8] = vec2[8](
    vec2(1.0, 0.0), vec2(-1.0, 0.0), vec2(0.0, 1.0), vec2(0.0, -1.0),
    vec2(0.70710678, 0.70710678), vec2(-0.70710678, 0.70710678), vec2(0.70710678, -0.70710678), vec2(-0.70710678, -0.70710678));

bool inCell(vec2 uv) { return all(greaterThanEqual(uv, inRingCell.xy)) && all(lessThanEqual(uv, inRingCell.zw)); }

// How solid the picture is at `uv` on its page, and nothing past its own cell: the next sheet on the page is another picture.
float solidAt(vec2 uv) {
    return inCell(uv) ? smoothstep(FAINT_ALPHA, SOLID_ALPHA, textureLod(sheets, vec3(uv, inUv.z), 0.0).a) : 0.0;
}

// The ring (SpriteRings), premultiplied: the hue within its width of the picture's solid part, the edge's hue within the
// outer width, each read at its width and at half of it so a twig narrower than the width is not stepped over, and none of
// it over the picture itself. Read in the quad's own axes, which a ring the same all round does not mind.
vec4 ringAt(vec2 uv) {
    float nearHue = 0.0;
    float nearEdge = 0.0;
    for (int way = 0; way < 8; way++) {
        vec2 stepUv = WAYS[way] * inRing.zw;
        nearHue = max(nearHue, max(solidAt(uv + stepUv * inRing.x), solidAt(uv + stepUv * inRing.x * 0.5)));
        nearEdge = max(nearEdge, solidAt(uv + stepUv * inRing.y));
    }

    float outside = 1.0 - solidAt(uv);
    float hue = inRingHue.a * nearHue * outside;
    float edge = inEdgeHue.a * max(nearHue, nearEdge) * outside * (1.0 - hue);
    return vec4(inRingHue.rgb * hue + inEdgeHue.rgb * edge, hue + edge);
}

void main() {
    vec4 texel = texture(sheets, inUv);
    // A picture that draws fans has its look laid over each one, turned (OBJ-6, TRN-13).
    vec3 lookUv;
    if (inFans != 0u && fansLook(inLocal, inFans, inTurns, inLook, lookUv)) {
        vec4 fan = texture(sheets, lookUv);
        texel = vec4(mix(texel.rgb, fan.rgb, fan.a), fan.a + texel.a * (1.0 - fan.a));
    }

    // A ringed quad is grown past its cell, where the page holds another picture.
    bool ringed = inRing.y > 0.0;
    if (ringed && !inCell(inUv.xy)) texel = vec4(0.0);
    vec4 ring = ringed ? ringAt(inUv.xy) : vec4(0.0);

    // The art is keyed, so most of every cell is nothing at all; discarding it keeps the blend from
    // laying a rectangle of near-zero alpha over whatever the sprite is standing on.
    if (texel.a < 0.004 && ring.a < 0.004) discard;

    // The tint's fourth is the instance's own opacity — one for everything the town draws but a mark,
    // which is laid at whatever strength the tyre that made it earned.
    float alpha = texel.a * inTint.a;

    // Premultiplied, blended One over OneMinusSrcAlpha: laid over at its alpha, or — a sheet of light, its
    // coverage nought — summed onto what is under it. The ring lies under the picture's own fringe.
    outColour = vec4(texel.rgb * inTint.rgb * alpha, alpha * (1.0 - inLight)) + ring * (1.0 - alpha);
}
