// Trigonometry the device answers to within a float or two of the host's (MathF.Sin, Cos, Tan, Atan, Atan2), on every
// driver alike. Vulkan lets a driver's own atan stand 4096 floats off and leaves sin and cos to an absolute bound, and
// RADV's stand a part in a hundred thousand off — which an arc of 8 km radius turns into a centimetre of where a car is
// on its line, and a chord of 500 m into half a millimetre of where its lead point is. Cephes' minimax polynomials,
// `precise` so no driver fuses them. Included by whatever needs it, as often as it likes.

#ifndef TRIG_GLSL
#define TRIG_GLSL

const float TrigPi = 3.14159265358979323846;
const float TrigHalfPi = 1.57079632679489661923;
const float TrigQuarterPi = 0.78539816339744830962;
const float TrigFourOverPi = 1.27323954473516268615;

// π/4 in three parts, the first two short enough that a multiple of them below 8192 is exact (Cody and Waite).
const float TrigQuarterPiHigh = 0.78515625;
const float TrigQuarterPiMiddle = 2.4187564849853515625e-4;
const float TrigQuarterPiLow = 3.77489497744594108e-8;

// sin and cos of x, each within a float or so of the exact answer for |x| below 8192 (Cephes sinf and cosf, sharing the
// reduction).
void trigSinCos(float x, out float sine, out float cosine)
{
    float magnitude = abs(x);
    uint octant = uint(TrigFourOverPi * magnitude);
    float folds = float(octant);
    if ((octant & 1u) != 0u)
    {
        octant += 1u;
        folds += 1.0;
    }

    octant &= 7u;
    precise float reduced = ((magnitude - folds * TrigQuarterPiHigh) - folds * TrigQuarterPiMiddle) - folds * TrigQuarterPiLow;
    precise float z = reduced * reduced;
    precise float sinSeries = (((-1.9515295891e-4 * z + 8.3321608736e-3) * z - 1.6666654611e-1) * z * reduced) + reduced;
    precise float cosSeries = (((2.443315711809948e-5 * z - 1.388731625493765e-3) * z + 4.166664568298827e-2) * z * z) - (0.5 * z) + 1.0;

    bool swapped = (octant & 2u) != 0u;
    float s = swapped ? cosSeries : sinSeries;
    float c = swapped ? sinSeries : cosSeries;
    sine = (octant > 3u) != (x < 0.0) ? -s : s;
    cosine = octant == 2u || octant == 4u ? -c : c;
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
