#version 460
#extension GL_GOOGLE_include_directive : require

// The town's scenery, one quad an instance, read out of the pieces laid on the device once (ScenerySprites.Pieces) by the
// runs a frame wrote of the rows in view (ScenerySprites.Spans): instance i belongs to the last run drawing nothing
// after it, and is its first piece and every `every`-th after. A piece's look gives its sheet and its size.

#include "app/render/kernels/quad.glsl"

// SceneryPiece: where it stands, then its look.
struct Piece {
    float x;
    float y;
    uint look;
};

// What a look is drawn as: its half-size and its sheet.
struct SceneryLook {
    vec2 halfSizeM;
    uint sheet;
    uint pad;
};

layout(std430, set = 0, binding = 12) readonly buffer Pieces { Piece pieces[]; };
layout(std430, set = 0, binding = 13) readonly buffer Looks { SceneryLook looks[]; };

// The frame's runs: the draw first, so the block is the indirect buffer of that draw, then the thinning and how many
// runs, then each run's first piece and how many are drawn before it (ScenerySpan).
layout(std430, set = 0, binding = 14) readonly buffer Spans {
    uvec4 draw;
    uint every;
    uint spanCount;
    uint pad0;
    uint pad1;
    uvec2 spans[];
};

void main() {
    uint at = uint(gl_InstanceIndex);
    uint lo = 0u;
    uint hi = spanCount;
    while (hi - lo > 1u) {
        uint mid = (lo + hi) >> 1;
        if (spans[mid].y <= at) lo = mid;
        else hi = mid;
    }

    Piece piece = pieces[spans[lo].x + (at - spans[lo].y) * every];
    SceneryLook drawn = looks[piece.look];
    quadEmit(vec2(piece.x, piece.y), drawn.halfSizeM, 0.0, vec2(0.0), vec2(1.0), vec4(1.0), drawn.sheet, 0u);
}
