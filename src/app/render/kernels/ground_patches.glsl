// The ground's patches (GroundPatches): a field over the town's plane, a couple of dozen metres to a lump, that decides
// which of its two looks every surface wears and tints it — a lawn worn, drier and paler in one place and kept in the
// next. GroundPatches' FieldMean and FieldSpread are this field's, measured off it: a change to it is a change to them.
// `salt` makes each surface's field a field of its own, with the same statistics. town.wgsl says it again.

uint patchHash(ivec2 cell, uint salt) {
    uvec2 u = uvec2(cell);
    uint h = (u.x * 0x8da6b343u) ^ (u.y * 0xd8163841u) ^ (salt * 0x9e3779b9u);
    h ^= h >> 16;
    h *= 0x7feb352du;
    h ^= h >> 15;
    h *= 0x846ca68bu;
    h ^= h >> 16;
    return h;
}

float patchLattice(ivec2 cell, uint salt) {
    return float(patchHash(cell, salt) >> 8) / 16777215.0;
}

// The lattice's values between its corners, eased by the quintic so neither the field nor its slope shows a cell's edge.
float patchValue(vec2 at, uint salt) {
    vec2 floored = floor(at);
    ivec2 cell = ivec2(floored);
    vec2 f = at - floored;
    vec2 u = f * f * f * (f * (f * 6.0 - 15.0) + 10.0);
    float bottom = mix(patchLattice(cell, salt), patchLattice(cell + ivec2(1, 0), salt), u.x);
    float top = mix(patchLattice(cell + ivec2(0, 1), salt), patchLattice(cell + ivec2(1, 1), salt), u.x);
    return mix(bottom, top, u.y);
}

// Four octaves, each half as wide as the last, turned a 3-4-5 triangle's angle and moved off it so no two share a lattice
// line: a patch with ragged edges rather than a blot.
float patchField(vec2 at, uint salt) {
    const mat2 turn = mat2(0.8, 0.6, -0.6, 0.8);
    float sum = 0.0;
    float amplitude = 0.5;
    float total = 0.0;
    for (int octave = 0; octave < 4; octave++) {
        sum += amplitude * patchValue(at, salt);
        total += amplitude;
        at = 2.0 * (turn * at) + vec2(17.0, 31.0);
        amplitude *= 0.5;
    }

    return sum / total;
}

// The field at `at`, in patches across, in standard deviations from its mean `field.x`, one over its spread `field.y`.
float patchLumps(vec2 at, vec2 field, uint salt) {
    return (patchField(at, salt) - field.x) * field.y;
}

// How much of the surface's second look shows where the field stands at `lumps`: none below `look.z`, all of it past
// `look.w`.
float patchLook(float lumps, vec4 look) {
    return smoothstep(look.z, look.w, lumps);
}

// What the surface is multiplied by there: its light and its warmth (red up, blue down) moved by `look.x` and `look.y`
// a standard deviation.
vec3 patchTint(float lumps, vec4 look) {
    return (1.0 + look.x * lumps) * vec3(1.0 + look.y * lumps, 1.0, 1.0 - look.y * lumps);
}

// The scatter (GroundPatches.Scatter): the plane cut into hexes about the points of a lattice of equilateral triangles
// one across, each hex reading its look from a place of its own. `at` is in hexes across; `corners` are the three points
// of the triangle it lies in and `shares` how much each has of it, summed to one and raised by `sharpness` so a hex is
// its own drawing over most of its width and hands over near its edge.
void scatterCorners(vec2 at, float sharpness, out ivec2 corners[3], out vec3 shares) {
    // Skewed onto the lattice's own axes: (1, 0) and (1/2, √3/2) are one step each.
    vec2 skewed = vec2(at.x - 0.57735027 * at.y, 1.15470054 * at.y);
    ivec2 base = ivec2(floor(skewed));
    vec2 f = fract(skewed);
    float rest = 1.0 - f.x - f.y;
    int upper = rest < 0.0 ? 1 : 0;
    float s = float(upper);
    float flip = 2.0 * s - 1.0;
    corners[0] = base + ivec2(upper, upper);
    corners[1] = base + ivec2(upper, 1 - upper);
    corners[2] = base + ivec2(1 - upper, upper);
    vec3 near = vec3(-rest * flip, s - f.y * flip, s - f.x * flip);
    vec3 sharpened = pow(max(near, vec3(0.0)), vec3(sharpness));
    shares = sharpened / (sharpened.x + sharpened.y + sharpened.z);
}

// Where in its look's repeat a hex reads from, in periods: anywhere, so no two hexes stand in step.
vec2 scatterFrom(ivec2 corner, uint salt) {
    uint h = patchHash(corner, salt);
    return vec2(float(h & 0xffffu), float(h >> 16)) / 65536.0;
}
