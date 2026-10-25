// NaN and infinity asked of a float's bits and made at run time, for every kernel. Vulkan keeps neither unless a
// kernel asks for it and a driver may fold `isnan` away; WGSL has no `isinf` or `isnan` at all, and refuses a NaN that
// is a constant, or an infinity — so either is the bits of one with the invocation's index masked out, which no compiler
// takes for a constant. And a float as a half, rounded as the host rounds it. Compute kernels only. Included by whatever
// needs it, as often as it likes.

#ifndef FLOATS_GLSL
#define FLOATS_GLSL

bool floatIsNaN(float x) { return (floatBitsToUint(x) & 0x7fffffffu) > 0x7f800000u; }

bool floatIsInfinite(float x) { return (floatBitsToUint(x) & 0x7fffffffu) == 0x7f800000u; }

// Neither NaN nor infinite.
bool floatIsFinite(float x) { return (floatBitsToUint(x) & 0x7f800000u) != 0x7f800000u; }

// The float of these bits, made at run time: naga writes a constant of the greatest float as a decimal past it, which
// WGSL refuses too.
float floatOfBits(uint bits) { return uintBitsToFloat(bits | (gl_LocalInvocationIndex & 0u)); }

float floatNaN() { return floatOfBits(0x7fc00000u); }

float floatInfinity() { return floatOfBits(0x7f800000u); }

float floatGreatest() { return floatOfBits(0x7f7fffffu); }

// A float to the nearest half, ties to even, as the host's (Half) conversion rounds it (Halves) — in integer arithmetic,
// because packHalf2x16's rounding is the driver's to choose. unpackHalf2x16 is exact, and reads it back.
uint floatHalf(float value)
{
    uint bits = floatBitsToUint(value);
    uint sign = (bits >> 16) & 0x8000u;
    uint magnitude = bits & 0x7FFFFFFFu;

    // Past the largest finite half once rounded, or not a number: infinity, or a quiet NaN.
    if (magnitude >= 0x47800000u) return sign | (magnitude > 0x7F800000u ? 0x7E00u : 0x7C00u);

    // Under the least normal half: a subnormal, in units of 2^-24, or nought under half the least of them.
    if (magnitude < 0x38800000u)
    {
        uint exponent = magnitude >> 23;
        if (exponent < 102u) return sign;

        uint mantissa = (magnitude & 0x7FFFFFu) | 0x800000u;
        uint shift = 126u - exponent;
        uint kept = mantissa >> shift;
        uint rest = mantissa & ((1u << shift) - 1u);
        uint halfway = 1u << (shift - 1u);
        if (rest > halfway || (rest == halfway && (kept & 1u) != 0u)) kept += 1u;
        return sign | kept;
    }

    // A normal: the exponent rebased, the mantissa cut to ten bits to the nearest, ties to even; a carry into the
    // exponent is the right answer, up to infinity.
    uint rebased = magnitude - 0x38000000u;
    rebased += 0xFFFu + ((rebased >> 13) & 1u);
    return sign | (rebased >> 13);
}

// A pair as two halves in one word, x in the low sixteen bits (Halves.Pack).
uint floatHalves(vec2 pair) { return floatHalf(pair.x) | (floatHalf(pair.y) << 16); }

#endif
