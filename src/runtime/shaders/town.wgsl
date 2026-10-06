// The whole town in one module: three vertex stages, three fragment stages and the one bind group all
// of them read. It is one file and not three because WGSL has no include and the group is what the
// three share — three copies of it would be three chances to disagree about a binding number.
//
// It is the GLSL beside it said again, and there are exactly three differences worth knowing:
//   * a page is sampled with an explicit sampler, because WGSL has no combined image sampler;
//   * clip space is y-up here and y-down in Vulkan, so every position negates y at the end;
//   * an array of samplers cannot be indexed at all, which the ground's switch already answered.

struct Camera {
    centreM: vec2f,
    clipPerM: vec2f,
    uiPx: vec2f,
    // How far the town is turned on screen, as its cosine and its sine (OBS-1c). Upright is (1, 0).
    facing: vec2f,
    // The period each surface's texture repeats over: grass, tarmac, pavement and deck, then the water.
    surfacePeriodsM: vec4f,
    waterPeriodM: vec4f,
};

@group(0) @binding(0) var<uniform> camera: Camera;

// Where each sheet was packed, as SheetAtlas laid it. Two vec4s a sheet, which is the C# struct.
struct Place {
    originScale: vec4f,
    // The layer, then the sheet's own size, which only the CPU reads.
    layerSize: vec4f,
};

struct Sheets {
    place: array<Place, 512>,
};

@group(0) @binding(1) var<uniform> sheets: Sheets;

@group(0) @binding(2) var pages: texture_2d_array<f32>;
@group(0) @binding(3) var glyphs: texture_2d<f32>;
@group(0) @binding(4) var grass: texture_2d<f32>;
@group(0) @binding(5) var tarmac: texture_2d<f32>;
@group(0) @binding(6) var pavement: texture_2d<f32>;
@group(0) @binding(7) var deck: texture_2d<f32>;
@group(0) @binding(8) var water: texture_2d<f32>;
@group(0) @binding(9) var clamped: sampler;
@group(0) @binding(10) var repeated: sampler;

/// The town's own metres to clip. The negation is the whole of what makes a y-up API draw a y-down
/// world the same way the y-down one does.
///
/// **The turn is applied here and nowhere else** (OBS-1c). A sprite's heading and a band's direction
/// are built in the town's own axes above this, so turning the whole offset turns them with it — which
/// is why nothing that carries a rotation of its own knows the town is turned at all.
fn toClip(atM: vec2f) -> vec4f {
    let fromCentreM = atM - camera.centreM;
    let turnedM = vec2f(
        fromCentreM.x * camera.facing.x - fromCentreM.y * camera.facing.y,
        fromCentreM.x * camera.facing.y + fromCentreM.y * camera.facing.x);
    let offset = turnedM * camera.clipPerM;
    return vec4f(offset.x, -offset.y, 0.0, 1.0);
}

/// A triangle strip's four corners, in the order the strip wants them.
fn corner(vertex: u32) -> vec2f {
    return vec2f(f32(vertex & 1u), f32((vertex >> 1u) & 1u));
}

struct GroundOut {
    @builtin(position) position: vec4f,
    @location(0) uv: vec2f,
    @location(1) tint: vec3f,
    @location(2) @interpolate(flat) surface: u32,
};

// How a shade is filed, said again from GroundVertex: three channels of nine bits up to the brightest
// tint, and the surface in the five above them, paint filed as the last of those.
const BRIGHTEST_TINT: f32 = 3.0;
const CHANNEL_STEPS: u32 = 511u;
const PAINT_FILED: u32 = 31u;

/// A corner carries its place and one word of shade and surface; its texture coordinate is its place over
/// its surface's period, which anchors every surface to the world origin.
@vertex
fn groundVertex(
    @location(0) positionM: vec2f,
    @location(1) shade: u32,
) -> GroundOut {
    var result: GroundOut;
    result.position = toClip(positionM);

    let filed = shade >> 27u;
    var period = 1.0;
    if (filed < 4u) {
        period = camera.surfacePeriodsM[filed];
    } else if (filed == 4u) {
        period = camera.waterPeriodM.x;
    }

    result.uv = positionM / period;
    result.tint = vec3f(vec3u(shade, shade >> 9u, shade >> 18u) & vec3u(CHANNEL_STEPS)) * (BRIGHTEST_TINT / f32(CHANNEL_STEPS));
    result.surface = select(filed, PAINT, filed == PAINT_FILED);
    return result;
}

// Not a surface: the tint alone, for a mark that is not ground.
const PAINT: u32 = 255u;

