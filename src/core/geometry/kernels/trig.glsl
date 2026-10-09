// Trigonometry as the host answers it. Vulkan lets a driver's own atan stand 4096 floats off and leaves sin and cos to an
// absolute bound, and RADV's stand a part in a hundred thousand off — which an arc of 8 km radius turns into a centimetre
// of where a car is on its line, and a chord of 500 m into half a millimetre of where its lead point is.
//
// Sine and cosine are the host's to the bit: MathF.Sin, Cos and SinCos are glibc's sinf, cosf and sincosf, which on
// x86-64 with FMA run __sincosf_fma (sysdeps/ieee754/flt-32/s_sincosf.h, built with -mfma) — reduced and summed in
// doubles, every sum fused with the product before it as that build fuses it, and rounded to a float once. A last bit of
// a heading apart is a contact's depth apart after a push, and a body frozen a step apart (SOL-37). The rest — tan, atan,
// atan2 — are Cephes' minimax polynomials, within a float or two, `precise` so no driver fuses them. The including kernel
// gets 64-bit floats with it. Included by whatever needs it, as often as it likes.

#ifndef TRIG_GLSL
#define TRIG_GLSL

const float TrigPi = 3.14159265358979323846;
const float TrigHalfPi = 1.57079632679489661923;
const float TrigQuarterPi = 0.78539816339744830962;

// glibc's sincos_t, to the bit: 2/π scaled by 2^24 for its rounding of the quadrant, π/2, the cosine's coefficients and
// the sine's. The second half-turn's table is the first with the cosine's negated.
const double TrigHalfPiInverseScaled = 10680707.430881744LF;
const double TrigHalfPiD = 1.5707963267948966LF;
const double TrigCos1 = -0.49999999725108224LF;
const double TrigCos2 = 0.041666623324344516LF;
const double TrigCos3 = -0.001388676379437604LF;
const double TrigCos4 = 2.4390450703564542e-05LF;
const double TrigSin1 = -0.16666654943701084LF;
const double TrigSin2 = 0.008332178146138854LF;
const double TrigSin3 = -0.00019517298981385725LF;

// π/2 × 2^-62: what a large argument's 62-bit fraction of a quadrant is worth.
const double TrigHalfPiOver2To62 = 3.4061215800865545e-19LF;

// glibc's __inv_pio4: 4/π's bits, a byte further along each word, for a large argument's reduction (Payne and Hanek).
const uint TrigFourOverPiBits[24] = uint[](
    0xa2u, 0xa2f9u, 0xa2f983u, 0xa2f9836eu, 0xf9836e4eu, 0x836e4e44u, 0x6e4e4415u, 0x4e441529u, 0x441529fcu, 0x1529fc27u,
    0x29fc2757u, 0xfc2757d1u, 0x2757d1f5u, 0x57d1f534u, 0xd1f534ddu, 0xf534ddc0u, 0x34ddc0dbu, 0xddc0db62u, 0xc0db6295u,
    0xdb629599u, 0x6295993cu, 0x95993c43u, 0x993c4390u, 0x3c439041u);

// glibc's exponent fields (abstop12) below which an argument is tiny, inside π/4, or reduced the short way (under 120).
const uint TrigTinyTop = 0x398u;
const uint TrigQuarterPiTop = 0x3f4u;
const uint TrigShortWayTop = 0x42fu;
const uint TrigFiniteTop = 0x7f8u;

// A large argument's reduction (reduce_large): its offset into its quadrant, and the quadrant — the 64-bit products
// taken in 32-bit halves, so no kernel needs 64-bit integers.
double trigReduceLarge(uint bits, out int quadrant)
{
    uint at = (bits >> 26) & 15u;
    uint mantissa = ((bits & 0xffffffu) | 0x800000u) << ((bits >> 23) & 7u);
    uint high = mantissa * TrigFourOverPiBits[at];
    uint middleHigh;
    uint middleLow;
    umulExtended(mantissa, TrigFourOverPiBits[at + 4u], middleHigh, middleLow);
    uint lowHigh;
    uint lowLow;
    umulExtended(mantissa, TrigFourOverPiBits[at + 8u], lowHigh, lowLow);

    uint carry;
    uint low = uaddCarry(lowHigh, middleLow, carry);
    high += middleHigh + carry;
    uint turns = (high + 0x20000000u) >> 30;
    high -= turns << 30;
    quadrant = int(turns);

    precise double fraction = double(int(high)) * 4294967296.0LF + double(low);
    precise double reduced = fraction * TrigHalfPiOver2To62;
    return reduced;
}

