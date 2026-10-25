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
    // The desktop's least prop and prop sheets (CameraView.PropCull); a page draws every prop and reads none of it.
    propCull: vec4f,
    // How far every fan has turned at this tick, in turns, and the least a picture's widest is across (CameraView.Turning).
    turning: vec4f,
    // The clouds' shadows (CameraView.Clouds), which a page does not draw: here so what follows lands where it is written.
    cloudField: vec4f,
    cloudLook: vec4f,
    // The patches every surface wears its looks by (GroundPatches): the field's mean and one over its spread; then each
    // surface's light and warmth a standard deviation of it, and where its second look begins and is whole, in the
    // surfaces' order; one over each surface's patches' width, the four on land then the water's and the shore's; the
    // hexes the looks are scattered in; and the lumps a fade is ragged by.
    patchField: vec4f,
    patchLook: array<vec4f, 6>,
    patchPerAcross: vec4f,
    patchPerAcrossWet: vec4f,
    patchScatter: vec4f,
    patchRagged: vec4f,
    // How far the water's still look and its lively one have drifted, in metres (GroundPatches.DriftAt).
    waterDriftM: vec4f,
};

@group(0) @binding(0) var<uniform> camera: Camera;

// Where each sheet was packed, as SheetAtlas laid it. Two vec4s a sheet, which is the C# struct.
struct Place {
    originScale: vec4f,
    // The layer, then the sheet's own size, which only the CPU reads, then one where the sheet adds light.
    layerSize: vec4f,
};

struct Sheets {
    place: array<Place, 2048>, // SheetAtlas.Slots
};

@group(0) @binding(1) var<uniform> sheets: Sheets;

@group(0) @binding(2) var pages: texture_2d_array<f32>;
@group(0) @binding(3) var glyphs: texture_2d<f32>;
@group(0) @binding(4) var grass: texture_2d_array<f32>;
@group(0) @binding(5) var tarmac: texture_2d_array<f32>;
@group(0) @binding(6) var pavement: texture_2d_array<f32>;
@group(0) @binding(7) var deck: texture_2d_array<f32>;
@group(0) @binding(8) var water: texture_2d_array<f32>;
@group(0) @binding(9) var clamped: sampler;
@group(0) @binding(10) var repeated: sampler;
// The track's strip, after the samplers so nothing before it moved when it came (ground.frag's binding 15).
@group(0) @binding(11) var track: texture_2d<f32>;

// The fans a roof or a vehicle draws (FanTable, OBJ-6, TRN-13; fans.glsl beside the desktop's): a run a sheet slot, its
// first fan in the low sixteen bits and how many in the high, then each fan's hub and radius in its sheet's own units,
// then past the fans the looks a fan is drawn in. What turns is one of the looks, laid over the body's own picture.
struct Fans {
    runs: array<vec4u, 512>,  // SheetAtlas.Slots / 4
    // FanTable.MostFans, two rows a fan: hub and radius, then the rim in the circle's units; then FanTable.MostLooks,
    // two rows a look: where it was packed, then its layer and how many looks there are.
    fans: array<vec4f, 2048>,
};

// FanTable.LookRow, and what a body's ID is mixed with before it picks its fans' look (fans.glsl).
const fanLookRow = 2016u;
const fanLookSalt = 0x5bd1e995u;

@group(0) @binding(12) var<uniform> fanTable: Fans;

// The shore, after the fans so nothing before it moved when it came (ground.frag's binding 17).
@group(0) @binding(13) var shore: texture_2d_array<f32>;

// And the two kerbs' strips after it (ground.frag's bindings 18 and 19).
@group(0) @binding(14) var kerb: texture_2d<f32>;
@group(0) @binding(15) var edging: texture_2d<f32>;

// The sheet's run, or nought where its widest fan, drawn `widthM` across, is less than `leastAcrossM` this frame.
fn fansRun(sheet: u32, widthM: f32, leastAcrossM: f32) -> u32 {
    let run = fanTable.runs[sheet >> 2u][sheet & 3u];
    let first = run & 0xffffu;
    for (var fan = 0u; fan < (run >> 16u); fan++) {
        if (2.0 * fanTable.fans[2u * (first + fan)].z * widthM >= leastAcrossM) {
            return run;
        }
    }

    return 0u;
}