// The gradients are handed in rather than taken here, and that is not a style: WGSL will not let a
// texture be sampled with implicit derivatives under control flow that may differ between the pixels
// of a quad, and which surface a triangle wears is exactly such a value. Taken once above the branch,
// where every pixel agrees, they are the same two vectors the implicit sample would have used — so the
// mip level is the one the ground has always been drawn at.
fn surfaceColour(which: u32, uv: vec2f, ddx: vec2f, ddy: vec2f) -> vec3f {
    switch which {
        case 1u: { return textureSampleGrad(tarmac, repeated, uv, ddx, ddy).rgb; }
        case 2u: { return textureSampleGrad(pavement, repeated, uv, ddx, ddy).rgb; }
        case 3u: { return textureSampleGrad(deck, repeated, uv, ddx, ddy).rgb; }
        case 4u: { return textureSampleGrad(water, repeated, uv, ddx, ddy).rgb; }
        default: { return textureSampleGrad(grass, repeated, uv, ddx, ddy).rgb; }
    }
}

@fragment
fn groundFragment(frag: GroundOut) -> @location(0) vec4f {
    let ddx = dpdx(frag.uv);
    let ddy = dpdy(frag.uv);

    // An edge is the surface drawn darker and paint is the surface drawn brighter, so both are the
    // ground's own texture through a tint and the grain comes through both.
    if (frag.surface == PAINT) {
        return vec4f(frag.tint, 1.0);
    }

    return vec4f(surfaceColour(frag.surface, frag.uv, ddx, ddy) * frag.tint, 1.0);
}

struct SpriteOut {
    @builtin(position) position: vec4f,
    @location(0) uv: vec3f,
    @location(1) tint: vec4f,
};

@vertex
fn spriteVertex(
    @builtin(vertex_index) vertex: u32,
    @location(0) centreM: vec2f,
    @location(1) halfSizeM: vec2f,
    @location(2) uvMin: vec2f,
    @location(3) uvSize: vec2f,
    @location(4) tint: vec4f,
    @location(5) sheet: u32,
    @location(6) headingRad: f32,
) -> SpriteOut {
    let at = corner(vertex);
    let fromCentreM = (at * 2.0 - 1.0) * halfSizeM;

    // Upright is a rotation of nothing, so the walkers pay one sine and one cosine and no branch.
    let along = vec2f(cos(headingRad), sin(headingRad));
    let across = vec2f(-along.y, along.x);

    let place = sheets.place[sheet];
    let uv = uvMin + at * uvSize;

    var result: SpriteOut;
    result.position = toClip(centreM + along * fromCentreM.x + across * fromCentreM.y);
    result.uv = vec3f(place.originScale.xy + uv * place.originScale.zw, place.layerSize.x);
    result.tint = tint;
    return result;
}

@fragment
fn spriteFragment(frag: SpriteOut) -> @location(0) vec4f {
    // The pages carry no mip chain, so the atlas is sampled at the top level.
    let texel = textureSampleLevel(pages, clamped, frag.uv.xy, i32(frag.uv.z), 0.0);

    // The art is keyed, so most of every cell is nothing at all; discarding it keeps the blend from
    // laying a rectangle of near-zero alpha over whatever the sprite is standing on.
    if (texel.a < 0.004) {
        discard;
    }

    return vec4f(texel.rgb * frag.tint.rgb, texel.a * frag.tint.a);
}

struct OverlayOut {
    @builtin(position) position: vec4f,
    @location(0) uv: vec2f,
    @location(1) colour: vec4f,
};

@vertex
fn overlayVertex(
    @builtin(vertex_index) vertex: u32,
    @location(0) centre: vec2f,
    @location(1) halfSize: vec2f,
    @location(2) uvMin: vec2f,
    @location(3) uvSize: vec2f,
    @location(4) colour: vec4f,
    @location(5) rotation: f32,
    @location(6) screen: u32,
    @location(7) taper: f32,
) -> OverlayOut {
    let at = corner(vertex);
    let side = at * 2.0 - 1.0;

    // The taper slants the two ends in opposite directions, which is how a piece of a band round a
    // bend is cut square to the line rather than to the chord.
    let fromCentre = vec2f(side.x * (halfSize.x - side.y * taper), side.y * halfSize.y);
    let along = vec2f(cos(rotation), sin(rotation));
    let across = vec2f(-along.y, along.x);
    let atPx = centre + along * fromCentre.x + across * fromCentre.y;

    var result: OverlayOut;
    if (screen == 0u) {
        result.position = toClip(atPx);
    } else {
        // Screen space is in interface pixels from the top-left, so an interface that is laid out once
        // does not move when the camera does. Interface pixels and not the framebuffer's: this
        // division is where the display's scale factor is paid.
        let unit = atPx / camera.uiPx;
        result.position = vec4f(unit.x * 2.0 - 1.0, 1.0 - unit.y * 2.0, 0.0, 1.0);
    }

    result.uv = uvMin + at * uvSize;
    result.colour = colour;
    return result;
}

@fragment
fn overlayFragment(frag: OverlayOut) -> @location(0) vec4f {
    // The sheet is coverage and nothing else: it carries no colour of its own, so the quad's colour
    // is the whole of what is drawn and the sheet only says how much of it lands.
    return vec4f(frag.colour.rgb, frag.colour.a * textureSample(glyphs, clamped, frag.uv).a);
}
