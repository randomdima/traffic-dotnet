// CornerLimits.Lay on the device: the speed each arc of a car's line may be entered at (S-2), as its square over the grip,
// laid from the last arc back into the car's own entry table. The includer defines the town's accessors (cars_…) and
// includes rounded.glsl first; the shares of grip are its figures, handed in as the C# hands in its config.

#ifndef CORNER_LIMITS_GLSL
#define CORNER_LIMITS_GLSL

// CornerLimits.StraightPerM: below this curvature an arc bounds nothing of its own.
const float CornerLimitsStraightPerM = 1e-4;

float cornerLimits_CornerM(float curvature, float cornerShare)
{
    float bend = abs(curvature);
    return bend < CornerLimitsStraightPerM ? uintBitsToFloat(0x7F800000u) : roundedQuotient(cornerShare, bend);
}

// Every arc's entry figure: its own corner, or the next arc's entry and a stop's worth of its own length, whichever is less.
void cornerLimits_Lay(uint car, uint arcCount, float gripMargin, float brakingMargin)
{
    float nextM = uintBitsToFloat(0x7F800000u);
    for (int arc = int(arcCount) - 1; arc >= 0; arc--)
    {
        vec4 b = cars_LineArcB(car, uint(arc));
        precise float brakedM = nextM + (2.0 * brakingMargin * b.y);
        nextM = min(cornerLimits_CornerM(b.z, gripMargin), brakedM);
        cars_LineEntry(car, uint(arc)) = nextM;
    }
}

#endif
