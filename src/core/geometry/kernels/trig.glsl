// Trigonometry as the host answers it. Vulkan lets a driver's own atan stand 4096 floats off and leaves sin and cos to an
// absolute bound, and RADV's stand a part in a hundred thousand off — which an arc of 8 km radius turns into a centimetre
// of where a car is on its line, and a chord of 500 m into half a millimetre of where its lead point is.
//
// All of it is the host's to the bit, for every float a kernel does not flush (a subnormal argument or answer is nought
// here, as in every kernel). A last bit of a heading apart is a contact's depth apart after a push, and a body frozen a
// step apart (SOL-37).
//
// - Sine and cosine: MathF.Sin, Cos and SinCos are glibc's sinf, cosf and sincosf, which on x86-64 with FMA run
//   __sincosf_fma (sysdeps/ieee754/flt-32/s_sincosf.h, built with -mfma) — reduced and summed in doubles, every sum fused
//   with the product before it as that build fuses it, and rounded to a float once. That code, to the bit.
// - Tangent, arctangent and bearing: MathF.Tan, Atan and Atan2 are glibc's tanf, atanf and atan2f, correctly rounded
//   (CORE-MATH, since 2.41) — the float nearest the exact answer, which any answer a double's last bit from exact
//   rounds to but in the rare tie a double cannot settle. fdlibm's double tan, atan and atan2, rounded to a float.
//
// `precise` throughout, so no driver fuses what the host rounds twice. The including kernel gets 64-bit floats with it.
// Included by whatever needs it, as often as it likes.

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

// NaN and infinity asked of the bits, since a driver that keeps neither folds them away (Vulkan keeps them only where a
// kernel asks for it).
bool trigIsNaN(float x)
{
    return (floatBitsToUint(x) & 0x7fffffffu) > 0x7f800000u;
}

bool trigIsInfinite(float x)
{
    return (floatBitsToUint(x) & 0x7fffffffu) == 0x7f800000u;
}

