// How far across the lane beside stands on the device (Switching.AsideOnTheLaneBeside, Aside), off the road's own lanes:
// the nearest point of the lane beside's line to where the car's lane is at a place on it, sought where the two lengths
// put it, along the driver's right. Its arcs' arithmetic is arc_chain.glsl's.
//
// The including kernel includes arc.glsl and arc_chain.glsl (its SPLINE_ARCS the road's lanes) and trig.glsl, and declares
// LANE_FIRST_ARC (uint[] a lane and one past the last: where its arcs begin), LANE_LENGTH and LANE_WIDTH (float[] a lane).

// How far across a lane beside stands at a place on a lane, along the driver's right (Switching.Aside).
float switchAside(int lane, int beside, float alongM)
{
    float lengthM = LANE_LENGTH[lane];
    float besideLengthM = LANE_LENGTH[beside];
    uint first = LANE_FIRST_ARC[lane];
    uint end = LANE_FIRST_ARC[lane + 1];
    uint besideFirst = LANE_FIRST_ARC[beside];
    uint besideEnd = LANE_FIRST_ARC[beside + 1];

    vec2 onM;
    float headingRad;
    splineSample(first, end, alongM, onM, headingRad);
    vec2 direction = trigUnit(headingRad);
    vec2 right = vec2(-direction.y, direction.x);

    precise float apartM = abs(besideLengthM - lengthM);
    precise float windowM = apartM + LANE_WIDTH[lane] + LANE_WIDTH[beside];
    precise float aroundM = alongM * roundedQuotient(besideLengthM, lengthM);
    float offSq;
    float besideM = splineProject(besideFirst, besideEnd, onM, aroundM, windowM, offSq);

    vec2 besideAtM;
    float besideHeadingRad;
    splineSample(besideFirst, besideEnd, besideM, besideAtM, besideHeadingRad);
    precise vec2 across = besideAtM - onM;
    precise float asideM = across.x * right.x + across.y * right.y;
    return asideM;
}