// sin and cos of x, each the float glibc's sincosf gives on its FMA path, for every float.
void trigSinCos(float x, out float sine, out float cosine)
{
    uint bits = floatBitsToUint(x);
    uint top = (bits >> 20) & 0x7ffu;
    if (top < TrigTinyTop)
    {
        sine = x;
        cosine = 1.0;
        return;
    }

    // glibc's x - x, which a driver that keeps no NaN folds to nought.
    if (top >= TrigFiniteTop)
    {
        sine = cosine = uintBitsToFloat(0x7fc00000u);
        return;
    }

    precise double reduced = double(x);
    int quadrant = 0;
    int signQuadrant = 0;
    if (top >= TrigShortWayTop)
    {
        reduced = trigReduceLarge(bits, quadrant);
        signQuadrant = quadrant + int(bits >> 31);
    }
    else if (top >= TrigQuarterPiTop)
    {
        precise double scaled = double(x) * TrigHalfPiInverseScaled;
        quadrant = (int(scaled) + 0x800000) >> 24;
        reduced = fma(-double(quadrant), TrigHalfPiD, double(x));
        signQuadrant = quadrant;
    }

    double sign = (signQuadrant & 3) == 1 || (signQuadrant & 3) == 2 ? -1.0LF : 1.0LF;
    double cosSign = (signQuadrant & 2) != 0 ? -1.0LF : 1.0LF;
    precise double along = reduced * sign;
    precise double squared = reduced * reduced;

    precise double sinTail = fma(squared, TrigSin3, TrigSin2);
    precise double cosTail = fma(squared, cosSign * TrigCos4, cosSign * TrigCos3);
    precise double cubed = squared * along;
    precise double fourth = squared * squared;
    precise double cosHead = fma(squared, cosSign * TrigCos1, cosSign);
    precise double fifth = squared * cubed;
    precise double sixth = squared * fourth;
    precise double sinSum = fma(cubed, TrigSin1, along);
    precise double cosSum = fma(fourth, cosSign * TrigCos2, cosHead);
    precise double sinValue = fma(sinTail, fifth, sinSum);
    precise double cosValue = fma(cosTail, sixth, cosSum);

    bool swapped = (quadrant & 1) != 0;
    sine = float(swapped ? cosValue : sinValue);
    cosine = float(swapped ? sinValue : cosValue);
}

float trigSin(float x)
{
    float sine;
    float cosine;
    trigSinCos(x, sine, cosine);
    return sine;
}

float trigTan(float x)
{
    float sine;
    float cosine;
    trigSinCos(x, sine, cosine);
    precise float tangent = sine / cosine;
    return tangent;
}

// The unit vector a heading points along: (cos, sin).
vec2 trigUnit(float headingRad)
{
    float sine;
    float cosine;
    trigSinCos(headingRad, sine, cosine);
    return vec2(cosine, sine);
}

// tan(3π/8) and tan(π/8): where the argument is folded back into [-tan(π/8), tan(π/8)].
const float TrigFoldsPastHalf = 2.414213562373095;
const float TrigFoldsPastQuarter = 0.4142135623730950;

// atan(x), within two floats of the exact answer (Cephes atanf).
float trigAtan(float x)
{
    float magnitude = abs(x);
    float offsetRad = 0.0;
    float folded = magnitude;
    if (magnitude > TrigFoldsPastHalf)
    {
        offsetRad = TrigHalfPi;
        precise float flipped = -1.0 / magnitude;
        folded = flipped;
    }
    else if (magnitude > TrigFoldsPastQuarter)
    {
        offsetRad = TrigQuarterPi;
        precise float turned = (magnitude - 1.0) / (magnitude + 1.0);
        folded = turned;
    }

    precise float z = folded * folded;
    precise float series =
        ((((8.05374449538e-2 * z - 1.38776856032e-1) * z + 1.99777106478e-1) * z - 3.33329491539e-1) * z * folded) + folded;
    precise float rad = offsetRad + series;
    return x < 0.0 ? -rad : rad;
}

// atan2(y, x), quadrant by quadrant and signed zero by signed zero as MathF.Atan2 answers it.
float trigAtan2(float y, float x)
{
    bool below = (floatBitsToUint(y) >> 31) != 0u;
    if (x == 0.0)
    {
        if (y == 0.0) return (floatBitsToUint(x) >> 31) != 0u ? (below ? -TrigPi : TrigPi) : y;
        return below ? -TrigHalfPi : TrigHalfPi;
    }

    precise float ratio = y / x;
    float rad = trigAtan(ratio);
    if (x > 0.0) return rad;

    precise float turned = below ? rad - TrigPi : rad + TrigPi;
    return turned;
}

#endif
