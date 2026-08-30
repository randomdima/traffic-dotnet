#version 460

// A body on the ground: one upright quad per instance, built out of gl_VertexIndex so there is no
// vertex buffer to bind and nothing per-vertex to upload. Everything that changes between frames is
// the instance buffer, which is mapped memory the CPU writes straight into.

layout(set = 0, binding = 0) uniform Camera {
    vec2 centreM;
    vec2 clipPerM;
} camera;

// Where each sheet was packed, as SheetAtlas laid it: the instance still names a sheet by number, and
// that number is now a rectangle of a page rather than a texture of its own. Reading it here rather
// than in the fragment stage costs four lookups a quad instead of one a pixel.
struct SheetPlace {
    vec4 originScale;
    vec4 layerTilesSize;
};

layout(set = 0, binding = 1) uniform Sheets {
    SheetPlace place[192];
} sheets;

layout(location = 0) in vec2 inCentreM;
layout(location = 1) in vec2 inHalfSizeM;
layout(location = 2) in vec2 inUvMin;
layout(location = 3) in vec2 inUvSize;
layout(location = 4) in vec4 inTint;
layout(location = 5) in uint inSheet;
layout(location = 6) in float inHeadingRad;

layout(location = 0) out vec3 outUv;
layout(location = 1) out vec4 outTint;
layout(location = 2) flat out float outTiles;
layout(location = 3) out vec2 outTileUv;

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

    SheetPlace place = sheets.place[inSheet];
    vec2 uv = inUvMin + corner * inUvSize;
    outUv = vec3(place.originScale.xy + uv * place.originScale.zw, place.layerTilesSize.x);
    // The sheet's own coordinate, kept for the one sheet that tiles: the tread's runs outside the
    // unit square by however many pitches the wheel lays, which is what an atlas cannot hold.
    outTileUv = uv;
    outTiles = place.layerTilesSize.y;
    outTint = inTint;
}