// A body's own share of a turn, off its ID (SpriteInstance.Id). lowbias32.
fn fansShare(body: u32) -> f32 {
    var id = body;
    id ^= id >> 16u;
    id *= 0x7feb352du;
    id ^= id >> 15u;
    id *= 0x846ca68bu;
    id ^= id >> 16u;
    return f32(id) * (1.0 / 4294967296.0);
}

// Where a fragment at `local` on its sheet reads the body's look (`lookShare` of the way through the looks) inside one
// of the run's fans — the look's middle on the hub, its edge on the rim, turned — as (u, v, layer, 1); its last nought
// outside every fan. The body's share `turns`, each further fan of it a golden section on.
fn fansLook(local: vec2f, run: u32, turns: f32, lookShare: f32) -> vec4f {
    let looks = u32(fanTable.fans[fanLookRow + 1u].y);
    if (looks == 0u) {
        return vec4f(0.0);
    }

    let look = min(u32(lookShare * f32(looks)), looks - 1u);
    let place = fanTable.fans[fanLookRow + 2u * look];
    let layer = fanTable.fans[fanLookRow + 2u * look + 1u].x;
    let first = run & 0xffffu;
    let count = run >> 16u;
    for (var fan = 0u; fan < count; fan++) {
        let at = fanTable.fans[2u * (first + fan)];
        // Over the radius each way, so the circle is one across in metres whatever the sheet's aspect.
        let off = (local - at.xy) / at.zw;
        let outSq = dot(off, off);
        if (outSq >= 1.0) {
            continue;
        }

        let fromHub = sqrt(outSq);
        let way = off / max(fromHub, 1e-6);
        let rim = fanTable.fans[2u * (first + fan) + 1u];
        let along = way * rim.zw;
        let a = dot(along, along);
        let b = dot(rim.xy, along);
        let reach = (-b + sqrt(max(0.0, b * b - a * (dot(rim.xy, rim.xy) - 1.0)))) / a;
        let share = fromHub / reach;
        if (share >= 1.0) {
            continue;
        }

        let turnedRad = fract(turns + f32(fan) * 0.618034) * 6.2831853;
        let c = cos(turnedRad);
        let s = sin(turnedRad);
        let turned = vec2f(c * way.x + s * way.y, c * way.y - s * way.x);
        return vec4f(place.xy + (vec2f(0.5) + turned * (share * 0.5)) * place.zw, layer, 1.0);
    }

    return vec4f(0.0);
}

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
    // Where the corner stands in the patches' field, in patches across, how its surface takes them (patchLook,
    // patchTint), and the field's mean and one over its spread.
    @location(3) patchAt: vec2f,
    @location(4) @interpolate(flat) patchLook: vec4f,
    @location(5) @interpolate(flat) patchField: vec2f,
    // How much of the ground laid before it the corner covers: one but where a surface fades out over it.
    @location(6) cover: f32,
    // The corner's place, which a fade's edge is ragged by; the hexes its looks are scattered in, as hexes a period, and
    // their sharpness, then the ragging's lumps a metre and how far they move the cover; and how far each look has
    // drifted, in periods.
    @location(7) positionM: vec2f,
    @location(8) @interpolate(flat) scatter: vec4f,
    @location(9) @interpolate(flat) drift: vec4f,
};

// The ground's patches, ground_patches.glsl said again: GroundPatches' FieldMean and FieldSpread are this field's.
fn patchHash(cell: vec2i, salt: u32) -> u32 {
    let u = vec2u(cell);
    var h = (u.x * 0x8da6b343u) ^ (u.y * 0xd8163841u) ^ (salt * 0x9e3779b9u);
    h ^= h >> 16u;
    h *= 0x7feb352du;
    h ^= h >> 15u;
    h *= 0x846ca68bu;
    h ^= h >> 16u;
    return h;
}

