#version 460

// The clouds' shadows (OBJ-8, CloudShadows): the ground under cloud wherever the field stands above its threshold, a
// cloud's thin edge shading less than its thick middle. CloudShadows' FieldMean, FieldSpread and FieldSlope are this
// field's, measured off it: a change to it is a change to them.

layout(location = 0) in vec3 inField;
// Where in the field a shadow begins and where it is deepest, and the light it takes there.
layout(location = 1) flat in vec3 inLook;

layout(location = 0) out vec4 outColour;

uint cloudHash(ivec3 cell) {
    uvec3 u = uvec3(cell);
    uint h = (u.x * 0x8da6b343u) ^ (u.y * 0xd8163841u) ^ (u.z * 0xcb1ab31fu);
    h ^= h >> 16;
    h *= 0x7feb352du;
    h ^= h >> 15;
    h *= 0x846ca68bu;
    h ^= h >> 16;
    return h;
}

float cloudLattice(ivec3 cell) {
    return float(cloudHash(cell) >> 8) / 16777215.0;
}

// The lattice's values between its corners, eased by the quintic so neither the field nor its slope shows a cell's edge.
float cloudValue(vec3 at) {
    vec3 floored = floor(at);
    ivec3 cell = ivec3(floored);
    vec3 f = at - floored;
    vec3 u = f * f * f * (f * (f * 6.0 - 15.0) + 10.0);
    float x00 = mix(cloudLattice(cell), cloudLattice(cell + ivec3(1, 0, 0)), u.x);
    float x10 = mix(cloudLattice(cell + ivec3(0, 1, 0)), cloudLattice(cell + ivec3(1, 1, 0)), u.x);
    float x01 = mix(cloudLattice(cell + ivec3(0, 0, 1)), cloudLattice(cell + ivec3(1, 0, 1)), u.x);
    float x11 = mix(cloudLattice(cell + ivec3(0, 1, 1)), cloudLattice(cell + ivec3(1, 1, 1)), u.x);
    return mix(mix(x00, x10, u.y), mix(x01, x11, u.y), u.z);
}

// Octaves each half as wide as the last, turned a 3-4-5 triangle's angle and moved off it so no two share a lattice
// line; its life's axis halves with them, so a cloud's small lumps change faster than its whole.
float cloudOctaves(vec3 at, int octaves) {
    const mat2 turn = mat2(0.8, 0.6, -0.6, 0.8);
    float sum = 0.0;
    float amplitude = 0.5;
    float total = 0.0;
    for (int octave = 0; octave < octaves; octave++) {
        sum += amplitude * cloudValue(at);
        total += amplitude;
        at = vec3(2.0 * (turn * at.xy) + vec2(17.0, 31.0), at.z * 2.0);
        amplitude *= 0.5;
    }

    return sum / total;
}

// Six octaves, down to a thirty-second of a cloud, read where two coarse ones of their own push the point: what bends
// a cloud's lumps into billows rather than blots.
float cloudField(vec3 at) {
    const float warpInClouds = 0.6;
    vec2 push = vec2(
        cloudOctaves(at + vec3(5.2, 1.3, 0.0), 2),
        cloudOctaves(at + vec3(9.7, 2.8, 0.0), 2)) * 2.0 - 1.0;
    return cloudOctaves(vec3(at.xy + warpInClouds * push, at.z), 6);
}

void main() {
    outColour = vec4(0.0, 0.0, 0.0, inLook.z * smoothstep(inLook.x, inLook.y, cloudField(inField)));
}
