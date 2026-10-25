#version 460
#extension GL_GOOGLE_include_directive : require

// A body on the ground: one upright quad per instance, built out of gl_VertexIndex so there is no
// vertex buffer to bind and nothing per-vertex to upload. Everything that changes between frames is
// the instance buffer, which is mapped memory the CPU writes straight into.

#include "app/render/kernels/quad.glsl"

// SpriteInstance, packed: the half-size arrives as two halves, the cell as four bytes — its column and row and the
// grid's columns and rows — the tint as four normalised bytes, and the sheet and the ID as two half-words, each widened
// by the vertex fetch.
layout(location = 0) in vec2 inCentreM;
layout(location = 1) in vec2 inHalfSizeM;
layout(location = 2) in float inHeadingRad;
layout(location = 3) in uvec4 inCell;
layout(location = 4) in vec4 inTint;
layout(location = 5) in uvec2 inSheetId;

void main() {
    quadEmit(inCentreM, inHalfSizeM, inHeadingRad, vec2(inCell.xy), vec2(inCell.zw), inTint, inSheetId.x, inSheetId.y);
}
