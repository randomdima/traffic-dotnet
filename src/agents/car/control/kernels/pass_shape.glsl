// How a step across is drawn on the device (CarFollower.ShapeAPass, StepMps; Overtake.ShortestStepM, MostBendOfAStep,
// MostBendChangeOfAStep): line for line with the host, every quotient and square root the float nearest the exact answer
// (rounded.glsl), a cube root put right in doubles, and `precise` so nothing is fused — a verdict is taken on each.
//
// The including kernel enables 64-bit floats, includes car_build.glsl and rounded.glsl first, and supplies
// PASS_SHAPE_FIGURES' fields tau, tauSquared and fourPiSquared: the host's own float products of π (Overtake's Tau, Tau ×
// Tau, and 4 × π × π), so no constant is folded the device's way.

#ifndef PASS_SHAPE_GLSL
#define PASS_SHAPE_GLSL

float passInfinity() { return uintBitsToFloat(0x7F800000u); }

// How many of Newton's steps a cube root is put right in: from a float's estimate, past a double's last bit.
const int PassCbrtSteps = 3;

// The cube root of x, the float nearest it (MathF.Cbrt): the device's own estimate, put right in doubles and rounded once.
float passCbrt(float x)
{
    if (!(x > 0.0) || isinf(x)) return x;

    double root = double(pow(x, 1.0 / 3.0));
    double cube = double(x);
    for (int step = 0; step < PassCbrtSteps; step++)
    {
        precise double squared = root * root;
        precise double next = root - ((root * squared) - cube) / (3.0LF * squared);
        root = next;
    }

    return float(root);
}

// The shortest step asideM across whose bend is never more than mostBend and never changes by more than bendPerM a metre
// (Overtake.ShortestStepM).
float passShortestStepM(float asideM, float mostBend, float bendPerM)
{
    float acrossM = abs(asideM);
    precise float bendArea = PASS_SHAPE_FIGURES.tau * acrossM;
    float byTheBendM = roundedSqrt(roundedQuotient(bendArea, mostBend));
    precise float rackVolume = PASS_SHAPE_FIGURES.tauSquared * acrossM;
    float byTheRackM = passCbrt(roundedQuotient(rackVolume, bendPerM));
    return max(byTheBendM, byTheRackM);
}

// The most a step of this length asideM across bends: 2π·d ⁄ L² (Overtake.MostBendOfAStep).
float passMostBendOfAStep(float asideM, float stepM)
{
    precise float across = PASS_SHAPE_FIGURES.tau * abs(asideM);
    precise float squared = stepM * stepM;
    return roundedQuotient(across, squared);
}

// And the most its bend changes a metre: 4π²·d ⁄ L³ (Overtake.MostBendChangeOfAStep).
float passMostBendChangeOfAStep(float asideM, float stepM)
{
    precise float across = PASS_SHAPE_FIGURES.tauSquared * abs(asideM);
    precise float cubed = stepM * stepM * stepM;
    return roundedQuotient(across, cubed);
}

// How a pass is drawn for a car doing speedMps (CarFollower.ShapeAPass): the speed its steps are drawn for, and the
// shortest step that speed allows — false where the line under it bends as tight as the lock on its own.
bool passShape(CarBuildRow car, float speedMps, float asideM, float corneringMps2, float lineBend, out float stepM, out float driveMps)
{
    stepM = 0.0;
    driveMps = 0.0;
    precise float lockBend = roundedQuotient(1.0, car.turningRadiusM) - lineBend;
    if (lockBend <= 0.0) return false;

    float acrossM = abs(asideM);
    float atTheLockM = passShortestStepM(acrossM, lockBend, passInfinity());
    precise float rackTurn = atTheLockM * atTheLockM * atTheLockM * car.steerRateRadPerS;
    precise float rackReach = PASS_SHAPE_FIGURES.fourPiSquared * acrossM * car.wheelbaseM;
    float rackMps = roundedQuotient(rackTurn, rackReach);
    precise float gripSquared = corneringMps2 * car.turningRadiusM;
    float gripMps = roundedSqrt(gripSquared);
    driveMps = max(speedMps, min(rackMps, gripMps));

    precise float driveSquared = driveMps * driveMps;
    precise float tyresBend = roundedQuotient(corneringMps2, driveSquared) - lineBend;
    float mostBend = min(lockBend, tyresBend);
    if (mostBend <= 0.0) return false;

    precise float rackPerM = driveMps * car.wheelbaseM;
    stepM = passShortestStepM(acrossM, mostBend, roundedQuotient(car.steerRateRadPerS, rackPerM));
    return true;
}

// The most a step this long may be driven at (CarFollower.StepMps): its bend and the line's under it inside what the tyres
// hold at the speed, and its bend changing no faster than the rack turns while the car rolls it. Nothing where the two
// together bend tighter than the lock.
float passStepMps(CarBuildRow car, float stepM, float asideM, float corneringMps2, float lineBend)
{
    precise float bend = passMostBendOfAStep(asideM, stepM) + lineBend;
    if (bend > roundedQuotient(1.0, car.turningRadiusM)) return 0.0;

    float gripMps = roundedSqrt(roundedQuotient(corneringMps2, bend));
    precise float rackPerM = passMostBendChangeOfAStep(asideM, stepM) * car.wheelbaseM;
    float rackMps = roundedQuotient(car.steerRateRadPerS, rackPerM);
    return min(gripMps, rackMps);
}

#endif
