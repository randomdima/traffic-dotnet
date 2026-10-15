// Trigonometry as the host's world layer answers it (core/geometry/Trig.cs): the same float operations in the same
// order, so a body the device turns is turned to the bit as the managed world turns it. Vulkan lets a driver's own atan
// stand 4096 floats off and bounds sin and cos only absolutely, and RADV's stand a part in a hundred thousand off — which
// an arc of 8 km radius turns into a centimetre of where a car is on its line.
//
// Floats alone: WebGPU has no doubles. `precise` throughout, so no Vulkan driver fuses what the host rounds twice, and
// every quotient rounded.glsl's nearest float, as the host's is; WGSL cannot say `precise`, so a page may stand a float
// off. A subnormal argument or answer is nought here, as in every kernel.
// Included by whatever needs it, as often as it likes.

#ifndef TRIG_GLSL
#define TRIG_GLSL

#include "core/geometry/kernels/floats.glsl"
#include "core/geometry/kernels/rounded.glsl"

const float TrigPi = 3.14159265358979323846;
const float TrigHalfPi = 1.57079632679489661923;
const float TrigQuarterPi = 0.78539816339744830962;

// Trig.TwoOverPi, the parts of π/2, and Trig.ShortWayBelow.
const float TrigTwoOverPi = 0.636619772367581343;
const float TrigHalfPi1 = 1.5703125;
const float TrigHalfPi2 = 4.837512969970703125e-4;
const float TrigHalfPi3 = 7.54978995489188216e-8;
const float TrigShortWayBelow = 4096.0;
const float TrigTinyBelow = 1.0 / 4096.0;

// glibc's __inv_pio4: 4/π's bits, a byte further along each word, for a large argument's reduction (Payne and Hanek).
const uint TrigFourOverPiBits[24] = uint[](
    0xa2u, 0xa2f9u, 0xa2f983u, 0xa2f9836eu, 0xf9836e4eu, 0x836e4e44u, 0x6e4e4415u, 0x4e441529u, 0x441529fcu, 0x1529fc27u,
    0x29fc2757u, 0xfc2757d1u, 0x2757d1f5u, 0x57d1f534u, 0xd1f534ddu, 0xf534ddc0u, 0x34ddc0dbu, 0xddc0db62u, 0xc0db6295u,
    0xdb629599u, 0x6295993cu, 0x95993c43u, 0x993c4390u, 0x3c439041u);
const float TrigHalfPiOver2To62 = 3.40612158e-19;

// Cephes' single-precision series (Trig.Sin0…, Cos0…, Tan0…, Atan0…).
const float TrigSin0 = -1.9515295891e-4;
const float TrigSin1 = 8.3321608736e-3;
const float TrigSin2 = -1.6666654611e-1;
const float TrigCos0 = 2.443315711809948e-5;
const float TrigCos1 = -1.388731625493765e-3;
const float TrigCos2 = 4.166664568298827e-2;
const float TrigTan0 = 9.38540185543e-3;
const float TrigTan1 = 3.11992232697e-3;
const float TrigTan2 = 2.44301354525e-2;
const float TrigTan3 = 5.34112807005e-2;
const float TrigTan4 = 1.33387994085e-1;
const float TrigTan5 = 3.33331568548e-1;
const float TrigTan3PiOver8 = 2.414213562373095;
const float TrigTanPiOver8 = 0.4142135623730950;
const float TrigAtan0 = 8.05374449538e-2;
const float TrigAtan1 = -1.38776856032e-1;
const float TrigAtan2Coefficient = 1.99777106478e-1;
const float TrigAtan3 = -3.33329491539e-1;

// Trig.ReduceLarge: a large magnitude's 62-bit fraction of a quadrant past the nearest whole one, in radians, and the
// quadrant.
float trigReduceLarge(uint bits, out int quadrant)
{
    uint at = (bits >> 26) & 15u;
    uint mantissa = ((bits & 0xffffffu) | 0x800000u) << ((bits >> 23) & 7u);
    uint high = mantissa * TrigFourOverPiBits[at];
    uint middleHigh;
    uint middleLow;
    wideProduct(mantissa, TrigFourOverPiBits[at + 4u], middleHigh, middleLow);
    uint lowestHigh;
    uint lowestLow;
    wideProduct(mantissa, TrigFourOverPiBits[at + 8u], lowestHigh, lowestLow);

    uint low = lowestHigh + middleLow;
    uint carry = low < middleLow ? 1u : 0u;
    high += middleHigh + carry;
    uint turns = (high + 0x20000000u) >> 30;
    high -= turns << 30;
    quadrant = int(turns);

    precise float fraction = float(int(high)) * 4294967296.0 + float(low);
    precise float reduced = fraction * TrigHalfPiOver2To62;
    return reduced;
}

// Trig.Reduce: x less its nearest whole number of quarter turns, which is the quadrant.
float trigReduce(float x, out int quadrant)
{
    float magnitude = abs(x);
    precise float reduced;
    if (magnitude < TrigShortWayBelow)
    {
        precise float scaled = magnitude * TrigTwoOverPi + 0.5;
        int turns = int(scaled);
        float whole = float(turns);
        reduced = magnitude - whole * TrigHalfPi1;
        reduced -= whole * TrigHalfPi2;
        reduced -= whole * TrigHalfPi3;
        quadrant = turns;
    }
    else
    {
        reduced = trigReduceLarge(floatBitsToUint(magnitude), quadrant);
    }

    if (x < 0.0)
    {
        reduced = -reduced;
        quadrant = -quadrant;
    }

    return reduced;
}

