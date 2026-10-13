// One upright quad on the ground, as every stage that draws a body builds it (sprite.vert, mark.vert, scenery.vert):
// out of gl_VertexIndex, turned to its heading in the town's axes, sampled from one cell of a sheet the atlas packed.
// The camera and the sheets' places are the frame's one set (TownRenderer), bindings 0 and 1.

// The whole block is declared, because std140 lays a member at the offset its predecessors leave it at (CameraView).
layout(set = 0, binding = 0) uniform Camera {
    vec2 centreM;
    vec2 clipPerM;
    vec2 uiPx;
    // How far the town is turned on screen, as its cosine and its sine (OBS-1c). Upright is (1, 0).
    vec2 facing;
    vec4 surfacePeriodsM;
    vec4 waterPeriodM;
    // The least half-size a prop is drawn at this frame, then the prop looks' sheets from and past (CameraView.PropCull).
    vec4 propCull;
} camera;

// The town's own metres to clip. **The turn is applied here and nowhere else**: the quad is built about its own heading
// in the town's axes, and turning the offset turns the built quad with it.
vec4 toClip(vec2 atM) {
    vec2 fromCentreM = atM - camera.centreM;
    vec2 turnedM = vec2(
        fromCentreM.x * camera.facing.x - fromCentreM.y * camera.facing.y,
        fromCentreM.x * camera.facing.y + fromCentreM.y * camera.facing.x);
    return vec4(turnedM * camera.clipPerM, 0.0, 1.0);
}

// Where each sheet was packed, as SheetAtlas laid it: the instance names a sheet by number, and that number is a
// rectangle of a page. Read here rather than in the fragment stage: four lookups a quad instead of one a pixel.
struct SheetPlace {
    vec4 originScale;
    // The layer, then the sheet's own size, which only the CPU reads.
    vec4 layerSize;
};

layout(set = 0, binding = 1) uniform Sheets {
    SheetPlace place[512];
} sheets;

layout(location = 0) out vec3 outUv;
layout(location = 1) out vec4 outTint;

// A prop smaller on screen than the frame draws one (ViewFigures.PropLeastPx): every corner put past the far plane, so
// nothing of it is rasterised.
bool quadTooSmall(vec2 halfSizeM, uint sheet) {
    float look = float(sheet);
    return look >= camera.propCull.y && look < camera.propCull.z && max(halfSizeM.x, halfSizeM.y) < camera.propCull.x;
}

// The quad's corner for this vertex: cell (column, row) of a grid of (columns, rows) of the sheet, tinted.
void quadEmit(vec2 centreM, vec2 halfSizeM, float headingRad, vec2 cell, vec2 grid, vec4 tint, uint sheet) {
    if (quadTooSmall(halfSizeM, sheet)) {
        gl_Position = vec4(0.0, 0.0, 2.0, 1.0);
        outUv = vec3(0.0);
        outTint = vec4(0.0);
        return;
    }

    // A triangle strip's four corners, in the order the strip wants them.
    vec2 corner = vec2(float(gl_VertexIndex & 1), float((gl_VertexIndex >> 1) & 1));
    vec2 fromCentreM = (corner * 2.0 - 1.0) * halfSizeM;

    // Upright is a rotation of nothing, so the walkers pay one sine and one cosine and no branch.
    vec2 along = vec2(cos(headingRad), sin(headingRad));
    vec2 across = vec2(-along.y, along.x);
    gl_Position = toClip(centreM + along * fromCentreM.x + across * fromCentreM.y);

    SheetPlace place = sheets.place[sheet];
    vec2 uv = (cell + corner) / grid;
    outUv = vec3(place.originScale.xy + uv * place.originScale.zw, place.layerSize.x);
    outTint = tint;
}
