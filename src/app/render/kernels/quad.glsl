// One upright quad on the ground, as every stage that draws a body builds it (sprite.vert, mark.vert, scenery.vert):
// out of gl_VertexIndex, turned to its heading in the town's axes, sampled from one cell of a sheet the atlas packed.
// The camera and the sheets' places are the frame's one set (TownRenderer), bindings 0 and 1; the fans 16.

#include "app/render/kernels/fans.glsl"

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
    // How far every fan has turned at this tick, in turns, and the least a picture's widest is across (CameraView.Turning).
    vec4 turning;
    // The car bodies' sheets from and past, and the height their tilt on their springs is seen from (CameraView.CarTilt).
    vec4 carTilt;
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
    // The layer, then the sheet's own size, which only the CPU reads, then one where the sheet adds light.
    vec4 layerSize;
};

layout(set = 0, binding = 1) uniform Sheets {
    SheetPlace place[2048]; // SheetAtlas.Slots
} sheets;

layout(location = 0) out vec3 outUv;
layout(location = 1) out vec4 outTint;
// One where the sheet is light, summed onto the picture rather than laid over it (SheetSource.AddsLight).
layout(location = 2) flat out float outLight;
// The sheet's run of fans, where on the sheet this point is, how far this body's fans have turned and which look they
// wear: what the fragment stage lays a fan's look over the body's own picture with (fans.glsl).
layout(location = 3) flat out uint outFans;
layout(location = 4) out vec2 outLocal;
layout(location = 6) flat out float outTurns;
layout(location = 7) flat out float outLook;

// A prop smaller on screen than the frame draws one (ViewFigures.PropLeastPx): every corner put past the far plane, so
// nothing of it is rasterised.
bool quadTooSmall(vec2 halfSizeM, uint sheet) {
    float look = float(sheet);
    return look >= camera.propCull.y && look < camera.propCull.z && max(halfSizeM.x, halfSizeM.y) < camera.propCull.x;
}

// A car body's tilt as its ID carries it (BodyTilt.Id): nose down in the low byte and right side down in the high, each a
// signed count of BodyTilt.StepsPerRad.
vec2 quadTiltRad(uint id) {
    return vec2(float(int(id << 24u) >> 24), float(int(id << 16u) >> 24)) * (1.0 / 512.0);
}

// The quad's corner for this vertex: cell (column, row) of a grid of (columns, rows) of the sheet, tinted; `id` is the
// body's own (SpriteInstance.Id).
void quadEmit(vec2 centreM, vec2 halfSizeM, float headingRad, vec2 cell, vec2 grid, vec4 tint, uint sheet, uint id) {
    if (quadTooSmall(halfSizeM, sheet)) {
        gl_Position = vec4(0.0, 0.0, 2.0, 1.0);
        outUv = vec3(0.0);
        outTint = vec4(0.0);
        outLight = 0.0;
        outFans = 0u;
        outLocal = vec2(0.0);
        outTurns = 0.0;
        outLook = 0.0;
        return;
    }

    // A triangle strip's four corners, in the order the strip wants them.
    vec2 corner = vec2(float(gl_VertexIndex & 1), float((gl_VertexIndex >> 1) & 1));
    vec2 fromCentreM = (corner * 2.0 - 1.0) * halfSizeM;

    // A car body tilted on its springs (CAR-56, BodyTilt): the plane its corners sink and rise to, as an eye
    // camera.carTilt.z over its middle sees it. The corner is drawn where that eye sees it and its w is how far off the eye
    // it is, so the picture across the quad is the tilted plane's and not two triangles bent at their seam.
    float depth = 1.0;
    float look = float(sheet);
    if (look >= camera.carTilt.x && look < camera.carTilt.y) {
        float sinkM = dot(fromCentreM, quadTiltRad(id));
        depth = (camera.carTilt.z + sinkM) / camera.carTilt.z;
        fromCentreM /= depth;
    }

    // Upright is a rotation of nothing, so the walkers pay one sine and one cosine and no branch.
    vec2 along = vec2(cos(headingRad), sin(headingRad));
    vec2 across = vec2(-along.y, along.x);
    gl_Position = toClip(centreM + along * fromCentreM.x + across * fromCentreM.y) * depth;

    SheetPlace place = sheets.place[sheet];
    vec2 uv = (cell + corner) / grid;
    outUv = vec3(place.originScale.xy + uv * place.originScale.zw, place.layerSize.x);
    outTint = tint;
    outLight = place.layerSize.w;
    outFans = fansRun(sheet, 2.0 * halfSizeM.x * grid.x, camera.turning.y);
    outLocal = uv;
    outTurns = fract(camera.turning.x + fansShare(id));
    outLook = fansShare(id ^ FAN_LOOK_SALT);
}