// sin and cos of x, each as Trig.SinCos gives it.
void trigSinCos(float x, out float sine, out float cosine)
{
    if (floatIsNaN(x) || floatIsInfinite(x))
    {
        sine = cosine = floatNaN();
        return;
    }

    if (abs(x) < TrigTinyBelow)
    {
        sine = x;
        cosine = 1.0;
        return;
    }

    int quadrant;
    float reduced = trigReduce(x, quadrant);
    precise float squared = reduced * reduced;
    precise float sinSeries = TrigSin0 * squared + TrigSin1;
    sinSeries = sinSeries * squared + TrigSin2;
    precise float sinValue = sinSeries * squared * reduced + reduced;
    precise float cosSeries = TrigCos0 * squared + TrigCos1;
    cosSeries = cosSeries * squared + TrigCos2;
    precise float cosValue = cosSeries * squared * squared - 0.5 * squared + 1.0;

    int turn = quadrant & 3;
    sine = turn == 0 ? sinValue : turn == 1 ? cosValue : turn == 2 ? -sinValue : -cosValue;
    cosine = turn == 0 ? cosValue : turn == 1 ? -sinValue : turn == 2 ? -cosValue : sinValue;
}

float trigSin(float x)
{
    float sine;
    float cosine;
    trigSinCos(x, sine, cosine);
    return sine;
}

// The unit vector a heading points along: (cos, sin).
vec2 trigUnit(float headingRad)
{
    float sine;
    float cosine;
    trigSinCos(headingRad, sine, cosine);
    return vec2(cosine, sine);
}

// Trig.AtanOfMagnitude.
float trigAtanOfMagnitude(float magnitude)
{
    precise float folded;
    float offset;
    if (magnitude > TrigTan3PiOver8)
    {
        folded = roundedQuotient(-1.0, magnitude);
        offset = TrigHalfPi;
    }
    else if (magnitude > TrigTanPiOver8)
    {
        precise float less = magnitude - 1.0;
        precise float more = magnitude + 1.0;
        folded = roundedQuotient(less, more);
        offset = TrigQuarterPi;
    }
    else
    {
        folded = magnitude;
        offset = 0.0;
    }

    precise float squared = folded * folded;
    precise float series = TrigAtan0 * squared + TrigAtan1;
    series = series * squared + TrigAtan2Coefficient;
    series = series * squared + TrigAtan3;
    precise float rad = series * squared * folded + folded;
    precise float whole = offset + rad;
    return whole;
}

// atan(x), as Trig.Atan.
float trigAtan(float x)
{
    if (floatIsNaN(x)) return floatNaN();

    float rad = trigAtanOfMagnitude(abs(x));
    return x < 0.0 ? -rad : rad;
}

// atan2(y, x), as Trig.Atan2, quadrant by quadrant and signed zero by signed zero.
float trigAtan2(float y, float x)
{
    if (floatIsNaN(x) || floatIsNaN(y)) return floatNaN();

    bool below = (floatBitsToUint(y) >> 31) != 0u;
    bool behind = (floatBitsToUint(x) >> 31) != 0u;
    if (x == 0.0)
    {
        if (y == 0.0) return behind ? (below ? -TrigPi : TrigPi) : y;
        return below ? -TrigHalfPi : TrigHalfPi;
    }

    if (floatIsInfinite(x))
    {
        float far = floatIsInfinite(y) ? (behind ? 3.0 * TrigQuarterPi : TrigQuarterPi) : (behind ? TrigPi : 0.0);
        return below ? -far : far;
    }

    if (floatIsInfinite(y)) return below ? -TrigHalfPi : TrigHalfPi;
    if (y == 0.0) return behind ? (below ? -TrigPi : TrigPi) : y;

    float z = trigAtanOfMagnitude(roundedQuotient(abs(y), abs(x)));
    precise float rad = behind ? TrigPi - z : z;
    return below ? -rad : rad;
}

// tan(x), as Trig.Tan: past the short way, the sine over the cosine.
float trigTan(float x)
{
    if (floatIsNaN(x) || floatIsInfinite(x)) return floatNaN();
    if (abs(x) < TrigTinyBelow) return x;

    if (abs(x) >= TrigShortWayBelow)
    {
        float sine;
        float cosine;
        trigSinCos(x, sine, cosine);
        return roundedQuotient(sine, cosine);
    }

    int quadrant;
    float reduced = trigReduce(x, quadrant);
    precise float squared = reduced * reduced;
    precise float series = TrigTan0 * squared + TrigTan1;
    series = series * squared + TrigTan2;
    series = series * squared + TrigTan3;
    series = series * squared + TrigTan4;
    series = series * squared + TrigTan5;
    precise float tangent = series * squared * reduced + reduced;
    return (quadrant & 1) != 0 ? roundedQuotient(-1.0, tangent) : tangent;
}

#endif