fn patchLattice(cell: vec2i, salt: u32) -> f32 {
    return f32(patchHash(cell, salt) >> 8u) / 16777215.0;
}

fn patchValue(at: vec2f, salt: u32) -> f32 {
    let floored = floor(at);
    let cell = vec2i(floored);
    let f = at - floored;
    let u = f * f * f * (f * (f * 6.0 - 15.0) + 10.0);
    let bottom = mix(patchLattice(cell, salt), patchLattice(cell + vec2i(1, 0), salt), u.x);
    let top = mix(patchLattice(cell + vec2i(0, 1), salt), patchLattice(cell + vec2i(1, 1), salt), u.x);
    return mix(bottom, top, u.y);
}

fn patchField(start: vec2f, salt: u32) -> f32 {
    let turn = mat2x2f(0.8, 0.6, -0.6, 0.8);
    var at = start;
    var sum = 0.0;
    var amplitude = 0.5;
    var total = 0.0;
    for (var octave = 0; octave < 4; octave++) {
        sum += amplitude * patchValue(at, salt);
        total += amplitude;
        at = 2.0 * (turn * at) + vec2f(17.0, 31.0);
        amplitude *= 0.5;
    }

    return sum / total;
}

fn patchLumps(at: vec2f, field: vec2f, salt: u32) -> f32 {
    return (patchField(at, salt) - field.x) * field.y;
}

fn patchLook(lumps: f32, look: vec4f) -> f32 {
    return smoothstep(look.z, look.w, lumps);
}

fn patchTint(lumps: f32, look: vec4f) -> vec3f {
    return (1.0 + look.x * lumps) * vec3f(1.0 + look.y * lumps, 1.0, 1.0 - look.y * lumps);
}

// The three points of the lattice of hexes a place lies between, and how much each has of it (scatterCorners).
struct ScatterCorners {
    corners: array<vec2i, 3>,
    shares: vec3f,
};

fn scatterCorners(at: vec2f, sharpness: f32) -> ScatterCorners {
    let skewed = vec2f(at.x - 0.57735027 * at.y, 1.15470054 * at.y);
    let base = vec2i(floor(skewed));
    let f = fract(skewed);
    let rest = 1.0 - f.x - f.y;
    let upper = select(0, 1, rest < 0.0);
    let s = f32(upper);
    let flip = 2.0 * s - 1.0;
    var result: ScatterCorners;
    result.corners = array<vec2i, 3>(
        base + vec2i(upper, upper), base + vec2i(upper, 1 - upper), base + vec2i(1 - upper, upper));
    let near = vec3f(-rest * flip, s - f.y * flip, s - f.x * flip);
    let sharpened = pow(max(near, vec3f(0.0)), vec3f(sharpness));
    result.shares = sharpened / (sharpened.x + sharpened.y + sharpened.z);
    return result;
}

fn scatterFrom(corner: vec2i, salt: u32) -> vec2f {
    let h = patchHash(corner, salt);
    return vec2f(f32(h & 0xffffu), f32(h >> 16u)) / 65536.0;
}

// How a shade is filed, said again from GroundVertex: three channels of nine bits up to the brightest
// tint, and the surface in the five above them, paint filed as the last of those.
const BRIGHTEST_TINT: f32 = 3.0;
const CHANNEL_STEPS: u32 = 511u;
const PAINT_FILED: u32 = 31u;

// The track's strip files where on it a corner stands in place of a tint, said again from GroundVertex.OnTheTrack:
// pictures along in eighteen bits of 256 steps each, and across in the nine above them.
const TRACK: u32 = 5u;

// The shore, filed after the track's strip, and the row of the patches its figures stand in (GroundPatches.ShoreRow).
const SHORE: u32 = 6u;
const SHORE_ROW: u32 = 5u;
// The kerbs' strips, filed as the track's is, and the shadow a kerb casts (ground.frag's SHADOW).
const KERB: u32 = 7u;
const EDGING: u32 = 8u;
const SHADOW: u32 = 9u;
// The walk, whose patches a kerb wears.
const PAVEMENT: u32 = 2u;
// A surface fading out over the ground before it files its number with this bit, and how much it covers in place of
// its tint (GroundVertex.Fading).
const FADING_FILED: u32 = 16u;
const TRACK_STEPS_PER_PICTURE: f32 = 256.0;
const ALONG_STEPS: u32 = 262143u;
const ACROSS_STEPS: f32 = 511.0;