float trigNaN()
{
    return uintBitsToFloat(0x7fc00000u);
}

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

    if (top >= TrigFiniteTop)
    {
        sine = cosine = trigNaN();
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

// The unit vector a heading points along: (cos, sin).
vec2 trigUnit(float headingRad)
{
    float sine;
    float cosine;
    trigSinCos(headingRad, sine, cosine);
    return vec2(cosine, sine);
}

// A double with its low word cleared: its leading 21 bits, whose products with another such are exact (fdlibm's __LO = 0).
double trigHighWord(double x)
{
    return packDouble2x32(uvec2(0u, unpackDouble2x32(x).y));
}

// fdlibm's atan (s_atan.c): the arctangents of a half, one, one and a half and infinity, each in two parts, and its series.
const double TrigAtanHalfHigh = 0.4636476090008061LF;
const double TrigAtanHalfLow = 2.2698777452961687e-17LF;
const double TrigAtanOneHigh = 0.7853981633974483LF;
const double TrigAtanOneLow = 3.061616997868383e-17LF;
const double TrigAtanOneAndAHalfHigh = 0.982793723247329LF;
const double TrigAtanOneAndAHalfLow = 1.3903311031230998e-17LF;
const double TrigAtanInfinityHigh = 1.5707963267948966LF;
const double TrigAtanInfinityLow = 6.123233995736766e-17LF;
const double TrigAtan0 = 0.3333333333333293LF;
const double TrigAtan1 = -0.19999999999876483LF;
const double TrigAtan2 = 0.14285714272503466LF;
const double TrigAtan3 = -0.11111110405462356LF;
const double TrigAtan4 = 0.09090887133436507LF;
const double TrigAtan5 = -0.0769187620504483LF;
const double TrigAtan6 = 0.06661073137387531LF;
const double TrigAtan7 = -0.058335701337905735LF;
const double TrigAtan8 = 0.049768779946159324LF;
const double TrigAtan9 = -0.036531572744216916LF;
const double TrigAtan10 = 0.016285820115365782LF;

// atan of a double, within a last bit of one: the argument folded about a half, one or one and a half, or inverted.
double trigAtanD(double value)
{
    double magnitude = abs(value);
    if (magnitude >= 7.378697629483821e19LF)
    {
        precise double whole = TrigAtanInfinityHigh + TrigAtanInfinityLow;
        return value < 0.0LF ? -whole : whole;
    }

    if (magnitude < 0.4375LF)
    {
        if (magnitude < 1.862645149230957e-09LF) return value;

        precise double z = value * value;
        precise double w = z * z;
        precise double odd = z * (TrigAtan0 + w * (TrigAtan2 + w * (TrigAtan4 + w * (TrigAtan6 + w * (TrigAtan8 + w * TrigAtan10)))));
        precise double even = w * (TrigAtan1 + w * (TrigAtan3 + w * (TrigAtan5 + w * (TrigAtan7 + w * TrigAtan9))));
        precise double near = value - value * (odd + even);
        return near;
    }

    precise double folded;
    double high;
    double low;
    if (magnitude < 0.6875LF)
    {
        folded = (2.0LF * magnitude - 1.0LF) / (2.0LF + magnitude);
        high = TrigAtanHalfHigh;
        low = TrigAtanHalfLow;
    }
    else if (magnitude < 1.1875LF)
    {
        folded = (magnitude - 1.0LF) / (magnitude + 1.0LF);
        high = TrigAtanOneHigh;
        low = TrigAtanOneLow;
    }
    else if (magnitude < 2.4375LF)
    {
        folded = (magnitude - 1.5LF) / (1.0LF + 1.5LF * magnitude);
        high = TrigAtanOneAndAHalfHigh;
        low = TrigAtanOneAndAHalfLow;
    }
    else
    {
        folded = -1.0LF / magnitude;
        high = TrigAtanInfinityHigh;
        low = TrigAtanInfinityLow;
    }

    precise double z = folded * folded;
    precise double w = z * z;
    precise double odd = z * (TrigAtan0 + w * (TrigAtan2 + w * (TrigAtan4 + w * (TrigAtan6 + w * (TrigAtan8 + w * TrigAtan10)))));
    precise double even = w * (TrigAtan1 + w * (TrigAtan3 + w * (TrigAtan5 + w * (TrigAtan7 + w * TrigAtan9))));
    precise double rad = high - ((folded * (odd + even) - low) - folded);
    return value < 0.0LF ? -rad : rad;
}

// atan(x), the float nearest the exact answer, as glibc's atanf (correctly rounded, CORE-MATH) has it.
float trigAtan(float x)
{
    if (trigIsNaN(x)) return trigNaN();

    return float(trigAtanD(double(x)));
}

// π in two parts (fdlibm's e_atan2.c).
const double TrigPiD = 3.141592653589793LF;
const double TrigPiLow = 1.2246467991473532e-16LF;

// atan2(y, x), the float nearest the exact answer as glibc's atan2f has it, quadrant by quadrant and signed zero by signed
// zero.
float trigAtan2(float y, float x)
{
    if (trigIsNaN(x) || trigIsNaN(y)) return trigNaN();

    bool below = (floatBitsToUint(y) >> 31) != 0u;
    bool behind = (floatBitsToUint(x) >> 31) != 0u;
    if (x == 0.0)
    {
        if (y == 0.0) return behind ? (below ? -TrigPi : TrigPi) : y;
        return below ? -TrigHalfPi : TrigHalfPi;
    }

    if (trigIsInfinite(x))
    {
        float rad = trigIsInfinite(y) ? (behind ? 3.0 * TrigQuarterPi : TrigQuarterPi) : (behind ? TrigPi : 0.0);
        return below ? -rad : rad;
    }

    if (trigIsInfinite(y)) return below ? -TrigHalfPi : TrigHalfPi;
    if (y == 0.0) return behind ? (below ? -TrigPi : TrigPi) : y;

    precise double ratio = abs(double(y) / double(x));
    precise double z = trigAtanD(ratio);
    precise double rad = !behind ? z : TrigPiD - (z - TrigPiLow);
    return float(below ? -rad : rad);
}

// fdlibm's __kernel_tan (k_tan.c, 2004): its series, and π/4 in two parts.
const double TrigTan0 = 0.3333333333333341LF;
const double TrigTan1 = 0.13333333333320124LF;
const double TrigTan2 = 0.05396825397622605LF;
const double TrigTan3 = 0.021869488294859542LF;
const double TrigTan4 = 0.0088632398235993LF;
const double TrigTan5 = 0.0035920791075913124LF;
const double TrigTan6 = 0.0014562094543252903LF;
const double TrigTan7 = 0.0005880412408202641LF;
const double TrigTan8 = 0.0002464631348184699LF;
const double TrigTan9 = 7.817944429395571e-05LF;
const double TrigTan10 = 7.140724913826082e-05LF;
const double TrigTan11 = -1.8558637485527546e-05LF;
const double TrigTan12 = 2.590730518636337e-05LF;
const double TrigQuarterPiD = 0.7853981633974483LF;
const double TrigQuarterPiLowD = 3.061616997868383e-17LF;

// tan(x + tail) for |x + tail| ≤ π/4, or -1/tan of it where cotangent is set (fdlibm's __kernel_tan).
double trigTanKernel(double x, double tail, bool cotangent)
{
    double magnitude = abs(x);
    if (magnitude < 3.725290298461914e-09LF)
    {
        if (!cotangent) return x;

        precise double sum = x + tail;
        precise double sumHigh = trigHighWord(sum);
        precise double sumTail = tail - (sumHigh - x);
        precise double inverse = -1.0LF / sum;
        precise double inverseHigh = trigHighWord(inverse);
        precise double correction = 1.0LF + inverseHigh * sumHigh;
        precise double cot = inverseHigh + inverse * (correction + inverseHigh * sumTail);
        return cot;
    }

    bool beyond = magnitude >= 0.6744LF;
    bool negative = x < 0.0LF;
    precise double near = x;
    precise double nearTail = tail;
    if (beyond)
    {
        precise double folded = TrigQuarterPiD - (negative ? -x : x);
        precise double foldedTail = TrigQuarterPiLowD - (negative ? -tail : tail);
        near = folded + foldedTail;
        nearTail = 0.0LF;
    }

    precise double z = near * near;
    precise double w = z * z;
    precise double odd = TrigTan1 + w * (TrigTan3 + w * (TrigTan5 + w * (TrigTan7 + w * (TrigTan9 + w * TrigTan11))));
    precise double even = z * (TrigTan2 + w * (TrigTan4 + w * (TrigTan6 + w * (TrigTan8 + w * (TrigTan10 + w * TrigTan12)))));
    precise double cubed = z * near;
    precise double rest = nearTail + z * (cubed * (odd + even) + nearTail);
    rest += TrigTan0 * cubed;
    precise double sum = near + rest;
    if (beyond)
    {
        double turn = cotangent ? -1.0LF : 1.0LF;
        precise double tangent = turn - 2.0LF * (near - (sum * sum / (sum + turn) - rest));
        return negative ? -tangent : tangent;
    }

    if (!cotangent) return sum;

    precise double sumHigh = trigHighWord(sum);
    precise double sumTail = rest - (sumHigh - near);
    precise double inverse = -1.0LF / sum;
    precise double inverseHigh = trigHighWord(inverse);
    precise double correction = 1.0LF + inverseHigh * sumHigh;
    precise double cot = inverseHigh + inverse * (correction + inverseHigh * sumTail);
    return cot;
}

// π/2 in three steps of 33 bits each and what is left past them (fdlibm's __ieee754_rem_pio2), so a multiple of each step
// below 2^20 is exact, and 2/π.
const double TrigTwoOverPiD = 0.6366197723675814LF;
const double TrigHalfPiStep1 = 1.5707963267341256LF;
const double TrigHalfPiStep2 = 6.077100506303966e-11LF;
const double TrigHalfPiStep3 = 2.0222662487111665e-21LF;
const double TrigHalfPiStep3Left = 8.4784276603689e-32LF;

// Below 2^19 quarter turns the three steps reduce exactly enough; past them, the sine over the cosine.
const float TrigTanReducedBelow = 823549.6;

// tan(x), the float nearest the exact answer as glibc's tanf (correctly rounded, CORE-MATH) has it, for |x| below 2^19
// quarter turns; past them, the sine over the cosine, within a float or two.
float trigTan(float x)
{
    if (trigIsNaN(x) || trigIsInfinite(x)) return trigNaN();
    if (x == 0.0) return x;

    if (abs(x) >= TrigTanReducedBelow)
    {
        float sine;
        float cosine;
        trigSinCos(x, sine, cosine);
        precise float tangent = sine / cosine;
        return tangent;
    }

    precise double magnitude = abs(double(x));
    int quarters = int(magnitude * TrigTwoOverPiD + 0.5LF);
    double turns = double(quarters);
    precise double reduced = magnitude - turns * TrigHalfPiStep1;
    reduced -= turns * TrigHalfPiStep2;
    precise double was = reduced;
    precise double third = turns * TrigHalfPiStep3;
    reduced = was - third;
    precise double left = turns * TrigHalfPiStep3Left - ((was - reduced) - third);
    precise double head = reduced - left;
    precise double tail = (reduced - head) - left;
    if (x < 0.0)
    {
        head = -head;
        tail = -tail;
    }

    return float(trigTanKernel(head, tail, (quarters & 1) != 0));
}

#endif
