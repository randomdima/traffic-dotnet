// A quotient and a square root as the host's float arithmetic gives them — the float nearest the exact answer — on a
// device Vulkan lets round both its own way (OpFDiv to 2.5 ULP, sqrt to inversesqrt's error). Where a discrete verdict
// is taken on one, a last bit is a different answer: which lattice point a body's edge reaches, which side of a band a
// point stands.
//
// The device's own answer is moved a float at a time until it is the nearest. Which side of the midpoint between two
// neighbours the exact answer lies is asked in doubles, which hold that midpoint (25 bits) times a float (24) or squared
// exactly, and the exact answer never lies on a midpoint, so there is no tie to break. The including kernel enables
// 64-bit floats.

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
