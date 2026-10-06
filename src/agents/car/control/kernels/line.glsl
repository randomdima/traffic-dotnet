// The route's line under a car, read on its tick (LineReader.Advance, RunsShort; CarFleet.ShiftTheLine): where along
// it the rear axle is, the lane it has left shifted off it, and whether what is left has run short of its sight.
//
// A car's line lives in the fleet's own layout, one room a car: SPLINE_ARCS and LINE_ENTRIES hold LINE_FIGURES.arcsPerCar
// to a car, and LANE_CHAIN, LANE_START, LANE_END, LANE_FIRST_ARC and JOIN_BREAKS hold LINE_FIGURES.mostLanes. The
// including kernel declares them writable, includes arc.glsl, car_build.glsl and arc_chain.glsl first, and supplies
// LINE_FIGURES' fields arcsPerCar, mostLanes and roundingM.

// How much of a car's line there is (DrivenLine), and whether it ends where the leg does.
struct CarLineRow
{
    uint arcCount;
    uint laneCount;
    float lengthM;
    uint finalApproach;
};

// Where a tick found the car on its line (LineAdvance): shiftM is how far the metres moved back, or nought.
struct LineAdvance
{
    float progressM;
    float coveredM;
    float shiftM;
};

// The line shifted on past its first lane: that lane and the join out of it dropped, everything else kept as laid, the
// metres measured from the second lane's own start (CarFleet.ShiftTheLine).
float lineShift(uint car, inout CarLineRow line)
{
    uint arcs = car * LINE_FIGURES.arcsPerCar;
    uint slots = car * LINE_FIGURES.mostLanes;
    float shiftM = LANE_START[slots + 1u];
    uint shiftArcs = LANE_FIRST_ARC[slots + 1u];
    for (uint at = shiftArcs; at < line.arcCount; at++)
    {
        SPLINE_ARCS[arcs + at - shiftArcs] = SPLINE_ARCS[arcs + at];
        LINE_ENTRIES[arcs + at - shiftArcs] = LINE_ENTRIES[arcs + at];
    }

    for (uint slot = 0u; slot + 1u < line.laneCount; slot++)
    {
        LANE_CHAIN[slots + slot] = LANE_CHAIN[slots + slot + 1u];
        precise float startM = LANE_START[slots + slot + 1u] - shiftM;
        precise float endM = LANE_END[slots + slot + 1u] - shiftM;
        LANE_START[slots + slot] = startM;
        LANE_END[slots + slot] = endM;
        LANE_FIRST_ARC[slots + slot] = LANE_FIRST_ARC[slots + slot + 1u] - shiftArcs;
        JOIN_BREAKS[slots + slot] = JOIN_BREAKS[slots + slot + 1u];
    }

    precise float lengthM = line.lengthM - shiftM;
    line = CarLineRow(line.arcCount - shiftArcs, line.laneCount - 1u, lengthM, line.finalApproach);
    return shiftM;
}

// Where along its line the car is now, the line shifted on past a lane the axle has left and the car found again on
// what is left of it (LineReader.Advance).
LineAdvance lineAdvance(uint car, CarBuildRow build, vec2 rearAxleM, float lastM, inout CarLineRow line)
{
    uint first = car * LINE_FIGURES.arcsPerCar;
    float offSq;
    float progressM = splineProject(first, first + line.arcCount, rearAxleM, lastM, build.projectionWindowM, offSq);
    precise float coveredM = abs(progressM - lastM);

    float nextLaneStartsM = LANE_START[car * LINE_FIGURES.mostLanes + 1u];
    precise float lastStartM = line.lengthM - LINE_FIGURES.roundingM;
    if (line.laneCount < 2u || progressM < nextLaneStartsM || nextLaneStartsM >= lastStartM)
    {
        return LineAdvance(progressM, coveredM, 0.0);
    }

    float shiftM = lineShift(car, line);
    precise float seedM = progressM - shiftM;
    progressM = splineProject(first, first + line.arcCount, rearAxleM, max(0.0, seedM), build.projectionWindowM, offSq);
    return LineAdvance(progressM, coveredM, shiftM);
}

// Whether the line has run short of the car's sight with road past its end to grow it over (LineReader.RunsShort).
bool lineRunsShort(CarBuildRow build, CarLineRow line, float progressM, bool roadRunsOn)
{
    precise float leftM = line.lengthM - progressM;
    return leftM < build.sightM && line.laneCount < LINE_FIGURES.mostLanes && line.finalApproach == 0u && roadRunsOn;
}

// How far off its own line the rear axle is, asideM to the right of it on a pass (CarFollower.OffLineM).
float lineOffM(uint car, CarLineRow line, vec2 rearAxleM, float progressM, float asideM)
{
    uint first = car * LINE_FIGURES.arcsPerCar;
    vec2 onM;
    float headingRad;
    splineSample(first, first + line.arcCount, progressM, onM, headingRad);
    vec2 right = vec2(-sin(headingRad), cos(headingRad));
    precise vec2 off = onM + right * asideM - rearAxleM;
    precise float offSq = off.x * off.x + off.y * off.y;
    return sqrt(offSq);
}
