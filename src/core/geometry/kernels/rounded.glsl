// A quotient and a square root as the host's float arithmetic gives them — the float nearest the exact answer — on a
// device Vulkan and WebGPU let round both its own way (a quotient to 2.5 ULP, sqrt to inversesqrt's error). RADV's own
// misses the nearest on 28 % of quotients and 15 % of roots. So every quotient and root a kernel shares with the host is
// taken here, and a kernel is the host's to the bit on every driver rather than on one that happens to round them.
//
// The device's own answer is moved a float at a time until it is the nearest. Which side of the midpoint between two
// neighbours the exact answer lies is asked of the significands in integers — a midpoint's 25 bits times a float's 24, or
// squared, held whole in two words — so it is exact on every machine and asks for no doubles. The exact answer never lies
// on a midpoint, so there is no tie to break. Included by whatever needs it, as often as it likes.

#ifndef ROUNDED_GLSL
#define ROUNDED_GLSL

#include "core/geometry/kernels/floats.glsl"
#include "core/geometry/kernels/wide.glsl"

// How far off the device's own answer is looked for, in floats either way: past the bound either API allows.
const int RoundedMostSteps = 4;

// The float next above or below a positive one.
float roundedAbove(float x) { return uintBitsToFloat(floatBitsToUint(x) + 1u); }
float roundedBelow(float x) { return uintBitsToFloat(floatBitsToUint(x) - 1u); }

// A positive normal float as significand · 2^(exponent − 150): the significand with its leading bit, the exponent biased.
uint roundedSignificand(float x) { return (floatBitsToUint(x) & 0x7fffffu) | 0x800000u; }
int roundedExponent(float x) { return int((floatBitsToUint(x) >> 23) & 0xffu); }

// Whether `value` · 2^shift lies below (-1) or above (1) the 64-bit (high, low), where the value is a significand. Past
// forty bits of shift it is above anything a midpoint's product reaches, and below nought it is under any.
int roundedSide(uint value, int shift, uint high, uint low)
{
    if (shift < 0) return -1;
    if (shift > 40) return 1;

    uint valueHigh = 0u;
    uint valueLow = value;
    wideShiftLeft(valueHigh, valueLow, uint(shift));
    return wideCompare(valueHigh, valueLow, high, low);
}

// Whether a lies below (-1) or above (1) b times the midpoint between m and the float above it; all three positive normals.
int roundedQuotientSide(float a, float b, float m)
{
    uint high;
    uint low;
    wideProduct(2u * roundedSignificand(m) + 1u, roundedSignificand(b), high, low);
    return roundedSide(roundedSignificand(a), roundedExponent(a) - roundedExponent(m) - roundedExponent(b) + 151, high, low);
}

// Whether x lies below (-1) or above (1) the square of the midpoint between s and the float above it; both positive normals.
int roundedRootSide(float x, float s)
{
    uint twice = 2u * roundedSignificand(s) + 1u;
    uint high;
    uint low;
    wideProduct(twice, twice, high, low);
    return roundedSide(roundedSignificand(x), roundedExponent(x) - 2 * roundedExponent(s) + 152, high, low);
}

bool roundedIsNormal(float x) { int exponent = roundedExponent(x); return exponent != 0 && exponent != 255; }

// a / b, the float nearest the exact quotient.
float roundedQuotient(float a, float b)
{
    precise float q = a / b;
    if (q == 0.0 || !floatIsFinite(q)) return q;

    float dividend = abs(a);
    float divisor = abs(b);
    float m = abs(q);
    if (!roundedIsNormal(dividend) || !roundedIsNormal(divisor) || !roundedIsNormal(m)) return q;

    for (int step = 0; step < RoundedMostSteps; step++)
    {
        float below = roundedBelow(m);
        if (!roundedIsNormal(below) || roundedQuotientSide(dividend, divisor, below) > 0) break;
        m = below;
    }

    for (int step = 0; step < RoundedMostSteps; step++)
    {
        if (roundedQuotientSide(dividend, divisor, m) < 0) break;
        m = roundedAbove(m);
    }

    return (a < 0.0) != (b < 0.0) ? -m : m;
}

// sqrt(x), the float nearest the exact root.
float roundedSqrt(float x)
{
    precise float s = sqrt(x);
    if (!(x > 0.0) || floatIsInfinite(x) || s == 0.0) return s;
    if (!roundedIsNormal(x) || !roundedIsNormal(s)) return s;

    for (int step = 0; step < RoundedMostSteps; step++)
    {
        float below = roundedBelow(s);
        if (!roundedIsNormal(below) || roundedRootSide(x, below) > 0) break;
        s = below;
    }

    for (int step = 0; step < RoundedMostSteps; step++)
    {
        if (roundedRootSide(x, s) < 0) break;
        s = roundedAbove(s);
    }

    return s;
}

// v / s, each of its two quotients the float nearest — as a Vector2 divided by a float divides each.
vec2 roundedQuotient(vec2 v, float s)
{
    return vec2(roundedQuotient(v.x, s), roundedQuotient(v.y, s));
}

// The length of v as the host's Vector2.Length() has it: its two squares summed, each rounded, and the nearest root.
float roundedLength(vec2 v)
{
    precise float squared = v.x * v.x + v.y * v.y;
    return roundedSqrt(squared);
}

#endif