/// A corner carries its place and one word of shade and surface; its texture coordinate is its place over
/// its surface's period, which anchors every surface to the world origin.
@vertex
fn groundVertex(
    @location(0) positionM: vec2f,
    @location(1) shade: u32,
) -> GroundOut {
    var result: GroundOut;
    result.position = toClip(positionM);

    var filed = shade >> 27u;
    result.patchAt = vec2f(0.0);
    result.patchField = camera.patchField.xy;
    result.patchLook = vec4f(0.0);
    result.cover = 1.0;
    result.positionM = positionM;
    result.scatter = vec4f(0.0);
    result.drift = vec4f(0.0);
    if (filed == TRACK || filed == KERB || filed == EDGING) {
        let strip = vec2f(f32(shade & ALONG_STEPS) / TRACK_STEPS_PER_PICTURE, f32((shade >> 18u) & CHANNEL_STEPS) / ACROSS_STEPS);
        // A kerb's picture has its low side at the bottom: the town's kerb drops to the road on the right of its line,
        // the walk's to the grass on the left.
        result.uv = select(strip, vec2f(strip.x, 1.0 - strip.y), filed == EDGING);
        result.tint = vec3f(1.0);
        result.surface = filed;
        // A kerb weathers as the walk it bounds does, paler and warmer in patches, on a field of its own.
        if (filed != TRACK) {
            result.patchAt = positionM * camera.patchPerAcross[PAVEMENT];
            result.patchLook = camera.patchLook[PAVEMENT];
        }
        return result;
    }

    result.tint = vec3f(vec3u(shade, shade >> 9u, shade >> 18u) & vec3u(CHANNEL_STEPS)) * (BRIGHTEST_TINT / f32(CHANNEL_STEPS));
    if (filed != PAINT_FILED && (filed & FADING_FILED) != 0u) {
        filed &= ~FADING_FILED;
        result.cover = result.tint.x / BRIGHTEST_TINT;
        result.tint = vec3f(1.0);
    }

    if (filed <= 4u) {
        result.patchLook = camera.patchLook[filed];
    } else if (filed == SHORE) {
        result.patchLook = camera.patchLook[SHORE_ROW];
    }

    var period = 1.0;
    var perAcross = 0.0;
    if (filed < 4u) {
        period = camera.surfacePeriodsM[filed];
        perAcross = camera.patchPerAcross[filed];
    } else if (filed == 4u) {
        period = camera.waterPeriodM.x;
        perAcross = camera.patchPerAcrossWet.x;
        result.drift = camera.waterDriftM / period;
    } else if (filed == SHORE) {
        period = camera.waterPeriodM.y;
        perAcross = camera.patchPerAcrossWet.y;
    }

    result.uv = positionM / period;
    result.surface = select(filed, PAINT, filed == PAINT_FILED);
    result.patchAt = positionM * perAcross;
    result.scatter = vec4f(period * camera.patchScatter.x, camera.patchScatter.y, camera.patchRagged.xy);
    return result;
}

// Not a surface: the tint alone, for a mark that is not ground.
const PAINT: u32 = 255u;

// The gradients are handed in rather than taken here, and that is not a style: WGSL will not let a
// texture be sampled with implicit derivatives under control flow that may differ between the pixels
// of a quad, and which surface a triangle wears is exactly such a value. Taken once above the branch,
// where every pixel agrees, they are the same two vectors the implicit sample would have used — so the
// mip level is the one the ground has always been drawn at.
// A surface is its looks, a layer a look (GroundPatches).
fn surfaceColour(which: u32, uv: vec2f, look: u32, ddx: vec2f, ddy: vec2f) -> vec3f {
    switch which {
        case 1u: { return textureSampleGrad(tarmac, repeated, uv, look, ddx, ddy).rgb; }
        case 2u: { return textureSampleGrad(pavement, repeated, uv, look, ddx, ddy).rgb; }
        case 3u: { return textureSampleGrad(deck, repeated, uv, look, ddx, ddy).rgb; }
        case 4u: { return textureSampleGrad(water, repeated, uv, look, ddx, ddy).rgb; }
        case 6u: { return textureSampleGrad(shore, repeated, uv, look, ddx, ddy).rgb; }
        default: { return textureSampleGrad(grass, repeated, uv, look, ddx, ddy).rgb; }
    }
}

