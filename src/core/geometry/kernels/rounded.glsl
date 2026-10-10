// A quotient and a square root as the host's float arithmetic gives them — the float nearest the exact answer — on a
// device Vulkan lets round both its own way (OpFDiv to 2.5 ULP, sqrt to inversesqrt's error). RADV's own misses the
// nearest on 28 % of quotients and 15 % of roots. So every quotient and root a kernel shares with the host is taken here,
// and a kernel is the host's to the bit on every driver rather than on one that happens to round them.
//
// The device's own answer is moved a float at a time until it is the nearest. Which side of the midpoint between two
// neighbours the exact answer lies is asked in doubles, which hold that midpoint (25 bits) times a float (24) or squared
// exactly, and the exact answer never lies on a midpoint, so there is no tie to break. The including kernel gets 64-bit
// floats with it. Included by whatever needs it, as often as it likes.

#ifndef ROUNDED_GLSL
#define ROUNDED_GLSL

// How far off the device's own answer is looked for, in floats either way: past Vulkan's bound for either operation.
const int RoundedMostSteps = 4;

// The float next above or below a positive one.
float roundedAbove(float x) { return uintBitsToFloat(floatBitsToUint(x) + 1u); }
float roundedBelow(float x) { return uintBitsToFloat(floatBitsToUint(x) - 1u); }

// a / b, the float nearest the exact quotient.
float roundedQuotient(float a, float b)
{
    precise float q = a / b;
    if (q == 0.0 || isinf(q) || isnan(q)) return q;

    double dividend = abs(double(a));
    double divisor = abs(double(b));
    float m = abs(q);
    for (int step = 0; step < RoundedMostSteps; step++)
    {
        float below = roundedBelow(m);
        precise double edge = ((double(below) + double(m)) * 0.5LF) * divisor;
        if (!(dividend < edge)) break;
        m = below;
    }

    for (int step = 0; step < RoundedMostSteps; step++)
    {
        float above = roundedAbove(m);
        precise double edge = ((double(m) + double(above)) * 0.5LF) * divisor;
        if (!(dividend > edge)) break;
        m = above;
    }

    return (a < 0.0) != (b < 0.0) ? -m : m;
}

// sqrt(x), the float nearest the exact root.
float roundedSqrt(float x)
{
    precise float s = sqrt(x);
    if (!(x > 0.0) || isinf(x) || s == 0.0) return s;

    double square = double(x);
    for (int step = 0; step < RoundedMostSteps; step++)
    {
        float below = roundedBelow(s);
        precise double edge = (double(below) + double(s)) * 0.5LF;
        if (!(square < edge * edge)) break;
        s = below;
    }

    for (int step = 0; step < RoundedMostSteps; step++)
    {
        float above = roundedAbove(s);
        precise double edge = (double(s) + double(above)) * 0.5LF;
        if (!(square > edge * edge)) break;
        s = above;
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