// Where each surface's hashes start past its own number, said again from ground.frag, and the least share a hex reads at.
const RAGGED_SALT: u32 = 8u;
const SCATTER_SALT: u32 = 16u;
const LEAST_SHARE: f32 = 1.0 / 256.0;
const WHOLE_COVER: f32 = 0.999;

// One look at `uv`, as the hexes there read it (scatterCorners).
fn scattered(which: u32, uv: vec2f, look: u32, scatter: vec4f, salt: u32, ddx: vec2f, ddy: vec2f) -> vec3f {
    // A var, since only a reference is indexed at run time.
    var hexes = scatterCorners(uv * scatter.x, scatter.y);
    var sum = vec3f(0.0);
    var read = 0.0;
    for (var c = 0; c < 3; c++) {
        let share = hexes.shares[c];
        if (share < LEAST_SHARE) {
            continue;
        }
        sum += share * surfaceColour(which, uv + scatterFrom(hexes.corners[c], salt), look, ddx, ddy);
        read += share;
    }

    return sum / read;
}

// A kerb's strip, held half a texel inside its long edges (ground.frag's kerbstone).
fn kerbstone(frag: GroundOut, ddx: vec2f, ddy: vec2f) -> vec3f {
    if (frag.surface == KERB) {
        let half = 0.5 / f32(textureDimensions(kerb).y);
        return textureSampleGrad(kerb, repeated, vec2f(frag.uv.x, clamp(frag.uv.y, half, 1.0 - half)), ddx, ddy).rgb;
    }

    let half = 0.5 / f32(textureDimensions(edging).y);
    return textureSampleGrad(edging, repeated, vec2f(frag.uv.x, clamp(frag.uv.y, half, 1.0 - half)), ddx, ddy).rgb;
}

// A fade's cover, moved by lumps of its own at its middle and not at its ends (ground.frag's ragged).
fn ragged(frag: GroundOut) -> f32 {
    let cover = frag.cover;
    let lumps = patchLumps(frag.positionM * frag.scatter.z, frag.patchField, RAGGED_SALT + frag.surface);
    return clamp(cover + frag.scatter.w * lumps * 4.0 * cover * (1.0 - cover), 0.0, 1.0);
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

    if (frag.surface == KERB || frag.surface == EDGING) {
        let kerbLumps = patchLumps(frag.patchAt, frag.patchField, frag.surface);
        return vec4f(kerbstone(frag, ddx, ddy) * patchTint(kerbLumps, frag.patchLook), 1.0);
    }

    if (frag.surface == SHADOW) {
        return vec4f(0.0, 0.0, 0.0, frag.cover);
    }

    // The track's strip: what its picture leaves clear past the ballast is not drawn, the ground laid before it showing.
    if (frag.surface == TRACK) {
        let strip = textureSampleGrad(track, repeated, frag.uv, ddx, ddy);
        if (strip.a < 0.5) {
            discard;
        }
        return vec4f(strip.rgb, 1.0);
    }

    // A look the patches give no share of a place is not read there.
    let lumps = patchLumps(frag.patchAt, frag.patchField, frag.surface);
    let second = patchLook(lumps, frag.patchLook);
    var ground = vec3f(0.0);
    if (second < 1.0) {
        ground += (1.0 - second) * scattered(
            frag.surface, frag.uv - frag.drift.xy, 0u, frag.scatter, SCATTER_SALT + 2u * frag.surface, ddx, ddy);
    }
    if (second > 0.0) {
        ground += second * scattered(
            frag.surface, frag.uv - frag.drift.zw, 1u, frag.scatter, SCATTER_SALT + 2u * frag.surface + 1u, ddx, ddy);
    }
    // Blended, and opaque everywhere but where a surface fades out over the ground before it (GroundVertex.Fading).
    var cover = 1.0;
    if (frag.cover < WHOLE_COVER) {
        cover = ragged(frag);
    }
    return vec4f(ground * patchTint(lumps, frag.patchLook) * frag.tint, cover);
}

struct SpriteOut {
    @builtin(position) position: vec4f,
    @location(0) uv: vec3f,
    @location(1) tint: vec4f,
    // One where the sheet is light, summed onto the picture rather than laid over it (SheetSource.AddsLight).
    @location(2) @interpolate(flat) light: f32,
    // The sheet's run of fans, where on the sheet this point is, how far this body's fans have turned and which look they
    // wear: what the fragment stage lays a fan's look over the body's own picture with.
    @location(3) @interpolate(flat) fans: u32,
    @location(4) local: vec2f,
    @location(6) @interpolate(flat) turns: f32,
    @location(7) @interpolate(flat) look: f32,
};

@vertex
fn spriteVertex(
    @builtin(vertex_index) vertex: u32,
    // SpriteInstance, packed as the desktop reads it: the half-size two halves, the cell four bytes, the tint four
    // normalised bytes, the sheet and the ID two half-words.
    @location(0) centreM: vec2f,
    @location(1) halfSizeM: vec2f,
    @location(2) headingRad: f32,
    @location(3) cell: vec4u,
    @location(4) tint: vec4f,
    @location(5) sheetId: vec2u,
) -> SpriteOut {
    let sheet = sheetId.x;
    let at = corner(vertex);
    let fromCentreM = (at * 2.0 - 1.0) * halfSizeM;

    // Upright is a rotation of nothing, so the walkers pay one sine and one cosine and no branch.
    let along = vec2f(cos(headingRad), sin(headingRad));
    let across = vec2f(-along.y, along.x);

    let place = sheets.place[sheet];
    let uv = (vec2f(cell.xy) + at) / vec2f(cell.zw);

    var result: SpriteOut;
    result.position = toClip(centreM + along * fromCentreM.x + across * fromCentreM.y);
    result.uv = vec3f(place.originScale.xy + uv * place.originScale.zw, place.layerSize.x);
    result.tint = tint;
    result.light = place.layerSize.w;
    result.fans = fansRun(sheet, 2.0 * halfSizeM.x * f32(cell.z), camera.turning.y);
    result.local = uv;
    result.turns = fract(camera.turning.x + fansShare(sheetId.y));
    result.look = fansShare(sheetId.y ^ fanLookSalt);
    return result;
}

@fragment
fn spriteFragment(frag: SpriteOut) -> @location(0) vec4f {
    // The pages carry no mip chain, so the atlas is sampled at the top level.
    var texel = textureSampleLevel(pages, clamped, frag.uv.xy, i32(frag.uv.z), 0.0);

    // A picture that draws fans has its look laid over each one, turned (OBJ-6, TRN-13).
    if (frag.fans != 0u) {
        let lookUv = fansLook(frag.local, frag.fans, frag.turns, frag.look);
        if (lookUv.w > 0.0) {
            let fan = textureSampleLevel(pages, clamped, lookUv.xy, i32(lookUv.z), 0.0);
            texel = vec4f(mix(texel.rgb, fan.rgb, fan.a), fan.a + texel.a * (1.0 - fan.a));
        }
    }

    // The art is keyed, so most of every cell is nothing at all; discarding it keeps the blend from
    // laying a rectangle of near-zero alpha over whatever the sprite is standing on.
    if (texel.a < 0.004) {
        discard;
    }

    // Premultiplied, blended one over one-minus-src-alpha (town.js): laid over at its alpha, or — a sheet of light,
    // its coverage nought — summed onto what is under it.
    let alpha = texel.a * frag.tint.a;
    return vec4f(texel.rgb * frag.tint.rgb * alpha, alpha * (1.0 - frag.light));
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
